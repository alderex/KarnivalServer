using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestCarParkInputBounds()
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

static void TestCarParkTargetSelection()
{
    FieldInfo emptySpotField = typeof(CarParkRound).GetField(
        "emptySpot", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark emptySpot field was not found.");

    for (int spotCount = 3; spotCount <= 12; spotCount++)
    {
        CarParkMiniGame game = new(new CarParkSettings { ParkingSpotCount = spotCount });
        for (int seed = 0; seed < 256; seed++)
        {
            CarParkRound round = (CarParkRound)game.CreateRound(Context(2000, seed));
            byte targetSpot = (byte)(emptySpotField.GetValue(round)
                ?? throw new InvalidOperationException("CarPark emptySpot is null."));

            AssertEx.True(targetSpot < spotCount);
            if (spotCount % 2 == 1)
                AssertEx.True(targetSpot != spotCount / 2);
        }
    }
}

static void TestCarParkCollisionFootprint()
{
    CarParkSettings settings = new();
    CarParkRound round = (CarParkRound)new CarParkMiniGame(settings)
        .CreateRound(Context(2001, 17));
    FieldInfo emptySpotField = typeof(CarParkRound).GetField(
        "emptySpot", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark emptySpot field was not found.");
    MethodInfo evaluatePose = typeof(CarParkRound).GetMethod(
        "EvaluatePose", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark EvaluatePose was not found.");
    byte emptySpot = (byte)(emptySpotField.GetValue(round)
        ?? throw new InvalidOperationException("CarPark emptySpot is null."));
    int parkedSlot = emptySpot == 0 ? 1 : 0;
    float parkedX = settings.FirstParkingX +
        ((settings.LastParkingX - settings.FirstParkingX) * parkedSlot /
            (settings.ParkingSpotCount - 1));

    object? separated = evaluatePose.Invoke(round, new object[]
    {
        new CarParkPose(parkedX, settings.ParkingRowY - 0.22f, 0f),
    });
    object? touching = evaluatePose.Invoke(round, new object[]
    {
        new CarParkPose(parkedX, settings.ParkingRowY - 0.2f, 0f),
    });

    AssertEx.True(separated is null);
    AssertEx.Equal(CarParkOutcomeReason.Collision,
        (CarParkOutcomeReason)(touching
            ?? throw new InvalidOperationException("Expected a collision.")));
}

static void TestCarParkParkingCapture()
{
    CarParkSettings settings = new();
    CarParkRound round = (CarParkRound)new CarParkMiniGame(settings)
        .CreateRound(Context(2002, 23));
    FieldInfo emptySpotField = typeof(CarParkRound).GetField(
        "emptySpot", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark emptySpot field was not found.");
    MethodInfo evaluatePose = typeof(CarParkRound).GetMethod(
        "EvaluatePose", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark EvaluatePose was not found.");
    byte emptySpot = (byte)(emptySpotField.GetValue(round)
        ?? throw new InvalidOperationException("CarPark emptySpot is null."));
    float targetX = settings.FirstParkingX +
        ((settings.LastParkingX - settings.FirstParkingX) * emptySpot /
            (settings.ParkingSpotCount - 1));

    object? early = evaluatePose.Invoke(round, new object[]
    {
        new CarParkPose(
            targetX,
            settings.ParkingRowY - 0.05f,
            15f),
    });
    object? result = evaluatePose.Invoke(round, new object[]
    {
        new CarParkPose(targetX, settings.ParkingRowY, 15f),
    });

    AssertEx.True(early is null);
    AssertEx.Equal(CarParkOutcomeReason.Parked,
        (CarParkOutcomeReason)(result
            ?? throw new InvalidOperationException("Expected the car to park.")));
}

static void TestCarParkInsetCollision()
{
    CarParkSettings settings = new();
    CarParkRound round = (CarParkRound)new CarParkMiniGame(settings)
        .CreateRound(Context(2003, 29));
    FieldInfo emptySpotField = typeof(CarParkRound).GetField(
        "emptySpot", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark emptySpot field was not found.");
    MethodInfo evaluatePose = typeof(CarParkRound).GetMethod(
        "EvaluatePose", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CarPark EvaluatePose was not found.");
    byte emptySpot = (byte)(emptySpotField.GetValue(round)
        ?? throw new InvalidOperationException("CarPark emptySpot is null."));
    float targetX = settings.FirstParkingX +
        ((settings.LastParkingX - settings.FirstParkingX) * emptySpot /
            (settings.ParkingSpotCount - 1));
    int parkedSlot = emptySpot == 0 ? 1 : emptySpot - 1;
    float parkedX = settings.FirstParkingX +
        ((settings.LastParkingX - settings.FirstParkingX) * parkedSlot /
            (settings.ParkingSpotCount - 1));

    object? maneuvering = evaluatePose.Invoke(round, new object[]
    {
        new CarParkPose(targetX, settings.ParkingRowY, 30f),
    });
    float towardParkedCar = MathF.Sign(parkedX - targetX);
    object? overlapping = evaluatePose.Invoke(round, new object[]
    {
        new CarParkPose(
            targetX + (towardParkedCar * 0.15f),
            settings.ParkingRowY,
            30f),
    });

    AssertEx.True(maneuvering is null);
    AssertEx.Equal(CarParkOutcomeReason.Collision,
        (CarParkOutcomeReason)(overlapping
            ?? throw new InvalidOperationException("Expected a collision.")));
}
 
static MiniGameRoundStartContext Context(uint roundId, int seed) =>
    new(roundId, DateTime.UtcNow, 10f, 10f, new Random(seed));


}
