public sealed class PhotoPuzzleSliderSettings
{
    public float DurationSeconds { get; init; } = 30f;
    public int ShuffleMoveCount { get; init; } = 100;
    public int MinimumManhattanDistance { get; init; } = 8;
    public int CorrectTilePoints { get; init; } = 10;
    public int CompletionBonus { get; init; } = 20;
}