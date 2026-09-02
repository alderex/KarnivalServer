public sealed class PotatoFaceSettings
{
    public float DurationSeconds { get; init; } = 20f;
    public float PositionToleranceNormalized { get; init; } = 0.35f;
    public int EyesPoints { get; init; } = 40;
    public int MouthPoints { get; init; } = 30;
    public int NosePoints { get; init; } = 30;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
