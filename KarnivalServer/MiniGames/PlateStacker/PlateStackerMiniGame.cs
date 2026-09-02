public sealed class PlateStackerMiniGame : IMiniGame
{
    private readonly PlateStackerSettings settings;

    public PlateStackerMiniGame(PlateStackerSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.PlateStacker;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float durationSeconds = Math.Max(1f, settings.DurationSeconds);
        int plateCount = Math.Clamp(settings.PlateCount, 1, ushort.MaxValue);
        float firstLandingSeconds = Math.Clamp(
            settings.FirstLandingSeconds,
            0.1f,
            durationSeconds);
        float inputGraceSeconds = Math.Max(0f, settings.InputGraceSeconds);
        float lastLandingSeconds = Math.Clamp(
            settings.LastLandingSeconds,
            firstLandingSeconds,
            Math.Max(
                firstLandingSeconds,
                durationSeconds - inputGraceSeconds));
        float maximumFallDurationSeconds = Math.Clamp(
            settings.MaximumFallDurationSeconds,
            0.1f,
            firstLandingSeconds);
        float minimumFallDurationSeconds = Math.Clamp(
            settings.MinimumFallDurationSeconds,
            0.1f,
            maximumFallDurationSeconds);
        float plateWidthNormalized = Math.Clamp(
            settings.PlateWidthNormalized,
            0.01f,
            0.49f);
        uint scheduleSeed = (uint)context.Random.NextInt64(
            1,
            (long)uint.MaxValue + 1L);
        PlateStackerScheduleEntry[] schedule =
            PlateStackerScheduleGenerator.Generate(
                scheduleSeed,
                plateCount,
                firstLandingSeconds,
                lastLandingSeconds,
                minimumFallDurationSeconds,
                maximumFallDurationSeconds,
                plateWidthNormalized);

        return new PlateStackerRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            scheduleSeed,
            firstLandingSeconds,
            lastLandingSeconds,
            minimumFallDurationSeconds,
            maximumFallDurationSeconds,
            schedule,
            settings);
    }
}
