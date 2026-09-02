using Riptide;

public sealed class StopGoRound : MiniGameRoundBase
{
    private const byte TerminalEvent = 1;
    private const byte SnapshotEvent = 2;
    private const float Epsilon = 0.00001f;

    private sealed class PlayerState
    {
        public PlayerState(PlayerSession session, float joinedAtSeconds)
        {
            Session = session;
            JoinedAtSeconds = joinedAtSeconds;
            SimulatedAtSeconds = joinedAtSeconds;
            LastSampleAtSeconds = joinedAtSeconds;
        }

        public PlayerSession Session { get; }
        public float JoinedAtSeconds { get; }
        public List<StopGoMovementSample> Samples { get; } = new();
        public int NextSampleIndex { get; set; }
        public float LastSampleAtSeconds { get; set; }
        public float SimulatedAtSeconds { get; set; }
        public float Progress { get; set; }
        public bool IsRunning { get; set; }
        public StopGoRacerStatus Status { get; set; }
        public float TerminalAtSeconds { get; set; }
    }

    private readonly uint scheduleSeed;
    private readonly IReadOnlyList<StopGoLightInterval> schedule;
    private readonly StopGoSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private float lastSnapshotAtSeconds = float.NegativeInfinity;

    public StopGoRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        uint scheduleSeed,
        IReadOnlyList<StopGoLightInterval> schedule,
        StopGoSettings settings)
        : base(
            roundId,
            MiniGameType.StopGo,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds)
    {
        this.scheduleSeed = scheduleSeed;
        this.schedule = schedule;
        this.settings = settings;
    }

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (session.IsSimulated || session.SubmittedThisRound ||
            playerStates.ContainsKey(session.ClientId))
        {
            return;
        }

        playerStates.Add(
            session.ClientId,
            new PlayerState(session, GetElapsedSeconds(nowUtc)));
    }

    public override void UnregisterPlayer(
        PlayerSession session,
        DateTime nowUtc,
        Riptide.Server server)
    {
        if (!playerStates.TryGetValue(session.ClientId, out PlayerState? state) ||
            state.Status != StopGoRacerStatus.Racing)
        {
            return;
        }

        ResolveZero(
            state,
            StopGoRacerStatus.Disconnected,
            GetElapsedSeconds(nowUtc),
            server,
            sendTerminal: false);
    }

    public override void SynchronizePlayer(
        PlayerSession session,
        Riptide.Server server)
    {
        if (!session.SubmittedThisRound)
            return;

        PlayerState? state = playerStates.Values.FirstOrDefault(
            candidate => candidate.Session.UserId == session.UserId);
        if (state != null && state.Status != StopGoRacerStatus.Racing)
            SendTerminal(state, state.TerminalAtSeconds, session.ClientId, server);
        base.SynchronizePlayer(session, server);
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        PlayerSession[] currentPlayers = sessions
            .Where(session => !session.IsSimulated)
            .ToArray();
        foreach (PlayerSession session in currentPlayers)
            RegisterPlayer(session, nowUtc);

        float elapsed = GetElapsedSeconds(nowUtc);
        float authoritativeTarget = Math.Clamp(
            elapsed - Math.Max(0f, settings.InputGraceSeconds),
            0f,
            DurationSeconds);
        foreach (PlayerSession session in currentPlayers)
        {
            if (playerStates.TryGetValue(session.ClientId, out PlayerState? state))
            {
                SimulateTo(
                    state,
                    Math.Max(state.JoinedAtSeconds, authoritativeTarget),
                    server);
            }
        }

        SendSnapshots(elapsed, currentPlayers, server);
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        bool isRunning = message.GetBool();
        float sampledAt = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        if (!playerStates.TryGetValue(session.ClientId, out PlayerState? state) ||
            state.Status != StopGoRacerStatus.Racing)
        {
            return;
        }

        float receivedAt = GetElapsedSeconds(nowUtc);
        if (float.IsNaN(sampledAt) ||
            sampledAt < state.JoinedAtSeconds ||
            sampledAt > DurationSeconds ||
            sampledAt + Epsilon < state.LastSampleAtSeconds ||
            sampledAt + Epsilon < state.SimulatedAtSeconds ||
            sampledAt > receivedAt +
                Math.Max(0f, settings.FutureInputToleranceSeconds) + Epsilon ||
            receivedAt - sampledAt >
                Math.Max(0f, settings.InputGraceSeconds) + Epsilon)
        {
            return;
        }

        bool latestRunning = state.NextSampleIndex < state.Samples.Count
            ? state.Samples[^1].IsRunning
            : state.IsRunning;
        if (latestRunning == isRunning)
            return;

        if (state.Samples.Count > 0 &&
            Math.Abs(state.Samples[^1].SampledAtSeconds - sampledAt) <= Epsilon)
        {
            if (state.NextSampleIndex < state.Samples.Count)
                state.Samples[^1] = new StopGoMovementSample(isRunning, sampledAt);
            return;
        }

        if (state.Samples.Count >= Math.Max(1, settings.MaximumInputSamples))
            return;

        state.Samples.Add(new StopGoMovementSample(isRunning, sampledAt));
        state.LastSampleAtSeconds = sampledAt;
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        PlayerSession[] currentPlayers = sessions
            .Where(session => !session.IsSimulated)
            .ToArray();
        foreach (PlayerSession session in currentPlayers)
            RegisterPlayer(session, EndsUtc);

        foreach (PlayerState state in playerStates.Values)
        {
            SimulateTo(state, DurationSeconds, server);
            if (state.Status == StopGoRacerStatus.Racing)
            {
                ResolveZero(
                    state,
                    StopGoRacerStatus.TimedOut,
                    DurationSeconds,
                    server);
            }
        }

        foreach (PlayerSession session in currentPlayers)
        {
            if (!session.SubmittedThisRound)
                CompleteSubmission(session, 0, server);
        }
    }

    public override string Describe()
    {
        return
            $"seed={scheduleSeed} lights={schedule.Count} " +
            $"traversal={Math.Max(0.1f, settings.ContinuousTraversalSeconds):0.###}s solo";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddUInt(scheduleSeed);
        message.AddFloat(Math.Max(0.1f, settings.ContinuousTraversalSeconds));
        message.AddFloat(Math.Max(0f, settings.RedGraceSeconds));
        message.AddUShort((ushort)schedule.Count);
        foreach (StopGoLightInterval interval in schedule)
        {
            message.AddBool(interval.IsGreen);
            message.AddFloat(interval.StartsAtSeconds);
            message.AddFloat(interval.EndsAtSeconds);
        }
    }

    private void SimulateTo(
        PlayerState state,
        float targetSeconds,
        Riptide.Server server)
    {
        if (state.Status != StopGoRacerStatus.Racing)
            return;

        float target = Math.Clamp(
            targetSeconds,
            state.SimulatedAtSeconds,
            DurationSeconds);
        while (state.Status == StopGoRacerStatus.Racing &&
            state.SimulatedAtSeconds <= target + Epsilon)
        {
            ProcessSamplesAtCurrentTime(state, server);
            if (state.Status != StopGoRacerStatus.Racing ||
                state.SimulatedAtSeconds >= target - Epsilon)
            {
                break;
            }

            StopGoLightInterval light = GetLightAt(state.SimulatedAtSeconds);
            float graceDeadline = light.IsGreen
                ? float.PositiveInfinity
                : light.StartsAtSeconds + Math.Max(0f, settings.RedGraceSeconds);
            if (!light.IsGreen && state.IsRunning &&
                state.SimulatedAtSeconds >= graceDeadline - Epsilon)
            {
                ResolveZero(
                    state,
                    StopGoRacerStatus.Eliminated,
                    graceDeadline,
                    server);
                break;
            }

            float nextAt = target;
            if (light.EndsAtSeconds > state.SimulatedAtSeconds + Epsilon)
                nextAt = Math.Min(nextAt, light.EndsAtSeconds);
            if (!light.IsGreen && state.IsRunning &&
                graceDeadline > state.SimulatedAtSeconds + Epsilon)
            {
                nextAt = Math.Min(nextAt, graceDeadline);
            }
            if (state.NextSampleIndex < state.Samples.Count)
            {
                float sampleAt = state.Samples[state.NextSampleIndex].SampledAtSeconds;
                if (sampleAt > state.SimulatedAtSeconds + Epsilon)
                    nextAt = Math.Min(nextAt, sampleAt);
            }

            if (nextAt <= state.SimulatedAtSeconds + Epsilon)
                nextAt = Math.Min(target, state.SimulatedAtSeconds + 0.0001f);
            Advance(state, nextAt, server);
        }
    }

    private void ProcessSamplesAtCurrentTime(
        PlayerState state,
        Riptide.Server server)
    {
        while (state.NextSampleIndex < state.Samples.Count &&
            state.Samples[state.NextSampleIndex].SampledAtSeconds <=
                state.SimulatedAtSeconds + Epsilon)
        {
            StopGoMovementSample sample = state.Samples[state.NextSampleIndex++];
            if (sample.IsRunning && !GetLightAt(sample.SampledAtSeconds).IsGreen)
            {
                ResolveZero(
                    state,
                    StopGoRacerStatus.Eliminated,
                    sample.SampledAtSeconds,
                    server);
                return;
            }

            state.IsRunning = sample.IsRunning;
        }
    }

    private void Advance(
        PlayerState state,
        float endsAtSeconds,
        Riptide.Server server)
    {
        float startsAt = state.SimulatedAtSeconds;
        float delta = Math.Max(0f, endsAtSeconds - startsAt);
        if (state.IsRunning && delta > 0f)
        {
            float speed = 1f / Math.Max(0.1f, settings.ContinuousTraversalSeconds);
            float distance = speed * delta;
            if (state.Progress + distance >= 1f - Epsilon)
            {
                float crossingAt = startsAt + ((1f - state.Progress) / speed);
                state.Progress = 1f;
                state.SimulatedAtSeconds = Math.Min(crossingAt, endsAtSeconds);
                state.IsRunning = false;
                state.Status = StopGoRacerStatus.Finished;
                state.TerminalAtSeconds = state.SimulatedAtSeconds;
                SendTerminal(state, state.SimulatedAtSeconds, server);
                CompleteSubmission(
                    state.Session,
                    100,
                    server,
                    completedAtSeconds: state.SimulatedAtSeconds);
                return;
            }

            state.Progress = Math.Clamp(state.Progress + distance, 0f, 1f);
        }

        state.SimulatedAtSeconds = endsAtSeconds;
    }

    private void ResolveZero(
        PlayerState state,
        StopGoRacerStatus status,
        float terminalAtSeconds,
        Riptide.Server server,
        bool sendTerminal = true)
    {
        if (state.Status != StopGoRacerStatus.Racing)
            return;

        state.Status = status;
        state.IsRunning = false;
        float resolvedAt = Math.Clamp(terminalAtSeconds, 0f, DurationSeconds);
        state.TerminalAtSeconds = resolvedAt;
        if (sendTerminal)
            SendTerminal(state, resolvedAt, server);
        CompleteSubmission(
            state.Session,
            0,
            server,
            completedAtSeconds: resolvedAt);
    }

    private void SendTerminal(
        PlayerState state,
        float terminalAtSeconds,
        Riptide.Server server)
    {
        SendTerminal(state, terminalAtSeconds, state.Session.ClientId, server);
    }

    private void SendTerminal(
        PlayerState state,
        float terminalAtSeconds,
        ushort clientId,
        Riptide.Server server)
    {
        Message message = CreateEvent(MessageSendMode.Reliable, TerminalEvent);
        message.AddByte((byte)state.Status);
        message.AddFloat(state.Progress);
        message.AddFloat(terminalAtSeconds);
        server.Send(message, clientId);
    }

    private void SendSnapshots(
        float elapsedSeconds,
        IReadOnlyList<PlayerSession> recipients,
        Riptide.Server server)
    {
        float interval = Math.Max(0.05f, settings.SnapshotIntervalSeconds);
        if (elapsedSeconds < lastSnapshotAtSeconds + interval)
            return;
        lastSnapshotAtSeconds = elapsedSeconds;

        foreach (PlayerSession recipient in recipients)
        {
            if (!playerStates.TryGetValue(recipient.ClientId, out PlayerState? state) ||
                state.Status != StopGoRacerStatus.Racing)
            {
                continue;
            }

            Message message = CreateEvent(MessageSendMode.Unreliable, SnapshotEvent);
            message.AddFloat(state.SimulatedAtSeconds);
            message.AddFloat(state.Progress);
            message.AddBool(state.IsRunning);
            server.Send(message, recipient.ClientId);
        }
    }

    private StopGoLightInterval GetLightAt(float elapsedSeconds)
    {
        foreach (StopGoLightInterval interval in schedule)
        {
            if (elapsedSeconds >= interval.StartsAtSeconds - Epsilon &&
                elapsedSeconds < interval.EndsAtSeconds - Epsilon)
            {
                return interval;
            }
        }

        return schedule[^1];
    }

    private Message CreateEvent(MessageSendMode mode, byte eventType)
    {
        Message message = Message.Create(mode, NetworkMessageId.MiniGameEvent);
        message.AddUInt(RoundId);
        message.AddUShort((ushort)GameType);
        message.AddByte(eventType);
        return message;
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
