public sealed class CupAndBallSettings
{
    public float BallRevealSeconds { get; init; } = 1.25f;
    public float CupDropSeconds { get; init; } = 0.175f;
    public float PreShufflePauseSeconds { get; init; } = 0.25f;
    public int ShuffleCount { get; init; } = 8;
    public float ShuffleStepSeconds { get; init; } = 0.175f;
    public float InputDurationSeconds { get; init; } = 6f;
    public float OutcomeRevealSeconds { get; init; } = 2f;
    public int CorrectScore { get; init; } = 100;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
