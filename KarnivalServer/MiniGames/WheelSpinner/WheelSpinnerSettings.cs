public sealed class WheelSpinnerSettings
{
    public float DurationSeconds { get; init; } = 10f;
    public float AngularSpeedDegreesPerSecond { get; init; } = 240f;
    public WheelSpinnerSliceSettings[] Slices { get; init; } =
    {
        new() { Points = 10, ArcDegrees = 48f },
        new() { Points = 25, ArcDegrees = 36f },
        new() { Points = 10, ArcDegrees = 48f },
        new() { Points = 50, ArcDegrees = 24f },
        new() { Points = 10, ArcDegrees = 48f },
        new() { Points = 25, ArcDegrees = 36f },
        new() { Points = 10, ArcDegrees = 48f },
        new() { Points = 50, ArcDegrees = 24f },
        new() { Points = 25, ArcDegrees = 36f },
        new() { Points = 100, ArcDegrees = 12f },
    };
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
