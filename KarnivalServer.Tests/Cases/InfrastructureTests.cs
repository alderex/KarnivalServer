using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestDefaultConfiguration()
{
    ServerConfigValidator.Validate(ServerConfig.Default);
    string configPath = Path.Combine(serverRoot, "KarnivalServer", "serverconfig.json");
    ServerConfig loaded = ServerConfigLoader.Load(new[] { "--config", configPath });
    AssertEx.Equal(20, loaded.TargetTicksPerSecond);
    AssertEx.Equal(256, loaded.MiniGames.StopGo.MaximumInputSamples);
    AssertEx.Equal(20f, loaded.MiniGames.PotatoFace.DurationSeconds);
    AssertEx.Equal(10f, loaded.MiniGames.SpotTheDifference.DurationSeconds);
    AssertEx.Equal(6, loaded.MiniGames.SpotTheDifference.AvailableDifferenceCount);
    AssertEx.Equal(3, loaded.MiniGames.SpotTheDifference.SelectedDifferenceCount);
    AssertEx.Equal(10f, loaded.MiniGames.PlateStacker.DurationSeconds);
    AssertEx.Equal(10, loaded.MiniGames.PlateStacker.PlateCount);
    AssertEx.Equal(
        0.15f,
        loaded.MiniGames.PlateStacker.StartingPlateWidthNormalized);
    AssertEx.Equal(15f, loaded.MiniGames.Maze.DurationSeconds);
    AssertEx.Equal(12, loaded.MiniGames.Maze.GridSize);
    AssertEx.Equal(100, loaded.MiniGames.Maze.CorrectScore);
    AssertEx.Equal(16f, loaded.MiniGames.SmackMan.DurationSeconds);
    AssertEx.Equal(4, loaded.MiniGames.SmackMan.GroupCount);
    AssertEx.Equal(5, loaded.MiniGames.SmackMan.HeadsPerGroup);
    AssertEx.Equal(5, loaded.MiniGames.SmackMan.PointsPerHit);
    AssertEx.Equal(1f, loaded.MiniGames.SmackMan.FirstGroupSeconds);
    AssertEx.Equal(4f, loaded.MiniGames.SmackMan.GroupIntervalSeconds);
    AssertEx.Equal(0.12f, loaded.MiniGames.SmackMan.RiseDurationSeconds);
    AssertEx.Equal(2f, loaded.MiniGames.SmackMan.HoldDurationSeconds);
    AssertEx.Equal(0.5f, loaded.MiniGames.SmackMan.RetractDurationSeconds);
    AssertEx.Equal(10f, loaded.MiniGames.WheresBaldo.DurationSeconds);
    AssertEx.Equal(100, loaded.MiniGames.WheresBaldo.CorrectScore);
    AssertEx.Equal(10, loaded.MiniGames.WheresBaldo.WrongSelectionPenalty);
    AssertEx.Equal(10f, loaded.MiniGames.WheelSpinner.DurationSeconds);
    AssertEx.Equal(240f,
        loaded.MiniGames.WheelSpinner.AngularSpeedDegreesPerSecond);
    AssertEx.Equal(10, loaded.MiniGames.WheelSpinner.Slices.Length);
    AssertEx.Equal(360f,
        loaded.MiniGames.WheelSpinner.Slices.Sum(slice => slice.ArcDegrees));
    AssertEx.Equal(15f, loaded.MiniGames.StepIntoTraffic.DurationSeconds);
    AssertEx.Equal(6, loaded.MiniGames.StepIntoTraffic.LaneCount);
    AssertEx.Equal(0.088f,
        loaded.MiniGames.StepIntoTraffic.CarHalfWidthNormalized);
    AssertEx.Equal(0.224f,
        loaded.MiniGames.StepIntoTraffic.CarHalfHeightRows);
    AssertEx.Equal(0.181f,
        loaded.MiniGames.FallingObjectCatcher.CatchWindowFallDurationFraction);
    MiniGameCatalog catalog = MiniGameCatalog.CreateDefault(loaded);
    AssertEx.Equal(20, catalog.Count);
    AssertEx.True(catalog.TryGetGame("WheresBaldo", out _));
    AssertEx.True(catalog.TryGetGame("WheelSpinner", out _));
    AssertEx.True(catalog.TryGetGame("StepIntoTraffic", out _));
}

static void TestInvalidConfiguration()
{
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig { TargetTicksPerSecond = 0 }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                CarPark = new CarParkSettings { MaximumInputSamples = 2 },
            },
        }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                SmackMan = new SmackManSettings
                {
                    HeadsPerGroup = 9,
                    GroupIntervalSeconds = 1f,
                },
            },
        }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                Maze = new MazeSettings { GridSize = 8 },
            },
        }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                SpotTheDifference = new SpotTheDifferenceSettings
                {
                    SelectedDifferenceCount = 8,
                },
            },
        }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                WheresBaldo = new WheresBaldoSettings
                {
                    WrongSelectionPenalty = -1,
                },
            },
        }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                WheelSpinner = new WheelSpinnerSettings
                {
                    Slices = new[]
                    {
                        new WheelSpinnerSliceSettings
                        {
                            Points = 100,
                            ArcDegrees = 360f,
                        },
                    },
                },
            },
        }));
    AssertEx.Throws<InvalidDataException>(() =>
        ServerConfigValidator.Validate(new ServerConfig
        {
            MiniGames = new MiniGameSettings
            {
                PlateStacker = new PlateStackerSettings
                {
                    PlateCount =
                        PlateStackerSettings.MaximumSupportedPlateCount + 1,
                },
            },
        }));
}

static void TestGeneratedProtocols()
{
    string schemaPath = Path.Combine(serverRoot, "Protocol", "protocol.json");
    string expected = ProtocolCodeGenerator.Render(
        ProtocolCodeGenerator.ReadSchema(schemaPath));
    AssertEx.True(ProtocolCodeGenerator.Matches(
        Path.Combine(serverRoot, "KarnivalServer", "KarnivalProtocol.cs"), expected));
    AssertEx.True(ProtocolCodeGenerator.Matches(
        Path.Combine(clientRoot, "Assets", "Scripts", "Networking", "KarnivalProtocol.cs"),
        expected));
    AssertEx.Equal((ushort)46, KarnivalProtocol.Version);
    AssertEx.Equal((ushort)18, (ushort)NetworkMessageId.SessionWaiting);
}

static void TestMidRoundLoginWaitsForNextGame()
{
    string testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"karnival-waiting-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testDirectory);
    try
    {
        ServerConfig config = new()
        {
            SessionRoundCount = 2,
            LobbyDurationSeconds = 60f,
        };
        Dictionary<ushort, PlayerSession> sessions = new();
        Riptide.Server server = new();
        MiniGameRoundManager manager = new(
            config,
            new Random(17),
            sessions,
            server,
            new MiniGameCatalog(new IMiniGame[]
            {
                new SliderTargetMiniGame(config.MiniGames.SliderTarget),
            }),
            new SqliteAccountPersistence(
                Path.Combine(testDirectory, "waiting.db")));

        DateTime nowUtc = DateTime.UtcNow;
        PlayerSession firstPlayer = new(1, 101, "first");
        sessions.Add(firstPlayer.ClientId, firstPlayer);
        manager.OnHumanLoggedIn(firstPlayer, nowUtc);
        AssertEx.True(manager.TryStartSessionNow(nowUtc, out _));
        AssertEx.Equal(SessionLifecyclePhase.Round, manager.Phase);

        PlayerSession waitingPlayer = new(2, 202, "waiting");
        sessions.Add(waitingPlayer.ClientId, waitingPlayer);
        manager.OnHumanLoggedIn(waitingPlayer, nowUtc);
        AssertEx.True(manager.CanChat(waitingPlayer));

        AssertEx.True(manager.TrySkipToFinalRound(nowUtc, out _));
        AssertEx.Equal(SessionLifecyclePhase.Round, manager.Phase);
        AssertEx.True(!manager.CanChat(waitingPlayer));
    }
    finally
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(testDirectory, true);
    }
}

static void TestFinalSecondBonus()
{
    DateTime starts = DateTime.UtcNow;
    foreach (float contactTime in new[] { 9.25f, 10f })
    {
        var round = new TestRound(starts);
        PlayerSession player = new(85, 85, "bonus");
        round.Complete(player, 10, contactTime);
        AssertEx.Equal(contactTime == 10f ? 1f : 1.15f, player.RoundResult.SpeedMultiplier);
    }
}

static void TestScheduledGameBonusLimits()
{
    DateTime starts = DateTime.UtcNow.AddSeconds(-0.5);
    Riptide.Server server = new();
    var catcher = new FallingObjectCatcherRound(2020, starts, 10f, 1f,
        FallingObjectShape.Circle, 1, 1, 0, 1f, 1f, 1f, 1f,
        new[] { new FallingObjectScheduleEntry(1, FallingObjectShape.Circle, 0.5f, 0f, 1f, 1f) },
        new FallingObjectCatcherSettings { InputGraceSeconds = 0f });
    PlayerSession catcherPlayer = new(87, 87, "catcher");
    catcher.RegisterPlayer(catcherPlayer, starts);
    catcher.Update(starts.AddSeconds(3), new[] { catcherPlayer }, server);
    AssertEx.True(catcherPlayer.RoundResult.BaseScore > 0);
    AssertEx.Equal(1f, catcherPlayer.RoundResult.SpeedMultiplier);

    foreach (bool clickLast in new[] { false, true })
    {
        var horde = new HordeClickerRound(2021, starts, 10f, 1f,
            1, 2, 10, 0f, 0f, 1f, 2f, 0.5f, 0.5f,
            new[] { new HordeClickerScheduleEntry(1, 0f, 1f, 0.5f),
                    new HordeClickerScheduleEntry(2, 0f, 2f, 0.5f) },
            new HordeClickerSettings { InputGraceSeconds = 0.5f });
        PlayerSession player = new(88, 88, "horde");
        horde.RegisterPlayer(player, starts);
        for (ushort id = 1; id <= (clickLast ? 2 : 1); id++)
        {
            Message message = Message.Create();
            try
            {
                message.AddUShort(id);
                message.AddFloat(0.4f);
                horde.HandleInput(message, player, server);
            }
            finally { message.Release(); }
        }
        horde.Update(starts.AddSeconds(3), new[] { player }, server);
        AssertEx.Equal(clickLast ? 20 : 10, player.RoundResult.BaseScore);
        if (clickLast)
            AssertEx.True(player.RoundResult.SpeedMultiplier > 1f);
        else
            AssertEx.Equal(1f, player.RoundResult.SpeedMultiplier);
    }
}

static void TestRoundResultRestore()
{
    DateTime start = DateTime.UtcNow;
    TestRound round = new(start);
    round.ScheduleStart(start);
    PlayerSession original = new(1, 42, "player", isSimulated: true);
    round.Complete(original, 80, 0f);
    AssertEx.Equal(240, original.RoundScore);
    AssertEx.Equal(80, original.RoundResult.BaseScore);
    AssertEx.Equal(300, original.RoundResult.MaximumScore);
    AssertEx.Equal(3f, original.RoundResult.SpeedMultiplier);

    PlayerSession reconnected = new(2, 42, "player");
    reconnected.RestoreSessionScore(original.TotalScore);
    reconnected.RestoreRoundState(original.RoundResult);
    AssertEx.Equal(original.RoundResult, reconnected.RoundResult);
    AssertEx.True(reconnected.SubmittedThisRound);
}


}
