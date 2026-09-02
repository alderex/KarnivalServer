public sealed class ColorSequenceSettings
{
    public int BallCount { get; init; } = 5;
    public int SequenceLength { get; init; } = 8;
    public float PlaybackLeadInSeconds { get; init; } = 0.75f;
    public float PlaybackLitSeconds { get; init; } = 0.6f;
    public float PlaybackGapSeconds { get; init; } = 0.25f;
    public float InputDurationSeconds { get; init; } = 10f;
    public int PointsPerCorrectPosition { get; init; } = 10;
    public int PerfectSequenceBonus { get; init; } = 20;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
