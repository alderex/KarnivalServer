using Riptide;

public sealed class CupAndBallRound : MiniGameRoundBase
{
    private const byte SelectionOutcomeEvent = 1;
    private const byte CupCount = 3;

    private sealed class PlayerState
    {
        public PlayerState(bool eligible)
        {
            Eligible = eligible;
        }

        public bool Eligible { get; }
    }

    private readonly byte initialBallSlot;
    private readonly byte correctSlot;
    private readonly IReadOnlyList<CupAndBallSwap> swaps;
    private readonly float ballRevealSeconds;
    private readonly float cupDropSeconds;
    private readonly float preShufflePauseSeconds;
    private readonly float shuffleStepSeconds;
    private readonly float inputDurationSeconds;
    private readonly float outcomeRevealSeconds;
    private readonly int correctScore;
    private readonly CupAndBallSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public CupAndBallRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        byte initialBallSlot,
        byte correctSlot,
        IReadOnlyList<CupAndBallSwap> swaps,
        float ballRevealSeconds,
        float cupDropSeconds,
        float preShufflePauseSeconds,
        float shuffleStepSeconds,
        float inputDurationSeconds,
        float outcomeRevealSeconds,
        int correctScore,
        CupAndBallSettings settings)
        : base(
            roundId,
            MiniGameType.CupAndBall,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            correctScore)
    {
        this.initialBallSlot = initialBallSlot;
        this.correctSlot = correctSlot;
        this.swaps = swaps;
        this.ballRevealSeconds = ballRevealSeconds;
        this.cupDropSeconds = cupDropSeconds;
        this.preShufflePauseSeconds = preShufflePauseSeconds;
        this.shuffleStepSeconds = shuffleStepSeconds;
        this.inputDurationSeconds = inputDurationSeconds;
        this.outcomeRevealSeconds = outcomeRevealSeconds;
        this.correctScore = correctScore;
        this.settings = settings;
    }

    public float InputStartsAtSeconds => ballRevealSeconds +
        cupDropSeconds +
        preShufflePauseSeconds +
        (swaps.Count * shuffleStepSeconds);

    public float InputEndsAtSeconds => InputStartsAtSeconds + inputDurationSeconds;

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        playerStates.Add(
            session.ClientId,
            new PlayerState(GetElapsedSeconds(nowUtc) < ballRevealSeconds));
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte selectedSlot = message.GetByte();
        float selectedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        bool accepted = state.Eligible &&
            selectedSlot < CupCount &&
            IsValidTimestamp(selectedAtSeconds, receivedAtSeconds);
        bool correct = accepted && selectedSlot == correctSlot;

        SendSelectionOutcome(
            session,
            selectedSlot,
            accepted,
            correct,
            server);
        if (accepted)
        {
            CompleteSubmission(
                session,
                correct ? correctScore : 0,
                server,
                completedAtSeconds: selectedAtSeconds);
        }
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            if (playerStates[session.ClientId].Eligible)
                CompleteSubmission(session, 0, server);
            else
                session.MarkMissedRound();
        }
    }

    public override string Describe()
    {
        string swapDescription = string.Join(
            ',',
            swaps.Select(swap => $"{swap.FirstSlot}-{swap.SecondSlot}"));
        return $"ball={initialBallSlot} correct={correctSlot} swaps={swapDescription}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte(initialBallSlot);
        message.AddByte((byte)swaps.Count);
        foreach (CupAndBallSwap swap in swaps)
        {
            message.AddByte(swap.FirstSlot);
            message.AddByte(swap.SecondSlot);
        }

        message.AddFloat(ballRevealSeconds);
        message.AddFloat(cupDropSeconds);
        message.AddFloat(preShufflePauseSeconds);
        message.AddFloat(shuffleStepSeconds);
        message.AddFloat(inputDurationSeconds);
        message.AddFloat(outcomeRevealSeconds);
    }

    private void SendSelectionOutcome(
        PlayerSession session,
        byte selectedSlot,
        bool accepted,
        bool correct,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(SelectionOutcomeEvent);
        outcome.AddByte(selectedSlot);
        outcome.AddBool(accepted);
        outcome.AddBool(correct);
        outcome.AddByte(correctSlot);
        server.Send(outcome, session.ClientId);
    }

    private bool IsValidTimestamp(float selectedAtSeconds, float receivedAtSeconds)
    {
        return !float.IsNaN(selectedAtSeconds) &&
            selectedAtSeconds >= InputStartsAtSeconds &&
            selectedAtSeconds <= InputEndsAtSeconds &&
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
