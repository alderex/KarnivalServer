using Riptide;

public sealed class SliderTargetRound : MiniGameRoundBase
{
    public SliderTargetRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        float target01,
        float fingerPeriodSeconds,
        float targetHitRadiusNormalized)
        : base(
            roundId,
            MiniGameType.SliderTarget,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds)
    {
        Target01 = target01;
        FingerPeriodSeconds = fingerPeriodSeconds;
        TargetHitRadiusNormalized = targetHitRadiusNormalized;
    }

    public float Target01 { get; }
    public float FingerPeriodSeconds { get; }
    public float TargetHitRadiusNormalized { get; }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        float submittedValue01 = Clamp01(message.GetFloat());
        float delta = MathF.Abs(submittedValue01 - Target01);
        float hitRadius = Math.Clamp(TargetHitRadiusNormalized, 0.01f, 0.5f);
        bool hit = delta <= hitRadius;
        int score = hit
            ? Math.Clamp(
                (int)MathF.Round(100f * (1f - (delta / hitRadius))),
                1,
                100)
            : 0;

        if (!CompleteSubmission(session, score, server))
            return;

        RiptideConsoleLogger.Info(
            $"Round {RoundId}: {session.Username} submitted {submittedValue01:0.000}, " +
            $"delta={delta:0.000}, hit={hit}, score={score}.");
    }

    public override string Describe()
    {
        return $"target={Target01:0.000} hitRadius={TargetHitRadiusNormalized:0.000}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddFloat(Target01);
        message.AddFloat(FingerPeriodSeconds);
        message.AddFloat(TargetHitRadiusNormalized);
    }
}