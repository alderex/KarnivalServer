public sealed class PhotoPuzzleSliderMiniGame : IMiniGame
{
    private static readonly PhotoPuzzleImageId[] ImageIds =
    {
        PhotoPuzzleImageId.Cat,
        PhotoPuzzleImageId.Logo,
    };

    private static readonly byte[] CornerTileIds = { 0, 2, 6, 8 };

    private readonly PhotoPuzzleSliderSettings settings;

    public PhotoPuzzleSliderMiniGame(PhotoPuzzleSliderSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.PhotoPuzzleSlider;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        PhotoPuzzleImageId imageId = ImageIds[context.Random.Next(ImageIds.Length)];
        byte missingTileId = CornerTileIds[context.Random.Next(CornerTileIds.Length)];
        byte[] initialBoard = CreateShuffledBoard(
            missingTileId,
            Math.Max(1, settings.ShuffleMoveCount),
            Math.Clamp(settings.MinimumManhattanDistance, 0, 22),
            context.Random);

        return new PhotoPuzzleSliderRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            imageId,
            missingTileId,
            initialBoard,
            settings);
    }

    private static byte[] CreateShuffledBoard(
        byte missingTileId,
        int shuffleMoveCount,
        int minimumManhattanDistance,
        Random random)
    {
        for (int attempt = 0; attempt < 256; attempt++)
        {
            byte[] board = PhotoPuzzleBoard.CreateSolved(missingTileId);
            int emptySlot = missingTileId;
            int previousEmptySlot = -1;

            for (int move = 0; move < shuffleMoveCount; move++)
            {
                int[] adjacentSlots = PhotoPuzzleBoard.GetAdjacentSlots(emptySlot);
                int[] candidates = adjacentSlots
                    .Where(slot => slot != previousEmptySlot)
                    .ToArray();
                if (candidates.Length == 0)
                    candidates = adjacentSlots;

                int tileSlot = candidates[random.Next(candidates.Length)];
                int oldEmptySlot = emptySlot;
                board[emptySlot] = board[tileSlot];
                board[tileSlot] = PhotoPuzzleBoard.Empty;
                emptySlot = tileSlot;
                previousEmptySlot = oldEmptySlot;
            }

            if (!PhotoPuzzleBoard.IsSolved(board, missingTileId) &&
                PhotoPuzzleBoard.GetManhattanDistance(board) >= minimumManhattanDistance)
            {
                return board;
            }
        }

        throw new InvalidOperationException(
            "Could not generate a photo puzzle with the configured shuffle constraints.");
    }
}