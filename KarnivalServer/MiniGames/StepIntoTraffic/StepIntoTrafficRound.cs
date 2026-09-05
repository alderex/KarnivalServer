using Riptide;

public sealed class StepIntoTrafficRound : MiniGameRoundBase
{
    public const byte HopOutcomeEvent = 1;
    public const byte CollisionEvent = 2;
    public const byte FinishEvent = 3;

    private sealed class PlayerState
    {
        public PlayerState(float startsAtSeconds)
        {
            SimulatedThroughSeconds = startsAtSeconds;
            RegisteredAtSeconds = startsAtSeconds;
        }

        public int InputSampleCount { get; set; }
        public int Row { get; set; }
        public bool IsHopping { get; set; }
        public float HopStartedAtSeconds { get; set; }
        public float RecoveryEndsAtSeconds { get; set; }
        public float SimulatedThroughSeconds { get; set; }
        public float RegisteredAtSeconds { get; }
        public float LastInputSeconds { get; set; } = -1f;
    }

    private readonly IReadOnlyList<StepIntoTrafficLane> lanes;
    private readonly StepIntoTrafficSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public StepIntoTrafficRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        IReadOnlyList<StepIntoTrafficLane> lanes,
        StepIntoTrafficSettings settings)
        : base(roundId, MiniGameType.StepIntoTraffic, startsUtc,
            durationSeconds, resultsDurationSeconds, settings.CorrectScore)
    {
        this.lanes = lanes;
        this.settings = settings;
    }

    public IReadOnlyList<StepIntoTrafficLane> Lanes => lanes;

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsed = GetElapsedSeconds(nowUtc);
        playerStates.Add(session.ClientId, new PlayerState(elapsed));
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        float hopAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAt = GetElapsedSeconds(nowUtc);
        if (session.SubmittedThisRound ||
            !float.IsFinite(hopAtSeconds) ||
            hopAtSeconds < state.RegisteredAtSeconds ||
            hopAtSeconds > DurationSeconds ||
            hopAtSeconds <= state.LastInputSeconds ||
            hopAtSeconds > receivedAt + settings.FutureInputToleranceSeconds ||
            receivedAt - hopAtSeconds > settings.InputGraceSeconds ||
            state.InputSampleCount >= settings.MaximumInputSamples)
        {
            return;
        }

        Simulate(session, state, hopAtSeconds, server);
        state.LastInputSeconds = hopAtSeconds;
        state.InputSampleCount++;
        bool accepted = !session.SubmittedThisRound &&
            !state.IsHopping &&
            state.RecoveryEndsAtSeconds <= 0f &&
            hopAtSeconds >= state.SimulatedThroughSeconds -
                settings.SimulationStepSeconds;
        int authoritativeRow = state.Row;
        if (accepted)
        {
            state.IsHopping = true;
            state.HopStartedAtSeconds = hopAtSeconds;
        }

        SendHopOutcome(
            session,
            accepted,
            (byte)authoritativeRow,
            hopAtSeconds,
            server);
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        float target = Math.Max(0f,
            GetElapsedSeconds(nowUtc) - settings.InputGraceSeconds);
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, nowUtc);
            Simulate(session, playerStates[session.ClientId], target, server);
        }
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            PlayerState state = playerStates[session.ClientId];
            Simulate(session, state, DurationSeconds, server);
            if (!session.SubmittedThisRound)
            {
                CompleteSubmission(session, 0, server,
                    completedAtSeconds: DurationSeconds);
            }
        }
    }

    public override string Describe() => $"lanes={lanes.Count}";

    protected override void WriteStartedPayload(Message message)
    {
        message.AddFloat(settings.HopDurationSeconds);
        message.AddFloat(settings.CollisionRecoverySeconds);
        message.AddFloat(settings.CarHalfWidthNormalized);
        message.AddFloat(settings.CarHalfHeightRows);
        message.AddFloat(settings.PedestrianHalfWidthNormalized);
        message.AddFloat(settings.PedestrianHalfHeightRows);
        message.AddByte((byte)lanes.Count);
        foreach (StepIntoTrafficLane lane in lanes)
        {
            message.AddByte(lane.LaneIndex);
            message.AddBool(lane.MovesLeftToRight);
            message.AddFloat(lane.SpeedNormalizedPerSecond);
            message.AddFloat(lane.FirstCenterCrossingSeconds);
            message.AddFloat(lane.CrossingIntervalSeconds);
        }
    }

    private void Simulate(
        PlayerSession session,
        PlayerState state,
        float targetSeconds,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound ||
            targetSeconds <= state.SimulatedThroughSeconds)
        {
            return;
        }

        float step = Math.Max(0.001f, settings.SimulationStepSeconds);
        while (state.SimulatedThroughSeconds < targetSeconds &&
            !session.SubmittedThisRound)
        {
            float next = Math.Min(
                targetSeconds,
                state.SimulatedThroughSeconds + step);
            AdvanceState(session, state, next, server);
            state.SimulatedThroughSeconds = next;
        }
    }

    private void AdvanceState(
        PlayerSession session,
        PlayerState state,
        float elapsedSeconds,
        Riptide.Server server)
    {
        if (state.RecoveryEndsAtSeconds > 0f)
        {
            if (elapsedSeconds < state.RecoveryEndsAtSeconds)
                return;
            state.RecoveryEndsAtSeconds = 0f;
            state.Row = 0;
            state.IsHopping = false;
        }

        if (state.IsHopping &&
            elapsedSeconds >=
                state.HopStartedAtSeconds + settings.HopDurationSeconds)
        {
            state.Row++;
            state.IsHopping = false;
            if (state.Row > lanes.Count)
            {
                SendFinish(session, elapsedSeconds, server);
                CompleteSubmission(session, settings.CorrectScore, server,
                    completedAtSeconds: elapsedSeconds);
                return;
            }
        }


        float playerRow = state.Row;
        if (state.IsHopping)
        {
            float progress = Math.Clamp(
                (elapsedSeconds - state.HopStartedAtSeconds) /
                    settings.HopDurationSeconds,
                0f,
                1f);
            playerRow += progress;
        }

        if (!StepIntoTrafficSchedule.TryGetCollision(
            lanes, playerRow, elapsedSeconds, settings, out var collision))
        {
            return;
        }

        state.IsHopping = false;
        state.Row = 0;
        state.RecoveryEndsAtSeconds = Math.Min(
            DurationSeconds,
            collision.CollisionSeconds + settings.CollisionRecoverySeconds);
        SendCollision(session, collision, state.RecoveryEndsAtSeconds, server);
    }

    private void SendHopOutcome(
        PlayerSession session,
        bool accepted,
        byte authoritativeRow,
        float hopAtSeconds,
        Riptide.Server server)
    {
        if (session.IsSimulated)
            return;

        Message message = CreateEvent(HopOutcomeEvent);
        message.AddBool(accepted);
        message.AddByte(authoritativeRow);
        message.AddFloat(hopAtSeconds);
        server.Send(message, session.ClientId);
    }
    private void SendCollision(
        PlayerSession session,
        StepIntoTrafficCollision collision,
        float recoveryEndsAtSeconds,
        Riptide.Server server)
    {
        if (session.IsSimulated)
            return;

        Message message = CreateEvent(CollisionEvent);
        message.AddByte(collision.LaneIndex);
        message.AddInt(collision.CarSequence);
        message.AddFloat(collision.CollisionSeconds);
        message.AddFloat(recoveryEndsAtSeconds);
        server.Send(message, session.ClientId);
    }

    private void SendFinish(
        PlayerSession session,
        float finishedAtSeconds,
        Riptide.Server server)
    {
        if (session.IsSimulated)
            return;

        Message message = CreateEvent(FinishEvent);
        message.AddFloat(finishedAtSeconds);
        server.Send(message, session.ClientId);
    }

    private Message CreateEvent(byte eventType)
    {
        Message message = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        message.AddUInt(RoundId);
        message.AddUShort((ushort)GameType);
        message.AddByte(eventType);
        return message;
    }

    private float GetElapsedSeconds(DateTime nowUtc) => Math.Clamp(
        (float)(nowUtc - StartsUtc).TotalSeconds,
        0f,
        DurationSeconds);
}
