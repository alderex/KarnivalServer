public sealed class FallingObjectCatcherMiniGame : IMiniGame
{
    private readonly FallingObjectCatcherSettings settings;

    public FallingObjectCatcherMiniGame(FallingObjectCatcherSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.FallingObjectCatcher;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float durationSeconds = Math.Max(1f, settings.DurationSeconds);
        float inputGraceSeconds = Math.Max(0f, settings.InputGraceSeconds);
        float firstCatchSeconds = Math.Clamp(
            settings.FirstCatchSeconds,
            0.1f,
            durationSeconds);
        float maximumFallDurationSeconds = Math.Clamp(
            settings.MaximumFallDurationSeconds,
            0.1f,
            firstCatchSeconds);
        float minimumFallDurationSeconds = Math.Clamp(
            settings.MinimumFallDurationSeconds,
            0.1f,
            maximumFallDurationSeconds);
        float catchWindowFraction = Math.Clamp(
            settings.CatchWindowFallDurationFraction,
            0.05f,
            1f);
        float maximumCatchWindowSeconds =
            maximumFallDurationSeconds * catchWindowFraction;
        float lastCatchSeconds = Math.Clamp(
            settings.LastCatchSeconds,
            firstCatchSeconds,
            Math.Max(
                firstCatchSeconds,
                durationSeconds - inputGraceSeconds - maximumCatchWindowSeconds));
        int targetObjectCount = Math.Clamp(settings.TargetObjectCount, 1, 10);
        int distractorCountPerShape = Math.Clamp(settings.DistractorCountPerShape, 0, 4);
        FallingObjectShape targetShape = (FallingObjectShape)context.Random.Next(3);
        uint scheduleSeed = (uint)context.Random.NextInt64(1, (long)uint.MaxValue + 1L);
        FallingObjectScheduleEntry[] schedule = FallingObjectScheduleGenerator.Generate(
            targetShape,
            scheduleSeed,
            targetObjectCount,
            distractorCountPerShape,
            firstCatchSeconds,
            lastCatchSeconds,
            minimumFallDurationSeconds,
            maximumFallDurationSeconds);

        return new FallingObjectCatcherRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            targetShape,
            scheduleSeed,
            targetObjectCount,
            distractorCountPerShape,
            firstCatchSeconds,
            lastCatchSeconds,
            minimumFallDurationSeconds,
            maximumFallDurationSeconds,
            schedule,
            settings);
    }
}