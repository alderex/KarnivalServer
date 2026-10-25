using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestSmackManSchedule()
{
    const float activeDuration = 2.62f;
    SmackManScheduleEntry[] first = SmackManScheduleGenerator.Generate(
        0x12345678u, 4, 5, 1f, 4f, activeDuration);
    SmackManScheduleEntry[] second = SmackManScheduleGenerator.Generate(
        0x12345678u, 4, 5, 1f, 4f, activeDuration);
    AssertEx.SequenceEqual(first, second);
    AssertEx.Equal(20, first.Length);
    float[] expectedWaveTimes = { 1f, 5f, 9f, 13f };
    for (int waveIndex = 0; waveIndex < expectedWaveTimes.Length; waveIndex++)
    {
        SmackManScheduleEntry[] wave = first
            .Skip(waveIndex * 5)
            .Take(5)
            .ToArray();
        AssertEx.True(wave.All(entry =>
            entry.SpawnSeconds == expectedWaveTimes[waveIndex]));
        AssertEx.Equal(5, wave.Select(entry => entry.HoleIndex).Distinct().Count());
    }
    AssertEx.True(first.All(entry =>
        entry.HoleIndex < SmackManScheduleGenerator.HoleCount));

    foreach (SmackManScheduleEntry entry in first)
    {
        SmackManScheduleEntry[] active = first
            .Where(candidate =>
                candidate.SpawnSeconds <= entry.SpawnSeconds &&
                candidate.SpawnSeconds + activeDuration > entry.SpawnSeconds)
            .ToArray();
        AssertEx.True(active.Length <= 5);
        AssertEx.Equal(active.Length, active.Select(candidate => candidate.HoleIndex).Distinct().Count());
    }
}

static void TestSmackManPayload()
{
    SmackManRound round = (SmackManRound)new SmackManMiniGame(
        new SmackManSettings()).CreateRound(Context(1017, 81));
    round.ConfigureSession(15, 7, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.SmackMan);
        message.GetUInt();
        AssertEx.Equal(5, message.GetInt());
        AssertEx.Equal(0.12f, message.GetFloat());
        AssertEx.Equal(2f, message.GetFloat());
        AssertEx.Equal(0.5f, message.GetFloat());
        int count = message.GetUShort();
        AssertEx.Equal(20, count);
        AssertEx.Equal(round.Schedule.Count, count);
        for (int index = 0; index < count; index++)
        {
            SmackManScheduleEntry expected = round.Schedule[index];
            AssertEx.Equal(expected.AppearanceId, message.GetUShort());
            AssertEx.Equal(expected.HoleIndex, message.GetByte());
            AssertEx.Equal(expected.SpawnSeconds, message.GetFloat());
        }
        AssertEx.Equal(100, round.MaximumScore);
    }
    finally
    {
        message.Release();
    }
}

static void TestSmackManScoring()
{
    SmackManSettings settings = new()
    {
        DurationSeconds = 2f,
        GroupCount = 1,
        HeadsPerGroup = 1,
        PointsPerHit = 5,
        FirstGroupSeconds = 0.4f,
        GroupIntervalSeconds = 1f,
        RiseDurationSeconds = 0.1f,
        HoldDurationSeconds = 0.4f,
        RetractDurationSeconds = 0.5f,
        InputGraceSeconds = 0.5f,
        FutureInputToleranceSeconds = 0.1f,
    };
    SmackManScheduleEntry[] schedule = { new(1, 3, 0.4f) };
    DateTime startsUtc = DateTime.UtcNow.AddSeconds(-1.2f);
    Riptide.Server server = new();
    SmackManRound round = new(
        1018,
        startsUtc,
        settings.DurationSeconds,
        1f,
        123u,
        settings.PointsPerHit,
        settings.RiseDurationSeconds,
        settings.HoldDurationSeconds,
        settings.RetractDurationSeconds,
        schedule,
        settings);
    PlayerSession hitPlayer = new(12, 52, "smacker");
    round.RegisterPlayer(hitPlayer, startsUtc);
    SendSmack(round, hitPlayer, server, 1, 0.8f);
    SendSmack(round, hitPlayer, server, 1, 0.8f);
    AssertEx.True(hitPlayer.SubmittedThisRound);
    AssertEx.Equal(5, hitPlayer.RoundResult.BaseScore);
    AssertEx.Equal(5, hitPlayer.RoundScore);
    AssertEx.Equal(5, hitPlayer.RoundResult.MaximumScore);
    AssertEx.Equal(1f, hitPlayer.RoundResult.SpeedMultiplier);

    PlayerSession earlyPlayer = new(14, 54, "early");
    round.RegisterPlayer(earlyPlayer, startsUtc);
    SendSmack(round, earlyPlayer, server, 1, 0.2f);
    round.FinalizeRound(new[] { earlyPlayer }, server);
    AssertEx.True(earlyPlayer.SubmittedThisRound);
    AssertEx.Equal(0, earlyPlayer.RoundScore);

    // Receipt is delayed until 1.2s, but scoring uses the timestamp inside the hold.
    AssertEx.True(Karnival.Rules.SmackManRules.IsClickable(0.5f, 0.4f, 0.1f, 0.4f));
    AssertEx.True(!Karnival.Rules.SmackManRules.IsClickable(0.45f, 0.4f, 0.1f, 0.4f));
    AssertEx.True(!Karnival.Rules.SmackManRules.IsClickable(0.9f, 0.4f, 0.1f, 0.4f));
    AssertEx.True(!Karnival.Rules.SmackManRules.IsClickable(float.NaN, 0.4f, 0.1f, 0.4f));
    foreach (float timestamp in new[] { 0.45f, 0.9f, 1.2f })
    {
        round.Clock = new ManualClock { UtcNow = startsUtc.AddSeconds(timestamp + 0.02f) };
        PlayerSession invalidPlayer = new((ushort)(100 + timestamp * 100), 100, "invalid");
        round.RegisterPlayer(invalidPlayer, startsUtc);
        SendSmack(round, invalidPlayer, server, 1, timestamp);
        round.FinalizeRound(new[] { invalidPlayer }, server);
        AssertEx.Equal(0, invalidPlayer.RoundScore);
    }

    foreach (float timestamp in new[] { 0.5f, 0.899f })
    {
        round.Clock = new ManualClock { UtcNow = startsUtc.AddSeconds(timestamp + 0.02f) };
        PlayerSession boundaryPlayer = new((ushort)(300 + timestamp * 100), 200, "boundary");
        round.RegisterPlayer(boundaryPlayer, startsUtc);
        SendSmack(round, boundaryPlayer, server, 1, timestamp);
        AssertEx.True(boundaryPlayer.SubmittedThisRound);
        AssertEx.Equal(5, boundaryPlayer.RoundScore);
    }

    PlayerSession missedPlayer = new(13, 53, "missed");
    round.RegisterPlayer(missedPlayer, startsUtc);
    round.FinalizeRound(new[] { missedPlayer }, server);
    AssertEx.True(missedPlayer.SubmittedThisRound);
    AssertEx.Equal(0, missedPlayer.RoundScore);
    AssertEx.Equal(1f, missedPlayer.RoundResult.SpeedMultiplier);

    AssertEx.True(SmackManRound.IsVisible(schedule[0], 0.4f, 1f));
    AssertEx.True(SmackManRound.IsVisible(schedule[0], 0.45f, 1f));
    AssertEx.True(SmackManRound.IsVisible(schedule[0], 0.75f, 1f));
    AssertEx.True(SmackManRound.IsVisible(schedule[0], 1.2f, 1f));
    AssertEx.True(SmackManRound.IsVisible(schedule[0], 1.399f, 1f));
    AssertEx.True(!SmackManRound.IsVisible(schedule[0], 0.399f, 1f));
    AssertEx.True(!SmackManRound.IsVisible(schedule[0], 1.4f, 1f));
}


}
