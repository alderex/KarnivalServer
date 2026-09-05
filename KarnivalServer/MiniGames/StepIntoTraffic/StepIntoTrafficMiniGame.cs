public sealed class StepIntoTrafficMiniGame : IMiniGame
{
    private readonly StepIntoTrafficSettings settings;

    public StepIntoTrafficMiniGame(StepIntoTrafficSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.StepIntoTraffic;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        StepIntoTrafficLane[] lanes = StepIntoTrafficSchedule.Generate(
            settings.LaneCount,
            context.Random,
            settings);
        return new StepIntoTrafficRound(
            context.RoundId,
            context.StartsUtc,
            settings.DurationSeconds,
            context.ResultsDurationSeconds,
            lanes,
            settings);
    }
}
