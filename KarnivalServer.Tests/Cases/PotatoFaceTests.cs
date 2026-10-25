using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestPotatoFaceTargets()
{
    PotatoFaceTarget first = PotatoFaceTargetGenerator.Generate(new Random(4567));
    PotatoFaceTarget second = PotatoFaceTargetGenerator.Generate(new Random(4567));
    AssertEx.Equal(first, second);
    AssertEx.True(first.EyesVariant is PotatoFacePartVariant.One or PotatoFacePartVariant.Two);
    AssertEx.True(first.MouthVariant is PotatoFacePartVariant.One or PotatoFacePartVariant.Two);
    AssertEx.True(first.NoseVariant is PotatoFacePartVariant.One or PotatoFacePartVariant.Two);
}

static void TestPotatoFaceScoring()
{
    PotatoFaceSettings settings = new() { PositionToleranceNormalized = 0.4f };
    PotatoFacePosition exact = new(0f, 0f);
    PotatoFacePosition halfway = new(0.2f, 0f);
    PotatoFaceTarget target = new(
        PotatoFacePartVariant.One,
        PotatoFacePartVariant.Two,
        PotatoFacePartVariant.One);

    AssertEx.Equal(100, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        settings));
    AssertEx.Equal(50, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, halfway,
        PotatoFacePartVariant.One, halfway,
        PotatoFacePartVariant.Two, halfway,
        PotatoFacePartVariant.One, halfway,
        settings));
    AssertEx.Equal(0, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.Two, exact,
        settings));
    AssertEx.Equal(60, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        settings));
    AssertEx.Equal(80, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.One, exact,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.Two, exact,
        PotatoFacePartVariant.One, exact,
        settings));
    AssertEx.Equal(0, PotatoFaceRound.CalculateScore(
        target,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.None, exact,
        PotatoFacePartVariant.None, exact,
        settings));
}

static void TestPotatoFacePayload()
{
    PotatoFaceRound round = (PotatoFaceRound)new PotatoFaceMiniGame(
        new PotatoFaceSettings()).CreateRound(Context(1006, 47));
    round.ConfigureSession(12, 4, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.PotatoFace);
        AssertEx.Equal(round.Target.EyesVariant,
            (PotatoFacePartVariant)message.GetByte());
        AssertEx.Equal(round.Target.MouthVariant,
            (PotatoFacePartVariant)message.GetByte());
        AssertEx.Equal(round.Target.NoseVariant,
            (PotatoFacePartVariant)message.GetByte());
    }
    finally
    {
        message.Release();
    }
}


}
