public sealed class SliderTargetMiniGame : IMiniGame
{
    private readonly SliderTargetSettings settings;

    public SliderTargetMiniGame(SliderTargetSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.SliderTarget;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        return new SliderTargetRound(
            context.RoundId,
            context.StartsUtc,
            context.DurationSeconds,
            context.ResultsDurationSeconds,
            context.Random.NextSingle(),
            Math.Max(0.25f, settings.FingerPeriodSeconds),
            Math.Clamp(settings.TargetHitRadiusNormalized, 0.01f, 0.5f));
    }
}