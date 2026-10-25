using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestFallingScheduleDeterminism()
{
    FallingObjectScheduleEntry[] first = FallingObjectScheduleGenerator.Generate(
        FallingObjectShape.Star, 12345, 10, 4, 3.8f, 14f, 2.2f, 3.6f);
    FallingObjectScheduleEntry[] second = FallingObjectScheduleGenerator.Generate(
        FallingObjectShape.Star, 12345, 10, 4, 3.8f, 14f, 2.2f, 3.6f);
    AssertEx.SequenceEqual(first, second);
    AssertEx.True(first.All(entry => entry.XNormalized is >= 0f and <= 1f));
}

static void TestFallingPayload()
{
    MiniGameRoundBase round = new FallingObjectCatcherMiniGame(
        new FallingObjectCatcherSettings()).CreateRound(Context(1001, 9));
    round.ConfigureSession(7, 2, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.FallingObjectCatcher);
        FallingObjectShape target = (FallingObjectShape)message.GetByte();
        uint seed = message.GetUInt();
        int targets = message.GetUShort();
        int distractors = message.GetUShort();
        float firstCatch = message.GetFloat();
        float lastCatch = message.GetFloat();
        float minimumFall = message.GetFloat();
        float maximumFall = message.GetFloat();
        AssertEx.Equal(0.8f, message.GetFloat());
        AssertEx.Equal(0.06f, message.GetFloat());
        AssertEx.Equal(0.06f, message.GetFloat());
        AssertEx.Equal(0.181f, message.GetFloat());
        int count = message.GetUShort();
        FallingObjectScheduleEntry[] expected = FallingObjectScheduleGenerator.Generate(
            target, seed, targets, distractors, firstCatch, lastCatch, minimumFall, maximumFall);
        AssertEx.Equal(expected.Length, count);
        for (int index = 0; index < count; index++)
        {
            FallingObjectScheduleEntry actual = new(
                message.GetUShort(),
                (FallingObjectShape)message.GetByte(),
                message.GetFloat(),
                message.GetFloat(),
                message.GetFloat(),
                message.GetFloat());
            AssertEx.Equal(expected[index], actual);
        }
    }
    finally
    {
        message.Release();
    }
}


}
