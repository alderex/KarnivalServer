public readonly record struct PlateStackerScheduleEntry(
    ushort PlateId,
    float XNormalized,
    float SpawnSeconds,
    float LandingSeconds,
    float FallDurationSeconds);
