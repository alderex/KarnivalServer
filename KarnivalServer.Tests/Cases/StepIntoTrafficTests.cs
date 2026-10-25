using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestStepIntoTrafficPayload()
{
    StepIntoTrafficSettings settings = new();
    StepIntoTrafficRound first = (StepIntoTrafficRound)new StepIntoTrafficMiniGame(settings)
        .CreateRound(Context(1023, 74));
    StepIntoTrafficRound second = (StepIntoTrafficRound)new StepIntoTrafficMiniGame(settings)
        .CreateRound(Context(1023, 74));
    AssertEx.Equal(6, first.Lanes.Count);
    AssertEx.SequenceEqual(first.Lanes, second.Lanes);
    for (int index = 0; index < first.Lanes.Count; index++)
    {
        StepIntoTrafficLane lane = first.Lanes[index];
        AssertEx.Equal((byte)(index + 1), lane.LaneIndex);
        AssertEx.Equal(index % 2 == 0, lane.MovesLeftToRight);
        AssertEx.True(lane.SpeedNormalizedPerSecond >=
            settings.MinimumCarSpeedNormalizedPerSecond);
        AssertEx.True(lane.SpeedNormalizedPerSecond <=
            settings.MaximumCarSpeedNormalizedPerSecond);
    }

    first.ConfigureSession(21, 5, 20);
    Message message = first.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.StepIntoTraffic);
        AssertEx.Equal(settings.HopDurationSeconds, message.GetFloat());
        AssertEx.Equal(settings.CollisionRecoverySeconds, message.GetFloat());
        AssertEx.Equal(settings.CarHalfWidthNormalized, message.GetFloat());
        AssertEx.Equal(settings.CarHalfHeightRows, message.GetFloat());
        AssertEx.Equal(settings.PedestrianHalfWidthNormalized, message.GetFloat());
        AssertEx.Equal(settings.PedestrianHalfHeightRows, message.GetFloat());
        AssertEx.Equal((byte)settings.LaneCount, message.GetByte());
        foreach (StepIntoTrafficLane lane in first.Lanes)
        {
            AssertEx.Equal(lane.LaneIndex, message.GetByte());
            AssertEx.Equal(lane.MovesLeftToRight, message.GetBool());
            AssertEx.Equal(lane.SpeedNormalizedPerSecond, message.GetFloat());
            AssertEx.Equal(lane.FirstCenterCrossingSeconds, message.GetFloat());
            AssertEx.Equal(lane.CrossingIntervalSeconds, message.GetFloat());
        }
    }
    finally
    {
        message.Release();
    }
}

static void TestStepIntoTrafficScoring()
{
    StepIntoTrafficSettings settings = new()
    {
        HopDurationSeconds = 0.1f,
        InputGraceSeconds = 10f,
        FutureInputToleranceSeconds = 10f,
    };
    DateTime startsUtc = DateTime.UtcNow.AddSeconds(-0.05f);
    Riptide.Server server = new();
    StepIntoTrafficRound finishedRound = new(
        1024, startsUtc, settings.DurationSeconds, 4f,
        Array.Empty<StepIntoTrafficLane>(), settings);
    PlayerSession finished = new(22, 60, "finished", isSimulated: true);
    SendStepIntoTraffic(finishedRound, finished, server, 0.1f);
    SendStepIntoTraffic(finishedRound, finished, server, 0.25f);
    AssertEx.True(finished.SubmittedThisRound);
    AssertEx.Equal(settings.CorrectScore, finished.RoundResult.BaseScore);

    StepIntoTrafficRound timedRound = new(
        1025, startsUtc, settings.DurationSeconds, 4f,
        Array.Empty<StepIntoTrafficLane>(), settings);
    PlayerSession timedOut = new(23, 61, "timeout", isSimulated: true);
    timedRound.FinalizeRound(new[] { timedOut }, server);
    AssertEx.True(timedOut.SubmittedThisRound);
    AssertEx.Equal(0, timedOut.RoundResult.BaseScore);
}

}
