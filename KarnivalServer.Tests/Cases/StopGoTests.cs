using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestStopGoInputBounds()
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


}
