using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestWheresBaldoPayload()
{
    AssertEx.Equal(100, WheresBaldoMiniGame.CharacterCount);
    WheresBaldoRound round = (WheresBaldoRound)new WheresBaldoMiniGame(
        new WheresBaldoSettings()).CreateRound(Context(1018, 71));
    round.ConfigureSession(15, 7, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.WheresBaldo);
        AssertEx.Equal(round.CrowdSeed, message.GetUInt());
        AssertEx.Equal(round.BaldoCharacterId, message.GetByte());
        AssertEx.True(
            round.BaldoCharacterId < WheresBaldoMiniGame.CharacterCount);
        AssertEx.Equal(100, round.MaximumScore);
    }
    finally
    {
        message.Release();
    }
}

static void TestWheresBaldoScoring()
{
    WheresBaldoSettings settings = new()
    {
        FutureInputToleranceSeconds = 2f,
        InputGraceSeconds = 0.5f,
    };
    WheresBaldoRound round = (WheresBaldoRound)new WheresBaldoMiniGame(settings)
        .CreateRound(Context(1019, 72));
    round.ScheduleStart(DateTime.UtcNow);
    Riptide.Server server = new();

    byte wrongOne = Enumerable.Range(0, WheresBaldoMiniGame.CharacterCount)
        .Select(value => (byte)value)
        .First(characterId => characterId != round.BaldoCharacterId);
    byte wrongTwo = Enumerable.Range(
            wrongOne + 1,
            WheresBaldoMiniGame.CharacterCount)
        .Select(value => (byte)(value % WheresBaldoMiniGame.CharacterCount))
        .First(characterId => characterId != round.BaldoCharacterId &&
            characterId != wrongOne);

    PlayerSession penalized = new(15, 55, "penalized");
    SendWheresBaldo(round, penalized, server, wrongOne, 0f);
    SendWheresBaldo(round, penalized, server, wrongOne, 0f);
    SendWheresBaldo(round, penalized, server, 200, 0f);
    SendWheresBaldo(round, penalized, server, wrongTwo, 0f);
    SendWheresBaldo(round, penalized, server, round.BaldoCharacterId, 0f);
    AssertEx.True(penalized.SubmittedThisRound);
    AssertEx.Equal(80, penalized.RoundResult.BaseScore);
    AssertEx.Equal(240, penalized.RoundScore);

    PlayerSession perfect = new(16, 56, "perfect");
    SendWheresBaldo(round, perfect, server, round.BaldoCharacterId, 0f);
    AssertEx.Equal(100, perfect.RoundResult.BaseScore);
    AssertEx.Equal(300, perfect.RoundScore);

    PlayerSession timedOut = new(17, 57, "timeout");
    round.FinalizeRound(new[] { timedOut }, server);
    AssertEx.True(timedOut.SubmittedThisRound);
    AssertEx.Equal(0, timedOut.RoundScore);
}


}
