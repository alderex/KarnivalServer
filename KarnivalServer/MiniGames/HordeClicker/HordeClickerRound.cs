using Riptide;

public sealed class HordeClickerRound : MiniGameRoundBase
{
    private const byte ClickOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public PlayerState(
            IReadOnlyList<HordeClickerScheduleEntry> eligibleCharacters,
            int maximumScore)
        {
            EligibleCharacters = eligibleCharacters;
            MaximumScore = maximumScore;
        }

        public IReadOnlyList<HordeClickerScheduleEntry> EligibleCharacters { get; }
        public HashSet<ushort> ResolvedCharacterIds { get; } = new();
        public int MaximumScore { get; }
        public int ClickedCount { get; set; }
        public float LastResolvedSeconds { get; set; }
    }

    private readonly IReadOnlyList<HordeClickerScheduleEntry> schedule;
    private readonly HordeClickerSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private readonly uint scheduleSeed;
    private readonly int characterCount;
    private readonly int pointsPerCharacter;
    private readonly float firstSpawnSeconds;
    private readonly float lastSpawnSeconds;
    private readonly float minimumTravelDurationSeconds;
    private readonly float maximumTravelDurationSeconds;
    private readonly float minimumYNormalized;
    private readonly float maximumYNormalized;

    public HordeClickerRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        uint scheduleSeed,
        int characterCount,
        int pointsPerCharacter,
        float firstSpawnSeconds,
        float lastSpawnSeconds,
        float minimumTravelDurationSeconds,
        float maximumTravelDurationSeconds,
        float minimumYNormalized,
        float maximumYNormalized,
        IReadOnlyList<HordeClickerScheduleEntry> schedule,
        HordeClickerSettings settings)
        : base(
            roundId,
            MiniGameType.HordeClicker,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            characterCount * pointsPerCharacter)
    {
        this.scheduleSeed = scheduleSeed;
        this.characterCount = characterCount;
        this.pointsPerCharacter = pointsPerCharacter;
        this.firstSpawnSeconds = firstSpawnSeconds;
        this.lastSpawnSeconds = lastSpawnSeconds;
        this.minimumTravelDurationSeconds = minimumTravelDurationSeconds;
        this.maximumTravelDurationSeconds = maximumTravelDurationSeconds;
        this.minimumYNormalized = minimumYNormalized;
        this.maximumYNormalized = maximumYNormalized;
        this.schedule = schedule;
        this.settings = settings;
    }

    protected override float SpeedBonusDurationSeconds => schedule.Count == 0
        ? DurationSeconds
        : Math.Min(DurationSeconds,
            schedule.Max(entry => entry.SpawnSeconds + entry.TravelDurationSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        HordeClickerScheduleEntry[] eligibleCharacters = schedule
            .Where(entry => entry.SpawnSeconds + entry.TravelDurationSeconds > elapsedSeconds)
            .ToArray();
        playerStates.Add(
            session.ClientId,
            new PlayerState(
                eligibleCharacters,
                eligibleCharacters.Length * pointsPerCharacter));
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
            foreach (HordeClickerScheduleEntry entry in state.EligibleCharacters)
            {
                if (!state.ResolvedCharacterIds.Contains(entry.CharacterId) &&
                    elapsedSeconds >= entry.SpawnSeconds +
                        entry.TravelDurationSeconds + graceSeconds)
                {
                    state.ResolvedCharacterIds.Add(entry.CharacterId);
                    state.LastResolvedSeconds = Math.Max(state.LastResolvedSeconds,
                        entry.SpawnSeconds + entry.TravelDurationSeconds);
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
        ushort characterId = message.GetUShort();
        float clickedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        HordeClickerScheduleEntry? matchingEntry = state.EligibleCharacters
            .Where(entry => entry.CharacterId == characterId)
            .Select(entry => (HordeClickerScheduleEntry?)entry)
            .FirstOrDefault();
        bool accepted = matchingEntry.HasValue &&
            !state.ResolvedCharacterIds.Contains(characterId) &&
            IsValidTimestamp(clickedAtSeconds, receivedAtSeconds) &&
            IsOnscreen(matchingEntry.Value, clickedAtSeconds);

        if (accepted)
        {
            state.ResolvedCharacterIds.Add(characterId);
            state.ClickedCount++;
            state.LastResolvedSeconds = Math.Max(state.LastResolvedSeconds, clickedAtSeconds);
        }

        SendClickOutcome(session, characterId, accepted, server);
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
            foreach (HordeClickerScheduleEntry entry in state.EligibleCharacters)
            {
                if (state.ResolvedCharacterIds.Add(entry.CharacterId))
                    state.LastResolvedSeconds = Math.Max(state.LastResolvedSeconds,
                        Math.Min(DurationSeconds, entry.SpawnSeconds + entry.TravelDurationSeconds));
            }

            TryCompletePlayer(session, state, server);
            if (!session.SubmittedThisRound)
                session.MarkMissedRound();
        }
    }

    public override string Describe()
    {
        return $"seed={scheduleSeed} characters={characterCount} points={pointsPerCharacter}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddUInt(scheduleSeed);
        message.AddUShort((ushort)characterCount);
        message.AddInt(pointsPerCharacter);
        message.AddFloat(firstSpawnSeconds);
        message.AddFloat(lastSpawnSeconds);
        message.AddFloat(minimumTravelDurationSeconds);
        message.AddFloat(maximumTravelDurationSeconds);
        message.AddFloat(minimumYNormalized);
        message.AddFloat(maximumYNormalized);
        message.AddUShort((ushort)schedule.Count);
        foreach (HordeClickerScheduleEntry entry in schedule)
        {
            message.AddUShort(entry.CharacterId);
            message.AddFloat(entry.SpawnSeconds);
            message.AddFloat(entry.TravelDurationSeconds);
            message.AddFloat(entry.YNormalized);
        }
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

    private static bool IsOnscreen(
        HordeClickerScheduleEntry entry,
        float elapsedSeconds)
    {
        return elapsedSeconds >= entry.SpawnSeconds &&
            elapsedSeconds <= entry.SpawnSeconds + entry.TravelDurationSeconds;
    }

    private void SendClickOutcome(
        PlayerSession session,
        ushort characterId,
        bool accepted,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(ClickOutcomeEvent);
        outcome.AddUShort(characterId);
        outcome.AddBool(accepted);
        server.Send(outcome, session.ClientId);
    }

    private void TryCompletePlayer(
        PlayerSession session,
        PlayerState state,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound ||
            state.ResolvedCharacterIds.Count < state.EligibleCharacters.Count)
        {
            return;
        }

        CompleteSubmission(
            session,
            state.ClickedCount * pointsPerCharacter,
            server,
            state.MaximumScore,
            completedAtSeconds: state.EligibleCharacters.Count == 0
                ? DurationSeconds : state.LastResolvedSeconds);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
