public static class PlateStackerScheduleGenerator
{
    public static PlateStackerScheduleEntry[] Generate(
        uint seed,
        int plateCount,
        float firstLandingSeconds,
        float lastLandingSeconds,
        float minimumFallDurationSeconds,
        float maximumFallDurationSeconds,
        float plateWidthNormalized)
    {
        int count = Math.Clamp(plateCount, 1, ushort.MaxValue);
        float firstLanding = Math.Max(0.1f, firstLandingSeconds);
        float lastLanding = Math.Max(firstLanding, lastLandingSeconds);
        float maximumFall = Math.Clamp(
            maximumFallDurationSeconds,
            0.1f,
            firstLanding);
        float minimumFall = Math.Clamp(
            minimumFallDurationSeconds,
            0.1f,
            maximumFall);
        float halfWidth = Math.Clamp(
            plateWidthNormalized * 0.5f,
            0.005f,
            0.245f);
        Random random = new(unchecked((int)seed));
        PlateStackerScheduleEntry[] schedule =
            new PlateStackerScheduleEntry[count];

        for (int index = 0; index < count; index++)
        {
            float fraction = count == 1 ? 0f : index / (float)(count - 1);
            float landingSeconds = Lerp(
                firstLanding,
                lastLanding,
                fraction);
            float fallDurationSeconds = Lerp(
                minimumFall,
                maximumFall,
                (float)random.NextDouble());
            float xNormalized = Lerp(
                halfWidth,
                1f - halfWidth,
                (float)random.NextDouble());
            schedule[index] = new PlateStackerScheduleEntry(
                (ushort)(index + 1),
                xNormalized,
                Math.Max(0f, landingSeconds - fallDurationSeconds),
                landingSeconds,
                fallDurationSeconds);
        }

        return schedule;
    }

    private static float Lerp(float start, float end, float amount) =>
        start + ((end - start) * amount);
}
