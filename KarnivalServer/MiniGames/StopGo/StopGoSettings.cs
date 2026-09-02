public sealed class StopGoSettings
{
    public float DurationSeconds { get; init; } = 30f;
    public float ContinuousTraversalSeconds { get; init; } = 10f;
    public float MinimumGreenSeconds { get; init; } = 2f;
    public float MaximumGreenSeconds { get; init; } = 4f;
    public float MinimumRedSeconds { get; init; } = 2f;
    public float MaximumRedSeconds { get; init; } = 4f;
    public float RedGraceSeconds { get; init; } = 0.5f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
    public float SnapshotIntervalSeconds { get; init; } = 0.1f;
    public int MaximumInputSamples { get; init; } = 256;
}
