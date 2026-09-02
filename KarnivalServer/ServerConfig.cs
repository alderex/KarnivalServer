public sealed class ServerConfig
{
    public static ServerConfig Default { get; } = new();

    public ushort Port { get; init; } = 8008;
    public ushort MaxClientCount { get; init; } = 1000;
    public int TargetTicksPerSecond { get; init; } = 20;
    public int ConnectionTimeoutMilliseconds { get; init; } = 60_000;
    public int ReliableMaxSendAttempts { get; init; } = 100;
    public string DatabasePath { get; init; } = "data/karnival.db";
    public float RoundDurationSeconds { get; init; } = 10f;
    public float ResultsDurationSeconds { get; init; } = 10f;
    public int SessionRoundCount { get; init; } = 20;
    public float LobbyDurationSeconds { get; init; } = 60f;
    public float VictoryDurationSeconds { get; init; } = 5f;
    public float FinalSummaryDurationSeconds { get; init; } = 4f;
    public BettingSettings Betting { get; init; } = new();
    public MiniGameSettings MiniGames { get; init; } = new();
    public NetworkSimulationConfig NetworkSimulation { get; init; } = new();
    public SimulatedPlayerConfig SimulatedPlayers { get; init; } = new();

    public TimeSpan TickInterval =>
        TimeSpan.FromMilliseconds(1000.0 / TargetTicksPerSecond);
}

public sealed class BettingSettings
{
    public bool Enabled { get; init; } = false;
    public float DurationSeconds { get; init; } = 15f;
    public int MaximumWager { get; init; } = 500;
    public int[] AfterRounds { get; init; } = { 5, 10, 15 };
}

public sealed class MiniGameSettings
{
    public SliderTargetSettings SliderTarget { get; init; } = new();
    public ObstacleRunnerSettings ObstacleRunner { get; init; } = new();
    public PhotoPuzzleSliderSettings PhotoPuzzleSlider { get; init; } = new();
    public FallingObjectCatcherSettings FallingObjectCatcher { get; init; } = new();
    public HordeClickerSettings HordeClicker { get; init; } = new();
    public ColorMatchSettings ColorMatch { get; init; } = new();
    public ColorSequenceSettings ColorSequence { get; init; } = new();
    public CardMatchSettings CardMatch { get; init; } = new();
    public CupAndBallSettings CupAndBall { get; init; } = new();
    public CarParkSettings CarPark { get; init; } = new();
    public ArcheryPracticeSettings ArcheryPractice { get; init; } = new();
    public StopGoSettings StopGo { get; init; } = new();
    public PotatoFaceSettings PotatoFace { get; init; } = new();
    public SpotTheDifferenceSettings SpotTheDifference { get; init; } = new();
    public PlateStackerSettings PlateStacker { get; init; } = new();
    public MazeSettings Maze { get; init; } = new();
}

public sealed class NetworkSimulationConfig
{
    public bool Enabled { get; init; } = false;
    public int IncomingLatencyMilliseconds { get; init; } = 75;
    public int OutgoingLatencyMilliseconds { get; init; } = 75;
    public int JitterMilliseconds { get; init; } = 0;
    public float IncomingLossChance { get; init; } = 0f;
    public float OutgoingLossChance { get; init; } = 0f;
    public int? RandomSeed { get; init; }
}

public sealed class SimulatedPlayerConfig
{
    public int InitialCount { get; init; } = 0;
    public int MaximumCount { get; init; } = 500;
}
