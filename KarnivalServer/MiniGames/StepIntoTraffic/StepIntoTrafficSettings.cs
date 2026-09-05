public sealed class StepIntoTrafficSettings
{
    public float DurationSeconds { get; init; } = 15f;
    public int LaneCount { get; init; } = 6;
    public float HopDurationSeconds { get; init; } = 0.3f;
    public float CollisionRecoverySeconds { get; init; } = 0.75f;
    public float MinimumCarSpeedNormalizedPerSecond { get; init; } = 0.65f;
    public float MaximumCarSpeedNormalizedPerSecond { get; init; } = 0.95f;
    public float MinimumSafeGapSeconds { get; init; } = 1.5f;
    public float MaximumSafeGapSeconds { get; init; } = 2.5f;
    public float CarHalfWidthNormalized { get; init; } = 0.088f;
    public float CarHalfHeightRows { get; init; } = 0.224f;
    public float PedestrianHalfWidthNormalized { get; init; } = 0.035f;
    public float PedestrianHalfHeightRows { get; init; } = 0.24f;
    public float SimulationStepSeconds { get; init; } = 1f / 120f;
    public int CorrectScore { get; init; } = 100;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
    public int MaximumInputSamples { get; init; } = 64;
}
