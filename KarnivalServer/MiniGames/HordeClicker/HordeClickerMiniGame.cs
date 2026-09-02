public sealed class HordeClickerMiniGame : IMiniGame
{
    private readonly HordeClickerSettings settings;

    public HordeClickerMiniGame(HordeClickerSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.HordeClicker;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float durationSeconds = Math.Max(1f, settings.DurationSeconds);
        int characterCount = Math.Clamp(settings.CharacterCount, 1, 25);
        int pointsPerCharacter = Math.Max(0, settings.PointsPerCharacter);
        float firstSpawnSeconds = Math.Clamp(settings.FirstSpawnSeconds, 0f, durationSeconds);
        float minimumTravelDurationSeconds = Math.Max(
            0.1f,
            settings.MinimumTravelDurationSeconds);
        float maximumTravelDurationSeconds = Math.Max(
            minimumTravelDurationSeconds,
            settings.MaximumTravelDurationSeconds);
        float lastSpawnSeconds = Math.Clamp(
            settings.LastSpawnSeconds,
            firstSpawnSeconds,
            Math.Max(firstSpawnSeconds, durationSeconds - minimumTravelDurationSeconds));
        float minimumYNormalized = Math.Clamp(settings.MinimumYNormalized, 0f, 1f);
        float maximumYNormalized = Math.Clamp(
            settings.MaximumYNormalized,
            minimumYNormalized,
            1f);
        uint scheduleSeed = (uint)context.Random.NextInt64(1, (long)uint.MaxValue + 1L);
        HordeClickerScheduleEntry[] schedule = HordeClickerScheduleGenerator.Generate(
            scheduleSeed,
            characterCount,
            firstSpawnSeconds,
            lastSpawnSeconds,
            minimumTravelDurationSeconds,
            maximumTravelDurationSeconds,
            minimumYNormalized,
            maximumYNormalized);

        return new HordeClickerRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            scheduleSeed,
            characterCount,
            pointsPerCharacter,
            firstSpawnSeconds,
            lastSpawnSeconds,
            minimumTravelDurationSeconds,
            maximumTravelDurationSeconds,
            minimumYNormalized,
            maximumYNormalized,
            schedule,
            settings);
    }
}
