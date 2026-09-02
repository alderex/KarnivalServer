public readonly record struct MiniGameRoundStartContext(
    uint RoundId,
    DateTime StartsUtc,
    float DurationSeconds,
    float ResultsDurationSeconds,
    Random Random);