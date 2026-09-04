public sealed class WheresBaldoSettings
{
    public float DurationSeconds { get; init; } = 10f;
    public int CorrectScore { get; init; } = 100;
    public int WrongSelectionPenalty { get; init; } = 10;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
