public sealed class WheelSpinnerSliceSettings
{
    public int Points { get; init; }
    public float ArcDegrees { get; init; }
}

public readonly record struct WheelSpinnerSlice(
    int Points,
    float ArcDegrees);
