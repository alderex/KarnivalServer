using Riptide;

public sealed class PhotoPuzzleSliderRound : MiniGameRoundBase
{
    private sealed class PlayerState
    {
        public PlayerState(byte[] initialBoard)
        {
            Board = (byte[])initialBoard.Clone();
        }

        public byte[] Board { get; }
    }

    private readonly byte[] initialBoard;
    private readonly PhotoPuzzleSliderSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public PhotoPuzzleSliderRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        PhotoPuzzleImageId imageId,
        byte missingTileId,
        byte[] initialBoard,
        PhotoPuzzleSliderSettings settings)
        : base(
            roundId,
            MiniGameType.PhotoPuzzleSlider,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds)
    {
        if (initialBoard == null || initialBoard.Length != PhotoPuzzleBoard.SlotCount)
            throw new ArgumentException("A photo puzzle requires exactly nine board entries.", nameof(initialBoard));

        ImageId = imageId;
        MissingTileId = missingTileId;
        this.initialBoard = (byte[])initialBoard.Clone();
        this.settings = settings;
    }

    public PhotoPuzzleImageId ImageId { get; }
    public byte MissingTileId { get; }

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (!playerStates.ContainsKey(session.ClientId))
            playerStates.Add(session.ClientId, new PlayerState(initialBoard));
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte tileId = message.GetByte();
        if (tileId >= PhotoPuzzleBoard.SlotCount || tileId == MissingTileId)
            return;

        RegisterPlayer(session, DateTime.UtcNow);
        PlayerState state = playerStates[session.ClientId];
        int tileSlot = PhotoPuzzleBoard.Find(state.Board, tileId);
        int emptySlot = PhotoPuzzleBoard.Find(state.Board, PhotoPuzzleBoard.Empty);
        if (!PhotoPuzzleBoard.AreAdjacent(tileSlot, emptySlot))
            return;

        state.Board[emptySlot] = tileId;
        state.Board[tileSlot] = PhotoPuzzleBoard.Empty;

        if (PhotoPuzzleBoard.IsSolved(state.Board, MissingTileId))
            CompletePlayer(session, state, server);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            if (!session.SubmittedThisRound)
                CompletePlayer(session, playerStates[session.ClientId], server);
        }
    }

    public override string Describe()
    {
        return $"image={ImageId} missingTile={MissingTileId}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)ImageId);
        message.AddByte(MissingTileId);
        foreach (byte tileId in initialBoard)
            message.AddByte(tileId);
    }

    private void CompletePlayer(
        PlayerSession session,
        PlayerState state,
        Riptide.Server server)
    {
        int correctTiles = PhotoPuzzleBoard.CountCorrectTiles(state.Board, MissingTileId);
        bool solved = PhotoPuzzleBoard.IsSolved(state.Board, MissingTileId);
        int score = correctTiles * Math.Max(0, settings.CorrectTilePoints);
        if (solved)
            score += Math.Max(0, settings.CompletionBonus);

        if (!CompleteSubmission(session, score, server))
            return;

        RiptideConsoleLogger.Info(
            $"Round {RoundId}: {session.Username} completed photo puzzle with " +
            $"{correctTiles}/8 correct tiles, solved={solved}, score={score}.");
    }
}