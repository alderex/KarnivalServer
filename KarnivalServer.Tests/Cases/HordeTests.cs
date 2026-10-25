using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestHordeScheduleDeterminism()
{
    HordeClickerScheduleEntry[] first = HordeClickerScheduleGenerator.Generate(
        54321, 25, 0.5f, 10f, 3f, 4.5f, 0.1f, 0.9f);
    HordeClickerScheduleEntry[] second = HordeClickerScheduleGenerator.Generate(
        54321, 25, 0.5f, 10f, 3f, 4.5f, 0.1f, 0.9f);
    AssertEx.SequenceEqual(first, second);
    AssertEx.True(first.All(entry => entry.YNormalized is >= 0.1f and <= 0.9f));
}

static void TestHordePayload()
{
    MiniGameRoundBase round = new HordeClickerMiniGame(
        new HordeClickerSettings()).CreateRound(Context(1002, 13));
    round.ConfigureSession(8, 3, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.HordeClicker);
        uint seed = message.GetUInt();
        int characterCount = message.GetUShort();
        message.GetInt();
        float firstSpawn = message.GetFloat();
        float lastSpawn = message.GetFloat();
        float minimumTravel = message.GetFloat();
        float maximumTravel = message.GetFloat();
        float minimumY = message.GetFloat();
        float maximumY = message.GetFloat();
        int count = message.GetUShort();
        HordeClickerScheduleEntry[] expected = HordeClickerScheduleGenerator.Generate(
            seed, characterCount, firstSpawn, lastSpawn,
            minimumTravel, maximumTravel, minimumY, maximumY);
        AssertEx.Equal(expected.Length, count);
        for (int index = 0; index < count; index++)
        {
            HordeClickerScheduleEntry actual = new(
                message.GetUShort(),
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
