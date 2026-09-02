public sealed class StopGoMiniGame : IMiniGame
{
    private readonly StopGoSettings settings;

    public StopGoMiniGame(StopGoSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.StopGo;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float duration = Math.Max(1f, settings.DurationSeconds);
        uint scheduleSeed = (uint)context.Random.NextInt64(1, (long)uint.MaxValue + 1L);
        IReadOnlyList<StopGoLightInterval> schedule = StopGoScheduleGenerator.Generate(
            scheduleSeed,
            duration,
            settings.MinimumGreenSeconds,
            settings.MaximumGreenSeconds,
            settings.MinimumRedSeconds,
            settings.MaximumRedSeconds);
        return new StopGoRound(
            context.RoundId,
            context.StartsUtc,
            duration,
            context.ResultsDurationSeconds,
            scheduleSeed,
            schedule,
            settings);
    }
}
