using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

string serverRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : FindServerRoot();
string clientRoot = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.GetFullPath(Path.Combine(serverRoot, "..", "..", "Unity Projects", "Karnival Client"));

(string Name, Action Test)[] tests =
{
    ("Default configuration is valid", TestDefaultConfiguration),
    ("Invalid configurations fail early", TestInvalidConfiguration),
    ("Generated protocols match the schema", TestGeneratedProtocols),
    ("Maze layouts are deterministic, connected, and perfect", TestMazeLayout),
    ("Maze payload contains the authoritative layout", TestMazePayload),
    ("Maze movement blocks walls and slides along them", TestMazeMovement),
    ("Maze completion and timeout scoring are authoritative", TestMazeScoring),
    ("Falling-object schedules are deterministic", TestFallingScheduleDeterminism),
    ("PlateStacker schedules are deterministic and evenly paced", TestPlateStackerSchedule),
    ("Horde schedules are deterministic", TestHordeScheduleDeterminism),
    ("Falling-object payload contains authoritative schedule", TestFallingPayload),
    ("PlateStacker payload contains authoritative rules and schedule", TestPlateStackerPayload),
    ("Horde payload contains authoritative schedule", TestHordePayload),
    ("PotatoFace targets are deterministic and valid", TestPotatoFaceTargets),
    ("PotatoFace scoring rewards matching accuracy", TestPotatoFaceScoring),
    ("PotatoFace payload contains the target face", TestPotatoFacePayload),
    ("SpotTheDifference subsets are deterministic and valid", TestSpotTheDifferenceSubset),
    ("SpotTheDifference payload contains authoritative scoring", TestSpotTheDifferencePayload),
    ("SpotTheDifference validates selections and scores partial progress", TestSpotTheDifferenceScoring),
    ("PlateStacker catches, misses, collapse, and scoring are authoritative", TestPlateStackerScoring),
    ("Round-result metadata survives reconnect restore", TestRoundResultRestore),
    ("StopGo input history is bounded and coalesced", TestStopGoInputBounds),
    ("CarPark input history is bounded and coalesced", TestCarParkInputBounds),
};

int failures = 0;
foreach ((string name, Action test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} server regression tests passed.");
return failures == 0 ? 0 : 1;

static string FindServerRoot()
{
    foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        DirectoryInfo? directory = new(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Protocol", "protocol.json")) &&
                File.Exists(Path.Combine(directory.FullName, "KarnivalServer", "serverconfig.json")))
                return directory.FullName;

            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException(
        "Could not locate the Karnival server repository. Pass its path as the first argument.");
}

void TestDefaultConfiguration()
{
    ServerConfigValidator.Validate(ServerConfig.Default);
    string configPath = Path.Combine(serverRoot, "KarnivalServer", "serverconfig.json");
    ServerConfig loaded = ServerConfigLoader.Load(new[] { "--config", configPath });
    AssertEx.Equal(20, loaded.TargetTicksPerSecond);
    AssertEx.Equal(256, loaded.MiniGames.StopGo.MaximumInputSamples);
    AssertEx.Equal(20f, loaded.MiniGames.PotatoFace.DurationSeconds);
    AssertEx.Equal(10f, loaded.MiniGames.SpotTheDifference.DurationSeconds);
    AssertEx.Equal(7, loaded.MiniGames.SpotTheDifference.AvailableDifferenceCount);
    AssertEx.Equal(3, loaded.MiniGames.SpotTheDifference.SelectedDifferenceCount);
    AssertEx.Equal(10f, loaded.MiniGames.PlateStacker.DurationSeconds);
    AssertEx.Equal(10, loaded.MiniGames.PlateStacker.PlateCount);
    AssertEx.Equal(15f, loaded.MiniGames.Maze.DurationSeconds);
    AssertEx.Equal(12, loaded.MiniGames.Maze.GridSize);
    AssertEx.Equal(100, loaded.MiniGames.Maze.CorrectScore);
}

void TestInvalidConfiguration()
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
}

void TestGeneratedProtocols()
{
    string schemaPath = Path.Combine(serverRoot, "Protocol", "protocol.json");
    string expected = ProtocolCodeGenerator.Render(
        ProtocolCodeGenerator.ReadSchema(schemaPath));
    AssertEx.True(ProtocolCodeGenerator.Matches(
        Path.Combine(serverRoot, "KarnivalServer", "KarnivalProtocol.cs"), expected));
    AssertEx.True(ProtocolCodeGenerator.Matches(
        Path.Combine(clientRoot, "Assets", "Scripts", "Networking", "KarnivalProtocol.cs"),
        expected));
    AssertEx.Equal((ushort)36, KarnivalProtocol.Version);
}

void TestMazeLayout()
{
    MazeLayout first = MazeGenerator.Generate(0x12345678u, 12);
    MazeLayout second = MazeGenerator.Generate(0x12345678u, 12);
    AssertEx.Equal(12, first.GridSize);
    AssertEx.Equal(78, first.StartCell);
    AssertEx.Equal(first.ExitCell, second.ExitCell);
    AssertEx.Equal(first.ExitSide, second.ExitSide);
    AssertEx.SequenceEqual(first.Walls, second.Walls);

    int internalOpenings = 0;
    for (int row = 0; row < first.GridSize; row++)
    {
        for (int column = 0; column < first.GridSize; column++)
        {
            int cell = (row * first.GridSize) + column;
            MazeWallMask walls = (MazeWallMask)first.Walls[cell];
            if (column < first.GridSize - 1 &&
                (walls & MazeWallMask.East) == 0)
            {
                internalOpenings++;
            }
            if (row < first.GridSize - 1 &&
                (walls & MazeWallMask.North) == 0)
            {
                internalOpenings++;
            }
        }
    }

    AssertEx.Equal(143, internalOpenings);
    IReadOnlyList<int> path = FindMazeCellPath(first);
    AssertEx.Equal(first.StartCell, path[0]);
    AssertEx.Equal(first.ExitCell, path[^1]);
    AssertEx.Equal(144, CountReachableCells(first));
    int exitColumn = first.ExitCell % first.GridSize;
    int exitRow = first.ExitCell / first.GridSize;
    AssertEx.True(
        exitColumn == 0 ||
        exitColumn == first.GridSize - 1 ||
        exitRow == 0 ||
        exitRow == first.GridSize - 1);
    MazeWallMask exitWall = first.ExitSide switch
    {
        MazeExitSide.North => MazeWallMask.North,
        MazeExitSide.East => MazeWallMask.East,
        MazeExitSide.South => MazeWallMask.South,
        _ => MazeWallMask.West,
    };
    AssertEx.True(
        ((MazeWallMask)first.Walls[first.ExitCell] & exitWall) == 0);
}

void TestMazePayload()
{
    MazeRound round = (MazeRound)new MazeMiniGame(
        new MazeSettings()).CreateRound(Context(1014, 71));
    round.ConfigureSession(15, 7, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.Maze);
        AssertEx.Equal(round.Layout.Seed, message.GetUInt());
        AssertEx.Equal((byte)12, message.GetByte());
        AssertEx.Equal((byte)round.Layout.StartCell, message.GetByte());
        AssertEx.Equal((byte)round.Layout.ExitCell, message.GetByte());
        AssertEx.Equal((byte)round.Layout.ExitSide, message.GetByte());
        AssertEx.Equal(round.StartPosition.X, message.GetFloat());
        AssertEx.Equal(round.StartPosition.Y, message.GetFloat());
        AssertEx.Equal(round.ExitPosition.X, message.GetFloat());
        AssertEx.Equal(round.ExitPosition.Y, message.GetFloat());
        AssertEx.Equal(0.01875f, message.GetFloat());
        AssertEx.Equal(0.0045f, message.GetFloat());
        AssertEx.Equal(0.02625f, message.GetFloat());
        AssertEx.Equal(0.12f, message.GetFloat());
        AssertEx.Equal((byte)16, message.GetByte());
        AssertEx.Equal((ushort)512, message.GetUShort());
        int wallCount = message.GetUShort();
        byte[] walls = new byte[wallCount];
        for (int index = 0; index < wallCount; index++)
            walls[index] = message.GetByte();
        AssertEx.SequenceEqual(round.Layout.Walls, walls);
    }
    finally
    {
        message.Release();
    }
}

void TestMazeMovement()
{
    byte[] walls = Enumerable
        .Repeat((byte)MazeWallMask.All, 9)
        .ToArray();
    MazeLayout blockedLayout = new(
        1,
        3,
        walls,
        4,
        5,
        MazeExitSide.East);
    MazeMovementSimulator blocked = new(blockedLayout, 0.025f, 0.006f);
    MazeMovementSimulator.Result collision = blocked.Move(
        new MazePoint(0.5f, 0.5f),
        new MazePoint(0.9f, 0.62f));
    AssertEx.True(collision.Position.X < (2f / 3f));
    AssertEx.True(collision.Position.Y > 0.5f);

    walls[4] &= (byte)~MazeWallMask.East;
    walls[5] &= (byte)~MazeWallMask.West;
    MazeMovementSimulator open = new(blockedLayout, 0.025f, 0.006f);
    MazeMovementSimulator.Result crossing = open.Move(
        new MazePoint(0.5f, 0.5f),
        new MazePoint(0.85f, 0.5f));
    AssertEx.True(crossing.Position.X > (2f / 3f));
}

void TestMazeScoring()
{
    MazeSettings settings = new()
    {
        FutureInputToleranceSeconds = 20f,
        InputGraceSeconds = 20f,
    };
    MazeRound completed = (MazeRound)new MazeMiniGame(settings)
        .CreateRound(Context(1015, 72));
    completed.ScheduleStart(DateTime.UtcNow);
    PlayerSession successfulPlayer = new(11, 51, "solver");
    Riptide.Server server = new();
    IReadOnlyList<int> path = FindMazeCellPath(completed.Layout);
    float sampledAt = 0.05f;
    foreach (int cell in path.Skip(1))
    {
        SendMaze(
            completed,
            successfulPlayer,
            server,
            MazeGenerator.GetCellCenter(cell, completed.Layout.GridSize),
            sampledAt);
        sampledAt += 0.05f;
    }
    SendMaze(
        completed,
        successfulPlayer,
        server,
        completed.ExitPosition,
        sampledAt);
    AssertEx.True(successfulPlayer.SubmittedThisRound);
    AssertEx.Equal(100, successfulPlayer.RoundResult.BaseScore);
    AssertEx.True(successfulPlayer.RoundScore >= 100);

    MazeRound timedOut = (MazeRound)new MazeMiniGame(settings)
        .CreateRound(Context(1016, 73));
    PlayerSession timedOutPlayer = new(12, 52, "timeout");
    timedOut.FinalizeRound(new[] { timedOutPlayer }, server);
    AssertEx.Equal(0, timedOutPlayer.RoundResult.BaseScore);
    AssertEx.Equal(0, timedOutPlayer.RoundScore);
}

void TestPotatoFaceTargets()
{
    PotatoFaceTarget first = PotatoFaceTargetGenerator.Generate(new Random(4567));
    PotatoFaceTarget second = PotatoFaceTargetGenerator.Generate(new Random(4567));
    AssertEx.Equal(first, second);
    AssertEx.True(first.EyesVariant is PotatoFacePartVariant.One or PotatoFacePartVariant.Two);
    AssertEx.True(first.MouthVariant is PotatoFacePartVariant.One or PotatoFacePartVariant.Two);
    AssertEx.True(first.NoseVariant is PotatoFacePartVariant.One or PotatoFacePartVariant.Two);
}

void TestPotatoFaceScoring()
{
    PotatoFaceSettings settings = new() { PositionToleranceNormalized = 0.4f };
    PotatoFacePosition exact = new(0f, 0f);
    PotatoFacePosition halfway = new(0.2f, 0f);
    PotatoFaceTarget target = new(
        PotatoFacePartVariant.One,
        PotatoFacePartVariant.Two,
        PotatoFacePartVariant.One);

    AssertEx.Equal(100, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        settings));
    AssertEx.Equal(50, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, halfway,
        PotatoFacePartVariant.One, halfway,
        PotatoFacePartVariant.Two, halfway,
        PotatoFacePartVariant.One, halfway,
        settings));
    AssertEx.Equal(0, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.Two, exact,
        settings));
    AssertEx.Equal(60, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        settings));
    AssertEx.Equal(80, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        settings));
    AssertEx.Equal(0, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.None, exact,
        settings));
}

void TestPotatoFacePayload()
{
    PotatoFaceRound round = (PotatoFaceRound)new PotatoFaceMiniGame(
        new PotatoFaceSettings()).CreateRound(Context(1006, 47));
    round.ConfigureSession(12, 4, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.PotatoFace);
        AssertEx.Equal(round.Target.EyesVariant,
            (PotatoFacePartVariant)message.GetByte());
        AssertEx.Equal(round.Target.MouthVariant,
            (PotatoFacePartVariant)message.GetByte());
        AssertEx.Equal(round.Target.NoseVariant,
            (PotatoFacePartVariant)message.GetByte());
    }
    finally
    {
        message.Release();
    }
}

void TestSpotTheDifferencePayload()
{
    SpotTheDifferenceRound round = (SpotTheDifferenceRound)new SpotTheDifferenceMiniGame(
        new SpotTheDifferenceSettings()).CreateRound(Context(1007, 51));
    round.ConfigureSession(13, 5, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.SpotTheDifference);
        AssertEx.Equal((byte)3, message.GetByte());
        byte[] payloadIds = new byte[3];
        for (int index = 0; index < payloadIds.Length; index++)
            payloadIds[index] = message.GetByte();
        AssertEx.SequenceEqual(round.SelectedDifferenceIds, payloadIds);
        AssertEx.Equal(25, message.GetInt());
        AssertEx.Equal(100, round.MaximumScore);
    }
    finally
    {
        message.Release();
    }
}

void TestSpotTheDifferenceSubset()
{
    byte[] first = SpotTheDifferenceMiniGame.SelectDifferenceIds(
        7,
        3,
        new Random(2468));
    byte[] second = SpotTheDifferenceMiniGame.SelectDifferenceIds(
        7,
        3,
        new Random(2468));
    AssertEx.SequenceEqual(first, second);
    AssertEx.Equal(3, first.Length);
    AssertEx.Equal(3, first.Distinct().Count());
    AssertEx.True(first.All(differenceId => differenceId < 7));
}

void TestSpotTheDifferenceScoring()
{
    SpotTheDifferenceSettings settings = new()
    {
        InputGraceSeconds = 0.5f,
        FutureInputToleranceSeconds = 2f,
    };
    Riptide.Server server = new();

    SpotTheDifferenceRound partial = (SpotTheDifferenceRound)new SpotTheDifferenceMiniGame(
        settings).CreateRound(Context(1008, 52));
    partial.ScheduleStart(DateTime.UtcNow.AddSeconds(-partial.DurationSeconds));
    PlayerSession partialPlayer = new(6, 46, "partial");
    byte firstSelected = partial.SelectedDifferenceIds[0];
    byte secondSelected = partial.SelectedDifferenceIds[1];
    byte thirdSelected = partial.SelectedDifferenceIds[2];
    byte unselected = Enumerable.Range(0, 7)
        .Select(value => (byte)value)
        .First(differenceId => !partial.SelectedDifferenceIds.Contains(differenceId));
    SendSpotTheDifference(
        partial, partialPlayer, server, firstSelected, partial.DurationSeconds);
    SendSpotTheDifference(
        partial, partialPlayer, server, firstSelected, partial.DurationSeconds);
    SendSpotTheDifference(
        partial, partialPlayer, server, secondSelected, partial.DurationSeconds);
    SendSpotTheDifference(
        partial, partialPlayer, server, unselected, partial.DurationSeconds);
    SendSpotTheDifference(partial, partialPlayer, server, thirdSelected, 0f);
    SendSpotTheDifference(
        partial,
        partialPlayer,
        server,
        thirdSelected,
        partial.DurationSeconds + 1f);
    AssertEx.True(!partialPlayer.SubmittedThisRound);
    partial.FinalizeRound(new[] { partialPlayer }, server);
    AssertEx.Equal(50, partialPlayer.RoundResult.BaseScore);
    AssertEx.Equal(50, partialPlayer.RoundScore);
    AssertEx.Equal(1f, partialPlayer.RoundResult.SpeedMultiplier);

    SpotTheDifferenceRound complete = (SpotTheDifferenceRound)new SpotTheDifferenceMiniGame(
        settings).CreateRound(Context(1009, 53));
    complete.ScheduleStart(DateTime.UtcNow);
    PlayerSession completePlayer = new(7, 47, "complete");
    foreach (byte differenceId in complete.SelectedDifferenceIds)
        SendSpotTheDifference(complete, completePlayer, server, differenceId, 0f);
    AssertEx.True(completePlayer.SubmittedThisRound);
    AssertEx.Equal(100, completePlayer.RoundResult.BaseScore);
    AssertEx.Equal(300, completePlayer.RoundScore);
    AssertEx.Equal(3f, completePlayer.RoundResult.SpeedMultiplier);
}

void TestFallingScheduleDeterminism()
{
    FallingObjectScheduleEntry[] first = FallingObjectScheduleGenerator.Generate(
        FallingObjectShape.Star, 12345, 10, 4, 3.8f, 14f, 2.2f, 3.6f);
    FallingObjectScheduleEntry[] second = FallingObjectScheduleGenerator.Generate(
        FallingObjectShape.Star, 12345, 10, 4, 3.8f, 14f, 2.2f, 3.6f);
    AssertEx.SequenceEqual(first, second);
    AssertEx.True(first.All(entry => entry.XNormalized is >= 0f and <= 1f));
}

void TestPlateStackerSchedule()
{
    PlateStackerScheduleEntry[] first =
        PlateStackerScheduleGenerator.Generate(
            24680,
            10,
            1.5f,
            9.25f,
            0.9f,
            1.4f,
            0.12f);
    PlateStackerScheduleEntry[] second =
        PlateStackerScheduleGenerator.Generate(
            24680,
            10,
            1.5f,
            9.25f,
            0.9f,
            1.4f,
            0.12f);
    AssertEx.SequenceEqual(first, second);
    AssertEx.Equal(10, first.Length);
    AssertEx.Equal(1.5f, first[0].LandingSeconds);
    AssertEx.Equal(9.25f, first[^1].LandingSeconds);
    AssertEx.True(first.All(entry =>
        entry.XNormalized is >= 0.06f and <= 0.94f));
    float expectedInterval =
        (first[^1].LandingSeconds - first[0].LandingSeconds) /
        (first.Length - 1);
    for (int index = 1; index < first.Length; index++)
    {
        float interval =
            first[index].LandingSeconds -
            first[index - 1].LandingSeconds;
        AssertEx.True(Math.Abs(interval - expectedInterval) < 0.0001f);
    }
}

void TestHordeScheduleDeterminism()
{
    HordeClickerScheduleEntry[] first = HordeClickerScheduleGenerator.Generate(
        54321, 25, 0.5f, 10f, 3f, 4.5f, 0.1f, 0.9f);
    HordeClickerScheduleEntry[] second = HordeClickerScheduleGenerator.Generate(
        54321, 25, 0.5f, 10f, 3f, 4.5f, 0.1f, 0.9f);
    AssertEx.SequenceEqual(first, second);
    AssertEx.True(first.All(entry => entry.YNormalized is >= 0.1f and <= 0.9f));
}

void TestFallingPayload()
{
    MiniGameRoundBase round = new FallingObjectCatcherMiniGame(
        new FallingObjectCatcherSettings()).CreateRound(Context(1001, 9));
    round.ConfigureSession(7, 2, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.FallingObjectCatcher);
        FallingObjectShape target = (FallingObjectShape)message.GetByte();
        uint seed = message.GetUInt();
        int targets = message.GetUShort();
        int distractors = message.GetUShort();
        float firstCatch = message.GetFloat();
        float lastCatch = message.GetFloat();
        float minimumFall = message.GetFloat();
        float maximumFall = message.GetFloat();
        message.GetFloat();
        message.GetFloat();
        message.GetFloat();
        message.GetFloat();
        int count = message.GetUShort();
        FallingObjectScheduleEntry[] expected = FallingObjectScheduleGenerator.Generate(
            target, seed, targets, distractors, firstCatch, lastCatch, minimumFall, maximumFall);
        AssertEx.Equal(expected.Length, count);
        for (int index = 0; index < count; index++)
        {
            FallingObjectScheduleEntry actual = new(
                message.GetUShort(),
                (FallingObjectShape)message.GetByte(),
                message.GetFloat(),
                message.GetFloat(),
                message.GetFloat(),
                message.GetFloat());
            AssertEx.Equal(expected[index], actual);
        }
    }
    finally
    {
        message.Release();
    }
}

void TestPlateStackerPayload()
{
    PlateStackerRound round = (PlateStackerRound)new PlateStackerMiniGame(
        new PlateStackerSettings()).CreateRound(Context(1010, 61));
    round.ConfigureSession(14, 6, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.PlateStacker);
        message.GetUInt();
        AssertEx.Equal(10, (int)message.GetUShort());
        AssertEx.Equal(1.5f, message.GetFloat());
        AssertEx.Equal(9.25f, message.GetFloat());
        AssertEx.Equal(0.9f, message.GetFloat());
        AssertEx.Equal(1.4f, message.GetFloat());
        AssertEx.Equal(0.8f, message.GetFloat());
        AssertEx.Equal(0.12f, message.GetFloat());
        AssertEx.Equal(1.5f, message.GetFloat());
        AssertEx.Equal(10, message.GetInt());
        int scheduleCount = message.GetUShort();
        AssertEx.Equal(round.Schedule.Count, scheduleCount);
        for (int index = 0; index < scheduleCount; index++)
        {
            PlateStackerScheduleEntry expected = round.Schedule[index];
            AssertEx.Equal(expected.PlateId, message.GetUShort());
            AssertEx.Equal(expected.XNormalized, message.GetFloat());
            AssertEx.Equal(expected.SpawnSeconds, message.GetFloat());
            AssertEx.Equal(expected.LandingSeconds, message.GetFloat());
            AssertEx.Equal(expected.FallDurationSeconds, message.GetFloat());
        }
        AssertEx.Equal(100, round.MaximumScore);
    }
    finally
    {
        message.Release();
    }
}

void TestPlateStackerScoring()
{
    AssertEx.True(PlateStackerRound.HasVisibleOverlap(0f, 0.999f, 1f));
    AssertEx.True(!PlateStackerRound.HasVisibleOverlap(0f, 1f, 1f));
    AssertEx.True(!PlateStackerRound.ExceedsCollapseThreshold(
        1.5f, 1f, 1.5f));
    AssertEx.True(PlateStackerRound.ExceedsCollapseThreshold(
        1.5001f, 1f, 1.5f));

    Riptide.Server server = new();
    DateTime startsUtc = DateTime.UtcNow.AddSeconds(-2f);
    PlateStackerSettings successSettings = new()
    {
        DurationSeconds = 2f,
        PlateCount = 3,
        FirstLandingSeconds = 0.25f,
        LastLandingSeconds = 1.5f,
        MinimumFallDurationSeconds = 0.2f,
        MaximumFallDurationSeconds = 0.2f,
        InputGraceSeconds = 0f,
        PointsPerPlate = 10,
    };
    PlateStackerScheduleEntry[] successSchedule =
    {
        new(1, 0.5f, 0.05f, 0.25f, 0.2f),
        new(2, 0.5f, 0.55f, 0.75f, 0.2f),
        new(3, 0.5f, 1.3f, 1.5f, 0.2f),
    };
    PlateStackerRound success = CreatePlateStackerRound(
        1011,
        startsUtc,
        successSettings,
        successSchedule);
    PlayerSession successfulPlayer = new(8, 48, "stacker");
    success.RegisterPlayer(successfulPlayer, startsUtc);
    success.Update(
        startsUtc.AddSeconds(2f),
        new[] { successfulPlayer },
        server);
    AssertEx.Equal(30, successfulPlayer.RoundResult.BaseScore);
    AssertEx.Equal(30, successfulPlayer.RoundScore);

    PlateStackerScheduleEntry[] missSchedule =
    {
        new(1, 0.5f, 0.05f, 0.25f, 0.2f),
        new(2, 0.8f, 0.55f, 0.75f, 0.2f),
        new(3, 0.5f, 1.3f, 1.5f, 0.2f),
    };
    PlateStackerRound missed = CreatePlateStackerRound(
        1012,
        startsUtc,
        successSettings,
        missSchedule);
    PlayerSession missedPlayer = new(9, 49, "missed");
    missed.RegisterPlayer(missedPlayer, startsUtc);
    missed.Update(
        startsUtc.AddSeconds(2f),
        new[] { missedPlayer },
        server);
    AssertEx.Equal(20, missedPlayer.RoundResult.BaseScore);

    PlateStackerScheduleEntry[] collapseSchedule =
    {
        new(1, 0.61f, 0.05f, 0.25f, 0.2f),
        new(2, 0.72f, 0.55f, 0.75f, 0.2f),
        new(3, 0.72f, 1.3f, 1.5f, 0.2f),
    };
    PlateStackerRound collapsed = CreatePlateStackerRound(
        1013,
        startsUtc,
        successSettings,
        collapseSchedule);
    PlayerSession collapsedPlayer = new(10, 50, "collapsed");
    collapsed.RegisterPlayer(collapsedPlayer, startsUtc);
    collapsed.Update(
        startsUtc.AddSeconds(2f),
        new[] { collapsedPlayer },
        server);
    AssertEx.Equal(0, collapsedPlayer.RoundResult.BaseScore);
    AssertEx.Equal(0, collapsedPlayer.RoundScore);
}

PlateStackerRound CreatePlateStackerRound(
    uint roundId,
    DateTime startsUtc,
    PlateStackerSettings settings,
    IReadOnlyList<PlateStackerScheduleEntry> schedule)
{
    return new PlateStackerRound(
        roundId,
        startsUtc,
        settings.DurationSeconds,
        1f,
        123u,
        settings.FirstLandingSeconds,
        settings.LastLandingSeconds,
        settings.MinimumFallDurationSeconds,
        settings.MaximumFallDurationSeconds,
        schedule,
        settings);
}

void TestHordePayload()
{
    MiniGameRoundBase round = new HordeClickerMiniGame(
        new HordeClickerSettings()).CreateRound(Context(1002, 13));
    round.ConfigureSession(8, 3, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.HordeClicker);
        uint seed = message.GetUInt();
        int characterCount = message.GetUShort();
        message.GetInt();
        float firstSpawn = message.GetFloat();
        float lastSpawn = message.GetFloat();
        float minimumTravel = message.GetFloat();
        float maximumTravel = message.GetFloat();
        float minimumY = message.GetFloat();
        float maximumY = message.GetFloat();
        int count = message.GetUShort();
        HordeClickerScheduleEntry[] expected = HordeClickerScheduleGenerator.Generate(
            seed, characterCount, firstSpawn, lastSpawn,
            minimumTravel, maximumTravel, minimumY, maximumY);
        AssertEx.Equal(expected.Length, count);
        for (int index = 0; index < count; index++)
        {
            HordeClickerScheduleEntry actual = new(
                message.GetUShort(),
                message.GetFloat(),
                message.GetFloat(),
                message.GetFloat());
            AssertEx.Equal(expected[index], actual);
        }
    }
    finally
    {
        message.Release();
    }
}

void TestRoundResultRestore()
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

void TestStopGoInputBounds()
{
    StopGoSettings settings = new()
    {
        FutureInputToleranceSeconds = 2f,
        InputGraceSeconds = 2f,
        MaximumInputSamples = 256,
    };
    StopGoRound round = (StopGoRound)new StopGoMiniGame(settings)
        .CreateRound(Context(1003, 21));
    DateTime start = DateTime.UtcNow;
    round.ScheduleStart(start);
    PlayerSession player = new(3, 43, "runner");
    Riptide.Server server = new();
    for (int index = 0; index < 300; index++)
        SendStopGo(round, player, server, index % 2 == 0, 0.01f + (index * 0.001f));
    AssertEx.Equal(256, GetSampleCount(round));

    StopGoRound coalesced = (StopGoRound)new StopGoMiniGame(settings)
        .CreateRound(Context(1004, 22));
    coalesced.ScheduleStart(DateTime.UtcNow);
    PlayerSession second = new(4, 44, "runner2");
    for (int index = 0; index < 50; index++)
        SendStopGo(coalesced, second, server, index % 2 == 0, 0.01f);
    AssertEx.Equal(1, GetSampleCount(coalesced));
}

void TestCarParkInputBounds()
{
    CarParkSettings settings = new()
    {
        LeadInSeconds = 0f,
        FutureInputToleranceSeconds = 2f,
        InputGraceSeconds = 2f,
        MaximumInputSamples = 256,
    };
    CarParkRound round = (CarParkRound)new CarParkMiniGame(settings)
        .CreateRound(Context(1005, 31));
    round.ScheduleStart(DateTime.UtcNow);
    PlayerSession player = new(5, 45, "driver");
    Riptide.Server server = new();
    for (int index = 0; index < 300; index++)
        SendCarPark(round, player, server, index % 2 == 0 ? -1f : 1f,
            0.11f + (index * 0.001f));
    AssertEx.Equal(256, GetSampleCount(round));
}

MiniGameRoundStartContext Context(uint roundId, int seed) =>
    new(roundId, DateTime.UtcNow, 10f, 10f, new Random(seed));

void SkipStartedHeader(Message message, MiniGameType expectedType)
{
    AssertEx.Equal((ulong)NetworkMessageId.MiniGameStarted, message.GetVarULong());
    message.GetUInt();
    message.GetUInt();
    message.GetUShort();
    message.GetUShort();
    AssertEx.Equal((ushort)expectedType, message.GetUShort());
    message.GetLong();
    message.GetFloat();
    message.GetFloat();
    message.GetFloat();
}

void SendStopGo(StopGoRound round, PlayerSession player, Riptide.Server server,
    bool running, float sampledAt)
{
    Message message = Message.Create().AddBool(running).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

void SendCarPark(CarParkRound round, PlayerSession player, Riptide.Server server,
    float steering, float sampledAt)
{
    Message message = Message.Create().AddFloat(steering).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

void SendSpotTheDifference(
    SpotTheDifferenceRound round,
    PlayerSession player,
    Riptide.Server server,
    byte differenceId,
    float sampledAt)
{
    Message message = Message.Create().AddByte(differenceId).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

void SendMaze(
    MazeRound round,
    PlayerSession player,
    Riptide.Server server,
    MazePoint desiredPosition,
    float sampledAt)
{
    Message message = Message.Create()
        .AddByte(1)
        .AddFloat(desiredPosition.X)
        .AddFloat(desiredPosition.Y)
        .AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

IReadOnlyList<int> FindMazeCellPath(MazeLayout layout)
{
    int[] previous = Enumerable.Repeat(-1, layout.Walls.Length).ToArray();
    Queue<int> pending = new();
    previous[layout.StartCell] = layout.StartCell;
    pending.Enqueue(layout.StartCell);
    while (pending.Count > 0)
    {
        int cell = pending.Dequeue();
        if (cell == layout.ExitCell)
            break;
        foreach (int neighbor in GetOpenMazeNeighbors(layout, cell))
        {
            if (previous[neighbor] >= 0)
                continue;
            previous[neighbor] = cell;
            pending.Enqueue(neighbor);
        }
    }

    AssertEx.True(previous[layout.ExitCell] >= 0);
    List<int> path = new();
    for (int cell = layout.ExitCell;
        cell != layout.StartCell;
        cell = previous[cell])
    {
        path.Add(cell);
    }
    path.Add(layout.StartCell);
    path.Reverse();
    return path;
}

int CountReachableCells(MazeLayout layout)
{
    HashSet<int> visited = new() { layout.StartCell };
    Queue<int> pending = new();
    pending.Enqueue(layout.StartCell);
    while (pending.Count > 0)
    {
        foreach (int neighbor in GetOpenMazeNeighbors(
            layout,
            pending.Dequeue()))
        {
            if (visited.Add(neighbor))
                pending.Enqueue(neighbor);
        }
    }
    return visited.Count;
}

IEnumerable<int> GetOpenMazeNeighbors(MazeLayout layout, int cell)
{
    int size = layout.GridSize;
    int column = cell % size;
    int row = cell / size;
    MazeWallMask walls = (MazeWallMask)layout.Walls[cell];
    if (row < size - 1 && (walls & MazeWallMask.North) == 0)
        yield return cell + size;
    if (column < size - 1 && (walls & MazeWallMask.East) == 0)
        yield return cell + 1;
    if (row > 0 && (walls & MazeWallMask.South) == 0)
        yield return cell - size;
    if (column > 0 && (walls & MazeWallMask.West) == 0)
        yield return cell - 1;
}

int GetSampleCount(object round)
{
    FieldInfo field = round.GetType().GetField(
        "playerStates", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("playerStates field was not found.");
    IEnumerable states = (IEnumerable)(field.GetValue(round)
        ?? throw new InvalidOperationException("playerStates is null."));
    foreach (object pair in states)
    {
        object state = pair.GetType().GetProperty("Value")!.GetValue(pair)!;
        ICollection samples = (ICollection)state.GetType().GetProperty("Samples")!
            .GetValue(state)!;
        return samples.Count;
    }
    return 0;
}

sealed class TestRound : MiniGameRoundBase
{
    public TestRound(DateTime start)
        : base(1, MiniGameType.SliderTarget, start, 10f, 1f) { }

    public void Complete(PlayerSession session, int score, float elapsed)
    {
        Riptide.Server server = new();
        CompleteSubmission(session, score, server, completedAtSeconds: elapsed);
    }

    public override void HandleInput(Message message, PlayerSession session,
        Riptide.Server server) { }
    public override string Describe() => string.Empty;
    protected override void WriteStartedPayload(Message message) { }
}

static class AssertEx
{
    public static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected true.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Sequences differ.");
    }

    public static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
