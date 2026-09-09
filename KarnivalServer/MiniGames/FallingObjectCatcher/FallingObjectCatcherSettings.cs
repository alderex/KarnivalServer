public sealed class FallingObjectCatcherSettings
{
    public float DurationSeconds { get; init; } = 15f;
    public int TargetObjectCount { get; init; } = 10;
    public int DistractorCountPerShape { get; init; } = 4;
    public float FirstCatchSeconds { get; init; } = 3.8f;
    public float LastCatchSeconds { get; init; } = 14f;
    public float MinimumFallDurationSeconds { get; init; } = 2.2f;
    public float MaximumFallDurationSeconds { get; init; } = 3.6f;
    public float CatcherSpeedNormalizedPerSecond { get; init; } = 0.8f;
    public float CatcherHalfWidthNormalized { get; init; } = 0.06f;
    public float CatchToleranceNormalized { get; init; } = 0.06f;
    public float CatchWindowFallDurationFraction { get; init; } = 0.181f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
    public int CorrectCatchPoints { get; init; } = 10;
    public int WrongCatchPenalty { get; init; } = 5;
}
