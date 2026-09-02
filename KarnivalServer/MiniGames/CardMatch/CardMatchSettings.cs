public sealed class CardMatchSettings
{
    public float DurationSeconds { get; init; } = 30f;
    public int PairCount { get; init; } = 6;
    public int AvailableShapeCount { get; init; } = 6;
    public int PointsPerPair { get; init; } = 20;
    public float MismatchRevealSeconds { get; init; } = 0.75f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
