public sealed class SmackManSettings
{
    public float DurationSeconds { get; init; } = 16f;
    public int GroupCount { get; init; } = 4;
    public int HeadsPerGroup { get; init; } = 5;
    public int PointsPerHit { get; init; } = 5;
    public float FirstGroupSeconds { get; init; } = 1f;
    public float GroupIntervalSeconds { get; init; } = 4f;
    public float RiseDurationSeconds { get; init; } = 0.12f;
    public float HoldDurationSeconds { get; init; } = 2f;
    public float RetractDurationSeconds { get; init; } = 0.5f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
