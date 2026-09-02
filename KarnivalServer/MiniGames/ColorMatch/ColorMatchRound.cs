using Riptide;

public sealed class ColorMatchRound : MiniGameRoundBase
{
    private const float MaximumRgbDistance = 441.67295593f;

    private readonly ColorMatchSettings settings;

    public ColorMatchRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        byte targetRed,
        byte targetGreen,
        byte targetBlue,
        byte initialRed,
        byte initialGreen,
        byte initialBlue,
        ColorMatchSettings settings)
        : base(
            roundId,
            MiniGameType.ColorMatch,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds)
    {
        TargetRed = targetRed;
        TargetGreen = targetGreen;
        TargetBlue = targetBlue;
        InitialRed = initialRed;
        InitialGreen = initialGreen;
        InitialBlue = initialBlue;
        this.settings = settings;
    }

    public byte TargetRed { get; }
    public byte TargetGreen { get; }
    public byte TargetBlue { get; }
    public byte InitialRed { get; }
    public byte InitialGreen { get; }
    public byte InitialBlue { get; }
    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte submittedRed = message.GetByte();
        byte submittedGreen = message.GetByte();
        byte submittedBlue = message.GetByte();
        float submittedAtSeconds = message.GetFloat();
        float receivedAtSeconds = GetElapsedSeconds(DateTime.UtcNow);
        if (!IsValidTimestamp(submittedAtSeconds, receivedAtSeconds))
            return;

        int score = CalculateScore(
            TargetRed,
            TargetGreen,
            TargetBlue,
            submittedRed,
            submittedGreen,
            submittedBlue);
        if (!CompleteSubmission(
            session,
            score,
            server,
            completedAtSeconds: submittedAtSeconds))
            return;

        RiptideConsoleLogger.Info(
            $"Round {RoundId}: {session.Username} matched " +
            $"({submittedRed}, {submittedGreen}, {submittedBlue}) against " +
            $"({TargetRed}, {TargetGreen}, {TargetBlue}), score={score}.");
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
        return $"target=({TargetRed},{TargetGreen},{TargetBlue})";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte(TargetRed);
        message.AddByte(TargetGreen);
        message.AddByte(TargetBlue);
        message.AddByte(InitialRed);
        message.AddByte(InitialGreen);
        message.AddByte(InitialBlue);
    }

    private bool IsValidTimestamp(float submittedAtSeconds, float receivedAtSeconds)
    {
        return !float.IsNaN(submittedAtSeconds) &&
            submittedAtSeconds >= 0f &&
            submittedAtSeconds <= DurationSeconds &&
            submittedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - submittedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private static int CalculateScore(
        byte targetRed,
        byte targetGreen,
        byte targetBlue,
        byte submittedRed,
        byte submittedGreen,
        byte submittedBlue)
    {
        float redDifference = submittedRed - targetRed;
        float greenDifference = submittedGreen - targetGreen;
        float blueDifference = submittedBlue - targetBlue;
        float distance = MathF.Sqrt(
            (redDifference * redDifference) +
            (greenDifference * greenDifference) +
            (blueDifference * blueDifference));
        return Math.Clamp(
            (int)MathF.Round(100f * (1f - (distance / MaximumRgbDistance))),
            0,
            100);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
