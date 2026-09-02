public sealed class ObstacleRunnerSettings
{
    public float AdditionalDurationSeconds { get; init; } = 2f;
    public int ObstacleCount { get; init; } = 12;
    public float FirstCollisionNormalized { get; init; } = 0.3f;
    public float LastCollisionNormalized { get; init; } = 0.9f;
    public float ObstacleTravelSeconds { get; init; } = 1f;
    public float JumpDurationSeconds { get; init; } = 0.45f;
    public float SlideDurationSeconds { get; init; } = 0.45f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
