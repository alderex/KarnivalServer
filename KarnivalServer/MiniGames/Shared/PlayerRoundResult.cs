public readonly record struct PlayerRoundResult(
    bool Submitted,
    int BaseScore,
    int Score,
    int MaximumScore,
    float SpeedMultiplier)
{
    public static PlayerRoundResult Pending { get; } = new(false, 0, 0, 0, 1f);
    public static PlayerRoundResult Missed { get; } = new(false, 0, 0, 0, 1f);
}
