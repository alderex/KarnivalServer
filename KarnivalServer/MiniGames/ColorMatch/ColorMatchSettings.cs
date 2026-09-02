public sealed class ColorMatchSettings
{
    public float DurationSeconds { get; init; } = 15f;
    public byte InitialRed { get; init; } = 128;
    public byte InitialGreen { get; init; } = 128;
    public byte InitialBlue { get; init; } = 128;
    public byte MinimumTargetChannel { get; init; } = 32;
    public byte MaximumTargetChannel { get; init; } = 223;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
