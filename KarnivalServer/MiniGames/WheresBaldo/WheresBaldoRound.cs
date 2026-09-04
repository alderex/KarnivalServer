using Riptide;

public sealed class WheresBaldoRound : MiniGameRoundBase
{
    private const byte SelectionOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public HashSet<byte> WrongCharacterIds { get; } = new();
    }

    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private readonly WheresBaldoSettings settings;

    public WheresBaldoRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        uint crowdSeed,
        byte baldoCharacterId,
        int correctScore,
        int wrongSelectionPenalty,
        WheresBaldoSettings settings)
        : base(
            roundId,
            MiniGameType.WheresBaldo,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            correctScore)
    {
        if (baldoCharacterId >= WheresBaldoMiniGame.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(baldoCharacterId));

        CrowdSeed = crowdSeed;
        BaldoCharacterId = baldoCharacterId;
        CorrectScore = Math.Max(0, correctScore);
        WrongSelectionPenalty = Math.Max(0, wrongSelectionPenalty);
        this.settings = settings;
    }

    public uint CrowdSeed { get; }
    public byte BaldoCharacterId { get; }
    public int CorrectScore { get; }
    public int WrongSelectionPenalty { get; }

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (!playerStates.ContainsKey(session.ClientId))
            playerStates.Add(session.ClientId, new PlayerState());
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte characterId = message.GetByte();
        float clickedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        bool valid = !session.SubmittedThisRound &&
            characterId < WheresBaldoMiniGame.CharacterCount &&
            IsValidTimestamp(clickedAtSeconds, receivedAtSeconds);
        bool correct = valid && characterId == BaldoCharacterId;
        bool accepted = correct ||
            (valid && state.WrongCharacterIds.Add(characterId));

        SendSelectionOutcome(
            session,
            characterId,
            accepted,
            correct,
            state.WrongCharacterIds.Count,
            server);

        if (!correct)
            return;

        int baseScore = Math.Max(
            0,
            CorrectScore - (state.WrongCharacterIds.Count * WrongSelectionPenalty));
        CompleteSubmission(
            session,
            baseScore,
            server,
            completedAtSeconds: clickedAtSeconds);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
            CompleteSubmission(session, 0, server);
    }

    public override string Describe()
    {
        return $"seed={CrowdSeed} baldo={BaldoCharacterId} " +
            $"score={CorrectScore} penalty={WrongSelectionPenalty}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddUInt(CrowdSeed);
        message.AddByte(BaldoCharacterId);
    }

    private bool IsValidTimestamp(float clickedAtSeconds, float receivedAtSeconds)
    {
        return float.IsFinite(clickedAtSeconds) &&
            clickedAtSeconds >= 0f &&
            clickedAtSeconds <= DurationSeconds &&
            clickedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - clickedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private void SendSelectionOutcome(
        PlayerSession session,
        byte characterId,
        bool accepted,
        bool correct,
        int wrongSelectionCount,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(SelectionOutcomeEvent);
        outcome.AddByte(characterId);
        outcome.AddBool(accepted);
        outcome.AddBool(correct);
        outcome.AddByte((byte)Math.Clamp(
            wrongSelectionCount,
            0,
            byte.MaxValue));
        server.Send(outcome, session.ClientId);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
