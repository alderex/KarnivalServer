using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestObstacleRunnerSpeedMultiplier()
{
    const float durationSeconds = 12f;
    ObstacleRunnerSettings settings = new()
    {
        InputGraceSeconds = 0f,
    };
    DateTime startsUtc = DateTime.UtcNow.AddSeconds(-durationSeconds);
    ObstacleScheduleEntry[] schedule =
    {
        new(1, ObstacleHeight.High, durationSeconds * 0.9f),
    };
    ObstacleRunnerRound round = new(
        1014,
        startsUtc,
        durationSeconds,
        1f,
        schedule,
        settings);
    PlayerSession player = new(11, 51, "runner");
    Riptide.Server server = new();

    round.RegisterPlayer(player, startsUtc);
    round.Update(
        startsUtc.AddSeconds(schedule[0].CollisionSeconds),
        new[] { player },
        server);

    AssertEx.True(player.SubmittedThisRound);
    AssertEx.Equal(100, player.RoundResult.BaseScore);
    AssertEx.Equal(100, player.RoundScore);
    AssertEx.Equal(1f, player.RoundResult.SpeedMultiplier);
}


}
