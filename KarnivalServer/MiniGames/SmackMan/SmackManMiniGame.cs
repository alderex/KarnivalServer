public sealed class SmackManMiniGame : IMiniGame
{
    private readonly SmackManSettings settings;

    public SmackManMiniGame(SmackManSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.SmackMan;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float durationSeconds = Math.Max(1f, settings.DurationSeconds);
        int headsPerGroup = Math.Clamp(
            settings.HeadsPerGroup,
            1,
            SmackManScheduleGenerator.HoleCount);
        int groupCount = Math.Clamp(settings.GroupCount, 1, ushort.MaxValue / headsPerGroup);
        int pointsPerHit = Math.Max(0, settings.PointsPerHit);
        float firstGroupSeconds = Math.Max(0f, settings.FirstGroupSeconds);
        float groupIntervalSeconds = Math.Max(0.0001f, settings.GroupIntervalSeconds);
        float riseDurationSeconds = Math.Max(0.0001f, settings.RiseDurationSeconds);
        float holdDurationSeconds = Math.Max(0.0001f, settings.HoldDurationSeconds);
        float retractDurationSeconds = Math.Max(0.0001f, settings.RetractDurationSeconds);
        float activeDurationSeconds = riseDurationSeconds + holdDurationSeconds + retractDurationSeconds;
        uint scheduleSeed = (uint)context.Random.NextInt64(1, (long)uint.MaxValue + 1L);
        SmackManScheduleEntry[] schedule = SmackManScheduleGenerator.Generate(
            scheduleSeed,
            groupCount,
            headsPerGroup,
            firstGroupSeconds,
            groupIntervalSeconds,
            activeDurationSeconds);

        return new SmackManRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            scheduleSeed,
            pointsPerHit,
            riseDurationSeconds,
            holdDurationSeconds,
            retractDurationSeconds,
            schedule,
            settings);
    }
}
