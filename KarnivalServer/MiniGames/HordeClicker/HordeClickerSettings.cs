public sealed class HordeClickerSettings
{
    public float DurationSeconds { get; init; } = 15f;
    public int CharacterCount { get; init; } = 25;
    public int PointsPerCharacter { get; init; } = 5;
    public float FirstSpawnSeconds { get; init; } = 0.5f;
    public float LastSpawnSeconds { get; init; } = 10f;
    public float MinimumTravelDurationSeconds { get; init; } = 3f;
    public float MaximumTravelDurationSeconds { get; init; } = 4.5f;
    public float MinimumYNormalized { get; init; } = 0.1f;
    public float MaximumYNormalized { get; init; } = 0.9f;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
