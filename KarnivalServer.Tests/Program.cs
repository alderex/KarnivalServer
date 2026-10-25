using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
    private static string serverRoot = string.Empty;
    private static string clientRoot = string.Empty;

    public static int Main(string[] args)
    {
        serverRoot = args.Length > 0
            ? Path.GetFullPath(args[0])
            : FindServerRoot();
        clientRoot = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.GetFullPath(Path.Combine(serverRoot, "..", "..", "Unity Projects", "KarnivalClient"));

        (string Name, Action Test)[] tests =
        {
    ("Every game finalizes timeout at 1x without wall-clock dependence", TestEveryGameTimeout),
    ("Playable intervals exclude buffers and exhaust at 1x", TestPlayableTimeline),
    ("Every round uses the shared client header codec", TestSharedRoundPackets),
    ("Plate payload and shared geometry agree", TestSharedPlatePacket),
    ("CarPark timeout drains playable time and awards 1x", TestCarParkTimeoutTimeline),
    ("Archery scoring uses impact time, not tick processing time", TestArcheryEventTimestamp),
    ("Maze authority and prediction use identical movement", TestSharedMazePrediction),
    ("Directional replay handles snapshots and reset", TestDirectionalReplay),
    ("Betting ledger validates, settles, and refunds wagers", TestBettingLedger),
    ("Default configuration is valid", TestDefaultConfiguration),
    ("Invalid configurations fail early", TestInvalidConfiguration),
    ("Generated protocols match the schema", TestGeneratedProtocols),
    ("Mid-round logins wait and can chat until the next game",
        TestMidRoundLoginWaitsForNextGame),
    ("Maze layouts are deterministic, connected, and perfect", TestMazeLayout),
    ("Maze payload contains the authoritative layout", TestMazePayload),
    ("Maze movement blocks walls and slides along them", TestMazeMovement),
    ("Maze completion and timeout scoring are authoritative", TestMazeScoring),
    ("ObstacleRunner fixed finish has no speed bonus", TestObstacleRunnerSpeedMultiplier),
    ("Falling-object schedules are deterministic", TestFallingScheduleDeterminism),
    ("PlateStacker schedules are deterministic and evenly paced", TestPlateStackerSchedule),
    ("Horde schedules are deterministic", TestHordeScheduleDeterminism),
    ("SmackMan schedules are deterministic and bounded", TestSmackManSchedule),
    ("Falling-object payload contains authoritative schedule", TestFallingPayload),
    ("PlateStacker payload contains authoritative rules and schedule", TestPlateStackerPayload),
    ("Horde payload contains authoritative schedule", TestHordePayload),
    ("SmackMan payload contains authoritative schedule", TestSmackManPayload),
    ("SmackMan validates clicks and scores at 1x", TestSmackManScoring),
    ("Where's Baldo payload contains the authoritative crowd", TestWheresBaldoPayload),
    ("Where's Baldo validates selections and scores independently", TestWheresBaldoScoring),
    ("WheelSpinner payload and angle mapping are deterministic", TestWheelSpinnerPayloadAndAngles),
    ("WheelSpinner stop and timeout awards are authoritative", TestWheelSpinnerScoring),
    ("Step Into Traffic schedules and payloads are deterministic", TestStepIntoTrafficPayload),
    ("Step Into Traffic finish and timeout scoring are authoritative", TestStepIntoTrafficScoring),
    ("PotatoFace targets are deterministic and valid", TestPotatoFaceTargets),
    ("PotatoFace scoring rewards matching accuracy", TestPotatoFaceScoring),
    ("PotatoFace payload contains the target face", TestPotatoFacePayload),
    ("SpotTheDifference subsets are deterministic and valid", TestSpotTheDifferenceSubset),
    ("SpotTheDifference payload contains authoritative scoring", TestSpotTheDifferencePayload),
    ("SpotTheDifference validates selections and scores partial progress", TestSpotTheDifferenceScoring),
    ("PlateStacker catches, misses, collapse, and scoring are authoritative", TestPlateStackerScoring),
    ("PlateStacker evaluates delayed batches at each landing time", TestPlateStackerDelayedBatchEvaluation),
    ("PlateStacker stack height controls completion and misses wait for exit", TestPlateStackerContactTiming),
    ("Speed bonus includes the final second", TestFinalSecondBonus),
    ("Scheduled game exhaustion awards 1x", TestScheduledGameBonusLimits),
    ("Round-result metadata survives reconnect restore", TestRoundResultRestore),
    ("StopGo input history is bounded and coalesced", TestStopGoInputBounds),
    ("CarPark never targets a center parking space", TestCarParkTargetSelection),
    ("CarPark collision footprint is inset from the car artwork",
        TestCarParkCollisionFootprint),
    ("CarPark centered angled entry is accepted", TestCarParkParkingCapture),
    ("CarPark inset hitboxes allow clearance without permitting overlap",
        TestCarParkInsetCollision),
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
    }


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
        Riptide.Server server)
    { }
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
