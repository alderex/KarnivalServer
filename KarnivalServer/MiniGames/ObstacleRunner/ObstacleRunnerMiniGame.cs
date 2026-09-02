public sealed class ObstacleRunnerMiniGame : IMiniGame
{
    private readonly ObstacleRunnerSettings settings;

    public ObstacleRunnerMiniGame(ObstacleRunnerSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.ObstacleRunner;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float durationSeconds = context.DurationSeconds +
            Math.Max(0f, settings.AdditionalDurationSeconds);
        int obstacleCount = Math.Max(1, settings.ObstacleCount);
        ObstacleHeight[] heights = CreateBalancedHeights(obstacleCount, context.Random);
        ObstacleScheduleEntry[] schedule = new ObstacleScheduleEntry[obstacleCount];
        float first = Math.Clamp(settings.FirstCollisionNormalized, 0.05f, 0.8f);
        float last = Math.Clamp(settings.LastCollisionNormalized, first, 0.95f);

        for (int index = 0; index < obstacleCount; index++)
        {
            float normalized = obstacleCount == 1
                ? first
                : first + ((last - first) * index / (obstacleCount - 1));
            schedule[index] = new ObstacleScheduleEntry(
                (ushort)(index + 1),
                heights[index],
                durationSeconds * normalized);
        }

        return new ObstacleRunnerRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            schedule,
            settings);
    }

    private static ObstacleHeight[] CreateBalancedHeights(int count, Random random)
    {
        ObstacleHeight[] heights = new ObstacleHeight[count];
        for (int index = 0; index < count; index++)
            heights[index] = (ObstacleHeight)(index % 3);

        for (int index = heights.Length - 1; index > 0; index--)
        {
            int otherIndex = random.Next(index + 1);
            (heights[index], heights[otherIndex]) = (heights[otherIndex], heights[index]);
        }

        return heights;
    }
}
