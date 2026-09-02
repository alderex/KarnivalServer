using Riptide;

public sealed class SmackManRound : MiniGameRoundBase
{
    private const byte ClickOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public PlayerState(
            IReadOnlyList<SmackManScheduleEntry> eligibleAppearances,
            int maximumScore)
        {
            EligibleAppearances = eligibleAppearances;
            MaximumScore = maximumScore;
        }

        public IReadOnlyList<SmackManScheduleEntry> EligibleAppearances { get; }
        public HashSet<ushort> ResolvedAppearanceIds { get; } = new();
        public int MaximumScore { get; }
        public int HitCount { get; set; }
    }

    private readonly IReadOnlyList<SmackManScheduleEntry> schedule;
    private readonly SmackManSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private readonly uint scheduleSeed;
    private readonly int pointsPerHit;
    private readonly float riseDurationSeconds;
    private readonly float holdDurationSeconds;
    private readonly float retractDurationSeconds;
    private readonly float activeDurationSeconds;

    public SmackManRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        uint scheduleSeed,
        int pointsPerHit,
        float riseDurationSeconds,
        float holdDurationSeconds,
        float retractDurationSeconds,
        IReadOnlyList<SmackManScheduleEntry> schedule,
        SmackManSettings settings)
        : base(
            roundId,
            MiniGameType.SmackMan,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            schedule.Count * pointsPerHit)
    {
        this.scheduleSeed = scheduleSeed;
        this.pointsPerHit = pointsPerHit;
        this.riseDurationSeconds = riseDurationSeconds;
        this.holdDurationSeconds = holdDurationSeconds;
        this.retractDurationSeconds = retractDurationSeconds;
        this.activeDurationSeconds =
            riseDurationSeconds + holdDurationSeconds + retractDurationSeconds;
        this.schedule = schedule;
        this.settings = settings;
    }

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public IReadOnlyList<SmackManScheduleEntry> Schedule => schedule;

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        SmackManScheduleEntry[] eligibleAppearances = schedule
            .Where(entry => entry.SpawnSeconds + activeDurationSeconds > elapsedSeconds)
            .ToArray();
        playerStates.Add(
            session.ClientId,
            new PlayerState(
                eligibleAppearances,
                eligibleAppearances.Length * pointsPerHit));
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        float graceSeconds = Math.Max(0f, settings.InputGraceSeconds);
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, nowUtc);
            PlayerState state = playerStates[session.ClientId];
            foreach (SmackManScheduleEntry entry in state.EligibleAppearances)
            {
                if (!state.ResolvedAppearanceIds.Contains(entry.AppearanceId) &&
                    elapsedSeconds >= entry.SpawnSeconds +
                        activeDurationSeconds + graceSeconds)
                {
                    state.ResolvedAppearanceIds.Add(entry.AppearanceId);
                }
            }

            TryCompletePlayer(session, state, server);
        }
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        ushort appearanceId = message.GetUShort();
        float clickedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        SmackManScheduleEntry? matchingEntry = state.EligibleAppearances
            .Where(entry => entry.AppearanceId == appearanceId)
            .Select(entry => (SmackManScheduleEntry?)entry)
            .FirstOrDefault();
        bool accepted = matchingEntry.HasValue &&
            !state.ResolvedAppearanceIds.Contains(appearanceId) &&
            IsValidTimestamp(clickedAtSeconds, receivedAtSeconds) &&
            IsVisible(matchingEntry.Value, clickedAtSeconds, activeDurationSeconds);

        if (accepted)
        {
            state.ResolvedAppearanceIds.Add(appearanceId);
            state.HitCount++;
        }

        SendClickOutcome(session, appearanceId, accepted, server);
        TryCompletePlayer(session, state, server);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            PlayerState state = playerStates[session.ClientId];
            foreach (SmackManScheduleEntry entry in state.EligibleAppearances)
                state.ResolvedAppearanceIds.Add(entry.AppearanceId);

            TryCompletePlayer(session, state, server);
            if (!session.SubmittedThisRound)
                session.MarkMissedRound();
        }
    }

    public override string Describe()
    {
        return $"seed={scheduleSeed} appearances={schedule.Count} points={pointsPerHit}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddUInt(scheduleSeed);
        message.AddInt(pointsPerHit);
        message.AddFloat(riseDurationSeconds);
        message.AddFloat(holdDurationSeconds);
        message.AddFloat(retractDurationSeconds);
        message.AddUShort((ushort)schedule.Count);
        foreach (SmackManScheduleEntry entry in schedule)
        {
            message.AddUShort(entry.AppearanceId);
            message.AddByte(entry.HoleIndex);
            message.AddFloat(entry.SpawnSeconds);
        }
    }

    public static bool IsVisible(
        SmackManScheduleEntry entry,
        float elapsedSeconds,
        float activeDurationSeconds)
    {
        return elapsedSeconds >= entry.SpawnSeconds &&
            elapsedSeconds < entry.SpawnSeconds + activeDurationSeconds;
    }

    private bool IsValidTimestamp(float clickedAtSeconds, float receivedAtSeconds)
    {
        return !float.IsNaN(clickedAtSeconds) &&
            clickedAtSeconds >= 0f &&
            clickedAtSeconds <= DurationSeconds &&
            clickedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - clickedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private void SendClickOutcome(
        PlayerSession session,
        ushort appearanceId,
        bool accepted,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(ClickOutcomeEvent);
        outcome.AddUShort(appearanceId);
        outcome.AddBool(accepted);
        server.Send(outcome, session.ClientId);
    }

    private void TryCompletePlayer(
        PlayerSession session,
        PlayerState state,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound ||
            state.ResolvedAppearanceIds.Count < state.EligibleAppearances.Count)
        {
            return;
        }

        CompleteSubmission(
            session,
            state.HitCount * pointsPerHit,
            server,
            maximumScore: state.MaximumScore,
            completedAtSeconds: DurationSeconds);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
