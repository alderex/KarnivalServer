using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestPlateStackerSchedule()
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

static void TestPlateStackerPayload()
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
        AssertEx.Equal(0.15f, message.GetFloat());
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

static void TestPlateStackerScoring()
{
    AssertEx.True(PlateStackerRound.HasVisibleOverlap(
        0f, 1f, 0.999f, 1f));
    AssertEx.True(!PlateStackerRound.HasVisibleOverlap(
        0f, 1f, 1f, 1f));
    AssertEx.True(PlateStackerRound.HasVisibleOverlap(
        0f, 0.15f, 0.1349f, 0.12f));
    AssertEx.True(!PlateStackerRound.HasVisibleOverlap(
        0f, 0.15f, 0.135f, 0.12f));
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
        new(1, 0.63f, 0.05f, 0.25f, 0.2f),
        new(2, 0.63f, 0.55f, 0.75f, 0.2f),
        new(3, 0.63f, 1.3f, 1.5f, 0.2f),
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
    AssertEx.Equal(33, successfulPlayer.RoundScore);

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


static void TestPlateStackerContactTiming()
{
    DateTime starts = DateTime.UtcNow;
    Riptide.Server server = new();
    PlateStackerSettings settings = new()
    {
        DurationSeconds = 2f, PlateCount = 3, InputGraceSeconds = 0f,
        FutureInputToleranceSeconds = 10f,
    };
    PlateStackerScheduleEntry[] schedule =
    {
        new(1, 0.63f, 0.05f, 0.25f, 0.2f),
        new(2, 0.63f, 0.55f, 0.75f, 0.2f),
        new(3, 0.63f, 1.3f, 1.5f, 0.2f),
    };
    var round = CreatePlateStackerRound(2010, starts, settings, schedule);
    PlayerSession tall = new(81, 81, "tall");
    PlayerSession shortStack = new(82, 82, "short");
    round.RegisterPlayer(tall, starts);
    round.RegisterPlayer(shortStack, starts);
    // Miss only the first plate, then return the tray to the center.
    SendPlateStackerMovement(round, shortStack, server, -1, 0f);
    SendPlateStackerMovement(round, shortStack, server, 1, 0.25f);
    SendPlateStackerMovement(round, shortStack, server, 0, 0.5f);
    var players = new[] { tall, shortStack };
    // Final contact: tall stack 1.47672s, shorter stack 1.48836s.
    round.Update(starts.AddSeconds(1.48f), players, server);
    AssertEx.True(tall.SubmittedThisRound);
    AssertEx.True(!shortStack.SubmittedThisRound);
    round.Update(starts.AddSeconds(1.49f), players, server);
    AssertEx.Equal(30, tall.RoundResult.BaseScore);
    AssertEx.Equal(20, shortStack.RoundResult.BaseScore);
    AssertEx.True(tall.RoundResult.SpeedMultiplier > shortStack.RoundResult.SpeedMultiplier);

    // A late tick must produce the same result as the incremental updates.
    var batch = CreatePlateStackerRound(2011, starts, settings, schedule);
    PlayerSession batched = new(83, 83, "batch");
    batch.RegisterPlayer(batched, starts);
    batch.Update(starts.AddSeconds(2), new[] { batched }, server);
    AssertEx.Equal(tall.RoundResult, batched.RoundResult);

    var miss = CreatePlateStackerRound(2012, starts,
        new PlateStackerSettings { DurationSeconds = 2f, PlateCount = 1, InputGraceSeconds = 0f },
        new[] { new PlateStackerScheduleEntry(1, 0.9f, 0.05f, 0.25f, 0.2f) });
    PlayerSession missed = new(84, 84, "miss");
    miss.RegisterPlayer(missed, starts);
    miss.Update(starts.AddSeconds(0.26), new[] { missed }, server);
    AssertEx.True(!missed.SubmittedThisRound);
    miss.Update(starts.AddSeconds(0.32), new[] { missed }, server);
    AssertEx.True(missed.SubmittedThisRound);
    AssertEx.Equal(0, missed.RoundResult.BaseScore);
    AssertEx.Equal(1f, missed.RoundResult.SpeedMultiplier);

    // Retain a nonzero score, miss the final plate, and consume the full
    // playable schedule even though the round timer still has time left.
    var finalMiss = CreatePlateStackerRound(2014, starts, settings,
        new[] {
            new PlateStackerScheduleEntry(1, 0.5f, 0.05f, 0.25f, 0.2f),
            new PlateStackerScheduleEntry(2, 0.9f, 0.55f, 0.75f, 0.2f),
        });
    PlayerSession finalMissPlayer = new(86, 86, "final-miss");
    finalMiss.RegisterPlayer(finalMissPlayer, starts);
    finalMiss.Update(starts.AddSeconds(1), new[] { finalMissPlayer }, server);
    AssertEx.Equal(10, finalMissPlayer.RoundResult.BaseScore);
    AssertEx.Equal(10, finalMissPlayer.RoundScore);
    AssertEx.Equal(1f, finalMissPlayer.RoundResult.SpeedMultiplier);
}

static void TestPlateStackerDelayedBatchEvaluation()
{
    DateTime startsUtc = DateTime.UtcNow;
    PlateStackerSettings settings = new()
    {
        DurationSeconds = 2f,
        PlateCount = 2,
        FirstLandingSeconds = 0.25f,
        LastLandingSeconds = 0.5f,
        MinimumFallDurationSeconds = 0.2f,
        MaximumFallDurationSeconds = 0.2f,
        StackSpeedNormalizedPerSecond = 0.5f,
        PlateWidthNormalized = 0.12f,
        InputGraceSeconds = 0.5f,
        PointsPerPlate = 10,
    };
    PlateStackerScheduleEntry[] schedule =
    {
        new(1, 0.625f, 0.05f, 0.25f, 0.2f),
        new(2, 0.75f, 0.3f, 0.5f, 0.2f),
    };
    PlateStackerRound round = CreatePlateStackerRound(
        1014,
        startsUtc,
        settings,
        schedule);
    PlayerSession player = new(11, 51, "delayed-stacker");
    Riptide.Server server = new();
    round.RegisterPlayer(player, startsUtc);
    SendPlateStackerMovement(round, player, server, 1, 0f);

    // One delayed server tick evaluates both plates. Each evaluation must use
    // its own landing timestamp rather than the time of this update.
    round.Update(
        startsUtc.AddSeconds(1.2f),
        new[] { player },
        server);

    AssertEx.Equal(20, player.RoundResult.BaseScore);
}
 
static PlateStackerRound CreatePlateStackerRound(
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


}
