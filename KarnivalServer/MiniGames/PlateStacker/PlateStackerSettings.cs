public sealed class PlateStackerSettings
{
    public float DurationSeconds { get; init; } = 10f;
    public int PlateCount { get; init; } = 10;
    public float FirstLandingSeconds { get; init; } = 1.5f;
    public float LastLandingSeconds { get; init; } = 9.25f;
    public float MinimumFallDurationSeconds { get; init; } = 0.9f;
    public float MaximumFallDurationSeconds { get; init; } = 1.4f;
    public float StackSpeedNormalizedPerSecond { get; init; } = 0.8f;
    public float PlateWidthNormalized { get; init; } = 0.12f;
    public float CollapseOffsetPlateWidths { get; init; } = 1.5f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
    public int MaximumInputSamples { get; init; } = 256;
    public int PointsPerPlate { get; init; } = 10;
}
