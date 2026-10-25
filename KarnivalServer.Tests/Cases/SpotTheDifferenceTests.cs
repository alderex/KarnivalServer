using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestSpotTheDifferencePayload()
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

static void TestSpotTheDifferenceSubset()
{
    byte[] first = SpotTheDifferenceMiniGame.SelectDifferenceIds(
        6,
        3,
        new Random(2468));
    byte[] second = SpotTheDifferenceMiniGame.SelectDifferenceIds(
        6,
        3,
        new Random(2468));
    AssertEx.SequenceEqual(first, second);
    AssertEx.Equal(3, first.Length);
    AssertEx.Equal(3, first.Distinct().Count());
    AssertEx.True(first.All(differenceId => differenceId < 6));
}

static void TestSpotTheDifferenceScoring()
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
    byte unselected = Enumerable.Range(0, 6)
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


}
