public sealed class MazeSettings
{
    public float DurationSeconds { get; init; } = 15f;
    public int GridSize { get; init; } = 9;
    public float PlayerRadiusNormalized { get; init; } = 0.025f;
    public float WallThicknessNormalized { get; init; } = 0.006f;
    public float ExitRadiusNormalized { get; init; } = 0.035f;
    public float OuterPaddingNormalized { get; init; } = 0.12f;
    public float InputGraceSeconds { get; init; } = 0.75f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
    public int MaximumBatchSamples { get; init; } = 16;
    public int MaximumInputSamples { get; init; } = 512;
    public int CorrectScore { get; init; } = 100;
}
