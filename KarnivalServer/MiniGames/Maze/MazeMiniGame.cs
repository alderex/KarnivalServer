public sealed class MazeMiniGame : IMiniGame
{
    private readonly MazeSettings settings;

    public MazeMiniGame(MazeSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.Maze;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        uint seed = (uint)context.Random.NextInt64(
            1,
            (long)uint.MaxValue + 1L);
        MazeLayout layout = MazeGenerator.Generate(seed, settings.GridSize);
        return new MazeRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            layout,
            settings);
    }
}
