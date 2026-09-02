using Riptide;

public sealed class PotatoFaceRound : MiniGameRoundBase
{
    private readonly PotatoFaceTarget target;
    private readonly PotatoFaceSettings settings;

    public PotatoFaceRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        PotatoFaceTarget target,
        PotatoFaceSettings settings)
        : base(
            roundId,
            MiniGameType.PotatoFace,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            GetMaximumScore(settings))
    {
        this.target = target;
        this.settings = settings;
    }

    public PotatoFaceTarget Target => target;

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        PotatoFacePartVariant leftEyeVariant = (PotatoFacePartVariant)message.GetByte();
        PotatoFacePosition leftEyePosition = new(message.GetFloat(), message.GetFloat());
        PotatoFacePartVariant rightEyeVariant = (PotatoFacePartVariant)message.GetByte();
        PotatoFacePosition rightEyePosition = new(message.GetFloat(), message.GetFloat());
        PotatoFacePartVariant mouthVariant = (PotatoFacePartVariant)message.GetByte();
        PotatoFacePosition mouthPosition = new(message.GetFloat(), message.GetFloat());
        PotatoFacePartVariant noseVariant = (PotatoFacePartVariant)message.GetByte();
        PotatoFacePosition nosePosition = new(message.GetFloat(), message.GetFloat());
        float submittedAtSeconds = message.GetFloat();
        float receivedAtSeconds = GetElapsedSeconds(DateTime.UtcNow);
        if (!IsSubmittedVariant(leftEyeVariant) ||
            !IsSubmittedVariant(rightEyeVariant) ||
            !IsSubmittedVariant(mouthVariant) ||
            !IsSubmittedVariant(noseVariant) ||
            !IsPosition(leftEyePosition) ||
            !IsPosition(rightEyePosition) ||
            !IsPosition(mouthPosition) ||
            !IsPosition(nosePosition) ||
            !IsValidTimestamp(submittedAtSeconds, receivedAtSeconds))
        {
            return;
        }

        int score = CalculateScore(
            target,
            leftEyeVariant,
            leftEyePosition,
            rightEyeVariant,
            rightEyePosition,
            mouthVariant,
            mouthPosition,
            noseVariant,
            nosePosition,
            settings);
        if (!CompleteSubmission(
            session,
            score,
            server,
            completedAtSeconds: submittedAtSeconds))
        {
            return;
        }

        RiptideConsoleLogger.Info(
            $"Round {RoundId}: {session.Username} locked PotatoFace score={score}.");
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
        return $"eyes={target.EyesVariant} mouth={target.MouthVariant} " +
            $"nose={target.NoseVariant}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)target.EyesVariant);
        message.AddByte((byte)target.MouthVariant);
        message.AddByte((byte)target.NoseVariant);
    }

    public static int CalculateScore(
        PotatoFaceTarget target,
        PotatoFacePartVariant leftEyeVariant,
        PotatoFacePosition leftEyePosition,
        PotatoFacePartVariant rightEyeVariant,
        PotatoFacePosition rightEyePosition,
        PotatoFacePartVariant mouthVariant,
        PotatoFacePosition mouthPosition,
        PotatoFacePartVariant noseVariant,
        PotatoFacePosition nosePosition,
        PotatoFaceSettings settings)
    {
        float tolerance = Math.Max(0.01f, settings.PositionToleranceNormalized);
        int leftEyePoints = Math.Max(0, settings.EyesPoints) / 2;
        int rightEyePoints = Math.Max(0, settings.EyesPoints) - leftEyePoints;
        bool mismatchedEyes =
            leftEyeVariant != PotatoFacePartVariant.None &&
            rightEyeVariant != PotatoFacePartVariant.None &&
            leftEyeVariant != rightEyeVariant;
        int eyeScore = mismatchedEyes
            ? 0
            : ScorePart(
                    leftEyeVariant,
                    target.EyesVariant,
                    leftEyePosition,
                    leftEyePoints,
                    tolerance) +
                ScorePart(
                    rightEyeVariant,
                    target.EyesVariant,
                    rightEyePosition,
                    rightEyePoints,
                    tolerance);
        return eyeScore +
            ScorePart(
                mouthVariant,
                target.MouthVariant,
                mouthPosition,
                settings.MouthPoints,
                tolerance) +
            ScorePart(
                noseVariant,
                target.NoseVariant,
                nosePosition,
                settings.NosePoints,
                tolerance);
    }

    private static int ScorePart(
        PotatoFacePartVariant submittedVariant,
        PotatoFacePartVariant targetVariant,
        PotatoFacePosition submittedOffset,
        int maximumPoints,
        float tolerance)
    {
        if (submittedVariant != targetVariant || maximumPoints <= 0)
            return 0;

        float distance = MathF.Sqrt(
            (submittedOffset.X * submittedOffset.X) +
            (submittedOffset.Y * submittedOffset.Y));
        float accuracy = Math.Clamp(1f - (distance / tolerance), 0f, 1f);
        return (int)MathF.Round(maximumPoints * accuracy);
    }

    private bool IsValidTimestamp(float submittedAtSeconds, float receivedAtSeconds)
    {
        return float.IsFinite(submittedAtSeconds) &&
            submittedAtSeconds >= 0f &&
            submittedAtSeconds <= DurationSeconds &&
            submittedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - submittedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private static bool IsSubmittedVariant(PotatoFacePartVariant variant) =>
        variant is PotatoFacePartVariant.None or
            PotatoFacePartVariant.One or
            PotatoFacePartVariant.Two;

    private static bool IsPosition(PotatoFacePosition position) =>
        float.IsFinite(position.X) &&
        float.IsFinite(position.Y) &&
            position.X is >= -1f and <= 1f &&
            position.Y is >= -1f and <= 1f;

    private static int GetMaximumScore(PotatoFaceSettings settings)
    {
        long maximum = (long)Math.Max(0, settings.EyesPoints) +
            Math.Max(0, settings.MouthPoints) +
            Math.Max(0, settings.NosePoints);
        return (int)Math.Min(int.MaxValue, maximum);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
