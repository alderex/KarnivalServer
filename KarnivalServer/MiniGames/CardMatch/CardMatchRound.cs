using Riptide;

public sealed class CardMatchRound : MiniGameRoundBase
{
    private const byte SelectionOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public PlayerState(int cardCount)
        {
            MatchedCards = new bool[cardCount];
        }

        public bool[] MatchedCards { get; }
        public byte? FirstCardId { get; set; }
        public int MatchedPairCount { get; set; }
        public float LockedUntilSeconds { get; set; }
    }

    private readonly IReadOnlyList<CardMatchShape> cards;
    private readonly int pairCount;
    private readonly int pointsPerPair;
    private readonly float mismatchRevealSeconds;
    private readonly CardMatchSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public CardMatchRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        IReadOnlyList<CardMatchShape> cards,
        int pairCount,
        int pointsPerPair,
        float mismatchRevealSeconds,
        CardMatchSettings settings)
        : base(
            roundId,
            MiniGameType.CardMatch,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            pairCount * pointsPerPair)
    {
        this.cards = cards;
        this.pairCount = pairCount;
        this.pointsPerPair = pointsPerPair;
        this.mismatchRevealSeconds = mismatchRevealSeconds;
        this.settings = settings;
    }

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (!playerStates.ContainsKey(session.ClientId))
            playerStates.Add(session.ClientId, new PlayerState(cards.Count));
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte cardId = message.GetByte();
        float selectedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);

        bool accepted = cardId < cards.Count &&
            !state.MatchedCards[cardId] &&
            state.FirstCardId != cardId &&
            selectedAtSeconds >= state.LockedUntilSeconds &&
            IsValidTimestamp(selectedAtSeconds, receivedAtSeconds);
        if (!accepted)
        {
            SendSelectionOutcome(
                session,
                cardId,
                false,
                false,
                state.FirstCardId ?? byte.MaxValue,
                false,
                server);
            return;
        }

        if (!state.FirstCardId.HasValue)
        {
            state.FirstCardId = cardId;
            SendSelectionOutcome(
                session,
                cardId,
                true,
                false,
                cardId,
                false,
                server);
            return;
        }

        byte firstCardId = state.FirstCardId.Value;
        state.FirstCardId = null;
        bool matched = cards[firstCardId] == cards[cardId];
        if (matched)
        {
            state.MatchedCards[firstCardId] = true;
            state.MatchedCards[cardId] = true;
            state.MatchedPairCount++;
        }
        else
        {
            state.LockedUntilSeconds =
                selectedAtSeconds + mismatchRevealSeconds;
        }

        SendSelectionOutcome(
            session,
            cardId,
            true,
            true,
            firstCardId,
            matched,
            server);
        if (state.MatchedPairCount >= pairCount)
            CompletePlayer(session, state, server, selectedAtSeconds);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            CompletePlayer(
                session,
                playerStates[session.ClientId],
                server,
                DurationSeconds);
        }
    }

    public override string Describe()
    {
        return $"pairs={pairCount} cards={string.Join(',', cards)}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)cards.Count);
        foreach (CardMatchShape shape in cards)
            message.AddByte((byte)shape);
        message.AddFloat(mismatchRevealSeconds);
    }

    private void SendSelectionOutcome(
        PlayerSession session,
        byte selectedCardId,
        bool accepted,
        bool pairResolved,
        byte firstCardId,
        bool matched,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(SelectionOutcomeEvent);
        outcome.AddByte(selectedCardId);
        outcome.AddBool(accepted);
        outcome.AddBool(pairResolved);
        outcome.AddByte(firstCardId);
        outcome.AddBool(matched);
        server.Send(outcome, session.ClientId);
    }

    private void CompletePlayer(
        PlayerSession session,
        PlayerState state,
        Riptide.Server server,
        float completedAtSeconds)
    {
        CompleteSubmission(
            session,
            state.MatchedPairCount * pointsPerPair,
            server,
            completedAtSeconds: completedAtSeconds);
    }

    private bool IsValidTimestamp(float selectedAtSeconds, float receivedAtSeconds)
    {
        return !float.IsNaN(selectedAtSeconds) &&
            selectedAtSeconds >= 0f &&
            selectedAtSeconds <= DurationSeconds &&
            selectedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - selectedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
