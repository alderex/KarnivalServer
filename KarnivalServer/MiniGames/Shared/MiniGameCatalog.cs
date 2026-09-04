public sealed class MiniGameCatalog
{
    private readonly IReadOnlyList<IMiniGame> games;

    public MiniGameCatalog(IEnumerable<IMiniGame> games)
    {
        this.games = games.ToArray();
        if (this.games.Count == 0)
            throw new InvalidOperationException("At least one mini game must be registered.");
    }

    public static MiniGameCatalog CreateDefault(ServerConfig config)
    {
        return new MiniGameCatalog(new IMiniGame[]
        {
            new SliderTargetMiniGame(config.MiniGames.SliderTarget),
            new ObstacleRunnerMiniGame(config.MiniGames.ObstacleRunner),
            new PhotoPuzzleSliderMiniGame(config.MiniGames.PhotoPuzzleSlider),
            new FallingObjectCatcherMiniGame(config.MiniGames.FallingObjectCatcher),
            new HordeClickerMiniGame(config.MiniGames.HordeClicker),
            new ColorMatchMiniGame(config.MiniGames.ColorMatch),
            new ColorSequenceMiniGame(config.MiniGames.ColorSequence),
            new CardMatchMiniGame(config.MiniGames.CardMatch),
            new CupAndBallMiniGame(config.MiniGames.CupAndBall),
            new CarParkMiniGame(config.MiniGames.CarPark),
            new ArcheryPracticeMiniGame(config.MiniGames.ArcheryPractice),
            new StopGoMiniGame(config.MiniGames.StopGo),
            new PotatoFaceMiniGame(config.MiniGames.PotatoFace),
            new SpotTheDifferenceMiniGame(config.MiniGames.SpotTheDifference),
            new PlateStackerMiniGame(config.MiniGames.PlateStacker),
            new MazeMiniGame(config.MiniGames.Maze),
            new SmackManMiniGame(config.MiniGames.SmackMan),
            new WheresBaldoMiniGame(config.MiniGames.WheresBaldo),
        });
    }

    public int Count => games.Count;

    public IMiniGame ChooseNext(
        Random random,
        MiniGameType? excludedGameType = null)
    {
        return ChooseNext(random, Array.Empty<MiniGameType>(), excludedGameType);
    }

    public IMiniGame ChooseNext(
        Random random,
        IEnumerable<MiniGameType> unavailableGameTypes,
        MiniGameType? avoidGameType = null)
    {
        HashSet<MiniGameType> unavailable = new(unavailableGameTypes);
        IMiniGame[] candidates = games
            .Where(game => !unavailable.Contains(game.GameType))
            .ToArray();
        if (avoidGameType.HasValue && candidates.Length > 1)
        {
            candidates = candidates
                .Where(game => game.GameType != avoidGameType.Value)
                .ToArray();
        }

        if (candidates.Length == 0)
            throw new InvalidOperationException("No eligible mini games remain in this session.");
        return candidates[random.Next(candidates.Length)];
    }

    public bool TryGetGame(string name, out IMiniGame game)
    {
        IMiniGame? match = games.FirstOrDefault(candidate =>
            candidate.GameType.ToString().Equals(
                name,
                StringComparison.OrdinalIgnoreCase));
        game = match!;
        return match != null;
    }

    public string GetGameNames()
    {
        return string.Join(", ", games.Select(game => game.GameType));
    }
}
