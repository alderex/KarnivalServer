using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestWheelSpinnerPayloadAndAngles()
{
    WheelSpinnerSettings settings = new();
    WheelSpinnerRound round = (WheelSpinnerRound)new WheelSpinnerMiniGame(settings)
        .CreateRound(Context(1020, 73));
    round.ConfigureSession(19, 4, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.WheelSpinner);
        AssertEx.Equal(round.InitialRotationDegrees, message.GetFloat());
        AssertEx.Equal(240f, message.GetFloat());
        AssertEx.Equal((byte)10, message.GetByte());
        float totalDegrees = 0f;
        for (int index = 0; index < settings.Slices.Length; index++)
        {
            AssertEx.Equal(settings.Slices[index].Points, message.GetInt());
            float arcDegrees = message.GetFloat();
            AssertEx.Equal(settings.Slices[index].ArcDegrees, arcDegrees);
            totalDegrees += arcDegrees;
        }
        AssertEx.Equal(360f, totalDegrees);
    }
    finally
    {
        message.Release();
    }

    WheelSpinnerSlice[] slices = settings.Slices
        .Select(slice => new WheelSpinnerSlice(slice.Points, slice.ArcDegrees))
        .ToArray();
    AssertEx.Equal(0, WheelSpinnerRound.GetSliceIndex(0f, slices));
    AssertEx.Equal(1, WheelSpinnerRound.GetSliceIndex(-48f, slices));
    AssertEx.Equal(9, WheelSpinnerRound.GetSliceIndex(12f, slices));
    AssertEx.Equal(100, slices[
        WheelSpinnerRound.GetSliceIndex(12f, slices)].Points);
    AssertEx.Equal(120f,
        WheelSpinnerRound.CalculateRotationDegrees(0f, 240f, 0.5f));
    AssertEx.Equal(0f, WheelSpinnerRound.NormalizeDegrees(360f));
}

static void TestWheelSpinnerScoring()
{
    WheelSpinnerSettings settings = new()
    {
        AngularSpeedDegreesPerSecond = 240f,
        FutureInputToleranceSeconds = 2f,
        InputGraceSeconds = 0.5f,
    };
    WheelSpinnerSlice[] slices = settings.Slices
        .Select(slice => new WheelSpinnerSlice(slice.Points, slice.ArcDegrees))
        .ToArray();
    Riptide.Server server = new();

    WheelSpinnerRound stoppedRound = new(
        1021, DateTime.UtcNow, settings.DurationSeconds, 4f,
        12f, settings.AngularSpeedDegreesPerSecond, slices, settings);
    PlayerSession stopped = new(20, 58, "stopped", isSimulated: true);
    SendWheelSpinner(stoppedRound, stopped, server, 0f);
    AssertEx.True(stopped.SubmittedThisRound);
    AssertEx.Equal(100, stopped.RoundResult.BaseScore);
    AssertEx.Equal(100, stopped.RoundScore);
    AssertEx.Equal(1f, stopped.RoundResult.SpeedMultiplier);
    SendWheelSpinner(stoppedRound, stopped, server, 0.1f);
    AssertEx.Equal(100, stopped.RoundScore);

    WheelSpinnerRound timedRound = new(
        1022, DateTime.UtcNow, settings.DurationSeconds, 4f,
        0f, settings.AngularSpeedDegreesPerSecond, slices, settings);
    PlayerSession timedOut = new(21, 59, "timeout", isSimulated: true);
    timedRound.FinalizeRound(new[] { timedOut }, server);
    int timedSlice = WheelSpinnerRound.GetSliceIndex(
        WheelSpinnerRound.CalculateRotationDegrees(
            0f, settings.AngularSpeedDegreesPerSecond,
            settings.DurationSeconds),
        slices);
    AssertEx.True(timedOut.SubmittedThisRound);
    AssertEx.Equal(slices[timedSlice].Points,
        timedOut.RoundResult.BaseScore);
    AssertEx.Equal(1f, timedOut.RoundResult.SpeedMultiplier);
}


}
