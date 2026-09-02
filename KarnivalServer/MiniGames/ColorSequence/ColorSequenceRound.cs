using Riptide;

public sealed class ColorSequenceRound : MiniGameRoundBase
{
    private sealed class PlayerState
    {
        public PlayerState(bool eligible)
        {
            Eligible = eligible;
        }

        public bool Eligible { get; }
    }

    private readonly int ballCount;
    private readonly IReadOnlyList<byte> sequence;
    private readonly float playbackLeadInSeconds;
    private readonly float playbackLitSeconds;
    private readonly float playbackGapSeconds;
    private readonly float inputDurationSeconds;
    private readonly int pointsPerCorrectPosition;
    private readonly int perfectSequenceBonus;
    private readonly ColorSequenceSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public ColorSequenceRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        int ballCount,
        IReadOnlyList<byte> sequence,
        float playbackLeadInSeconds,
        float playbackLitSeconds,
        float playbackGapSeconds,
        float inputDurationSeconds,
        int pointsPerCorrectPosition,
        int perfectSequenceBonus,
        ColorSequenceSettings settings)
        : base(
            roundId,
            MiniGameType.ColorSequence,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            (sequence.Count * pointsPerCorrectPosition) + perfectSequenceBonus)
    {
        this.ballCount = ballCount;
        this.sequence = sequence;
        this.playbackLeadInSeconds = playbackLeadInSeconds;
        this.playbackLitSeconds = playbackLitSeconds;
        this.playbackGapSeconds = playbackGapSeconds;
        this.inputDurationSeconds = inputDurationSeconds;
        this.pointsPerCorrectPosition = pointsPerCorrectPosition;
        this.perfectSequenceBonus = perfectSequenceBonus;
        this.settings = settings;
    }

    public float InputStartsAtSeconds => playbackLeadInSeconds +
        (sequence.Count * (playbackLitSeconds + playbackGapSeconds));

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        playerStates.Add(
            session.ClientId,
            new PlayerState(GetElapsedSeconds(nowUtc) < playbackLeadInSeconds));
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte submittedCount = message.GetByte();
        byte[] submittedSequence = new byte[submittedCount];
        bool validBallIds = true;
        for (int index = 0; index < submittedSequence.Length; index++)
        {
            submittedSequence[index] = message.GetByte();
            validBallIds &= submittedSequence[index] < ballCount;
        }

        float submittedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        if (!state.Eligible ||
            submittedCount > sequence.Count ||
            !validBallIds ||
            !IsValidTimestamp(submittedAtSeconds, receivedAtSeconds))
        {
            return;
        }

        int correctPositionCount = 0;
        for (int index = 0; index < submittedSequence.Length; index++)
        {
            if (submittedSequence[index] == sequence[index])
                correctPositionCount++;
        }

        bool perfect = submittedSequence.Length == sequence.Count &&
            correctPositionCount == sequence.Count;
        int score = (correctPositionCount * pointsPerCorrectPosition) +
            (perfect ? perfectSequenceBonus : 0);
        CompleteSubmission(
            session,
            score,
            server,
            completedAtSeconds: submittedAtSeconds);
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
        return $"balls={ballCount} sequence={string.Join(',', sequence)}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)ballCount);
        message.AddByte((byte)sequence.Count);
        foreach (byte ballId in sequence)
            message.AddByte(ballId);
        message.AddFloat(playbackLeadInSeconds);
        message.AddFloat(playbackLitSeconds);
        message.AddFloat(playbackGapSeconds);
        message.AddFloat(inputDurationSeconds);
    }

    private bool IsValidTimestamp(float submittedAtSeconds, float receivedAtSeconds)
    {
        return !float.IsNaN(submittedAtSeconds) &&
            submittedAtSeconds >= InputStartsAtSeconds &&
            submittedAtSeconds <= DurationSeconds &&
            submittedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - submittedAtSeconds <=
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
