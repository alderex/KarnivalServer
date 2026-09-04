public sealed class SpotTheDifferenceSettings
{
    public float DurationSeconds { get; init; } = 10f;
    public int AvailableDifferenceCount { get; init; } = 6;
    public int SelectedDifferenceCount { get; init; } = 3;
    public int PointsPerDifference { get; init; } = 25;
    public int FullCompletionScore { get; init; } = 100;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
