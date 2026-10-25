using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static void TestMazeLayout()
{
    MazeLayout first = MazeGenerator.Generate(0x12345678u, 12);
    MazeLayout second = MazeGenerator.Generate(0x12345678u, 12);
    AssertEx.Equal(12, first.GridSize);
    AssertEx.Equal(78, first.GoalCell);
    AssertEx.Equal(first.StartCell, second.StartCell);
    AssertEx.Equal(first.GoalCell, second.GoalCell);
    AssertEx.Equal(first.EntranceSide, second.EntranceSide);
    AssertEx.SequenceEqual(first.Walls, second.Walls);

    int internalOpenings = 0;
    for (int row = 0; row < first.GridSize; row++)
    {
        for (int column = 0; column < first.GridSize; column++)
        {
            int cell = (row * first.GridSize) + column;
            MazeWallMask walls = (MazeWallMask)first.Walls[cell];
            if (column < first.GridSize - 1 &&
                (walls & MazeWallMask.East) == 0)
            {
                internalOpenings++;
            }
            if (row < first.GridSize - 1 &&
                (walls & MazeWallMask.North) == 0)
            {
                internalOpenings++;
            }
        }
    }

    AssertEx.Equal(143, internalOpenings);
    IReadOnlyList<int> path = FindMazeCellPath(first);
    AssertEx.Equal(first.StartCell, path[0]);
    AssertEx.Equal(first.GoalCell, path[^1]);
    AssertEx.Equal(144, CountReachableCells(first));
    int startColumn = first.StartCell % first.GridSize;
    int startRow = first.StartCell / first.GridSize;
    AssertEx.True(
        startColumn == 0 ||
        startColumn == first.GridSize - 1 ||
        startRow == 0 ||
        startRow == first.GridSize - 1);
    MazeWallMask entranceWall = first.EntranceSide switch
    {
        MazeEntranceSide.North => MazeWallMask.North,
        MazeEntranceSide.East => MazeWallMask.East,
        MazeEntranceSide.South => MazeWallMask.South,
        _ => MazeWallMask.West,
    };
    AssertEx.True(
        ((MazeWallMask)first.Walls[first.StartCell] & entranceWall) == 0);
    MazePoint outsideStart = MazeGenerator.GetOutsideStartPosition(
        first,
        0.01875f,
        0.0045f);
    AssertEx.True(
        outsideStart.X < 0f || outsideStart.X > 1f ||
        outsideStart.Y < 0f || outsideStart.Y > 1f);
    AssertEx.Equal(
        MazeGenerator.GetCellCenter(first.GoalCell, first.GridSize),
        MazeGenerator.GetCellCenter(78, first.GridSize));
}

static void TestMazePayload()
{
    MazeRound round = (MazeRound)new MazeMiniGame(
        new MazeSettings()).CreateRound(Context(1014, 71));
    AssertEx.True(
        round.StartPosition.X < 0f || round.StartPosition.X > 1f ||
        round.StartPosition.Y < 0f || round.StartPosition.Y > 1f);
    AssertEx.Equal(
        MazeGenerator.GetCellCenter(round.Layout.GoalCell,
            round.Layout.GridSize),
        round.GoalPosition);
    round.ConfigureSession(15, 7, 20);
    Message message = round.CreateStartedMessage();
    try
    {
        SkipStartedHeader(message, MiniGameType.Maze);
        AssertEx.Equal(round.Layout.Seed, message.GetUInt());
        AssertEx.Equal((byte)12, message.GetByte());
        AssertEx.Equal((byte)round.Layout.StartCell, message.GetByte());
        AssertEx.Equal((byte)round.Layout.GoalCell, message.GetByte());
        AssertEx.Equal((byte)round.Layout.EntranceSide, message.GetByte());
        AssertEx.Equal(round.StartPosition.X, message.GetFloat());
        AssertEx.Equal(round.StartPosition.Y, message.GetFloat());
        AssertEx.Equal(round.GoalPosition.X, message.GetFloat());
        AssertEx.Equal(round.GoalPosition.Y, message.GetFloat());
        AssertEx.Equal(0.01875f, message.GetFloat());
        AssertEx.Equal(0.0045f, message.GetFloat());
        AssertEx.Equal(0.02625f, message.GetFloat());
        AssertEx.Equal(0.12f, message.GetFloat());
        AssertEx.Equal((byte)16, message.GetByte());
        AssertEx.Equal((ushort)512, message.GetUShort());
        int wallCount = message.GetUShort();
        byte[] walls = new byte[wallCount];
        for (int index = 0; index < wallCount; index++)
            walls[index] = message.GetByte();
        AssertEx.SequenceEqual(round.Layout.Walls, walls);
    }
    finally
    {
        message.Release();
    }
}

static void TestMazeMovement()
{
    byte[] walls = Enumerable
        .Repeat((byte)MazeWallMask.All, 9)
        .ToArray();
    MazeLayout blockedLayout = new(
        1,
        3,
        walls,
        4,
        5,
        MazeEntranceSide.East);
    MazeMovementSimulator blocked = new(blockedLayout, 0.025f, 0.006f);
    MazeMovementSimulator.Result collision = blocked.Move(
        new MazePoint(0.5f, 0.5f),
        new MazePoint(0.9f, 0.62f));
    AssertEx.True(collision.Position.X < (2f / 3f));
    AssertEx.True(collision.Position.Y > 0.5f);

    walls[4] &= (byte)~MazeWallMask.East;
    walls[5] &= (byte)~MazeWallMask.West;
    MazeMovementSimulator open = new(blockedLayout, 0.025f, 0.006f);
    MazeMovementSimulator.Result crossing = open.Move(
        new MazePoint(0.5f, 0.5f),
        new MazePoint(0.85f, 0.5f));
    AssertEx.True(crossing.Position.X > (2f / 3f));
}

static void TestMazeScoring()
{
    MazeSettings settings = new()
    {
        FutureInputToleranceSeconds = 20f,
        InputGraceSeconds = 20f,
    };
    MazeRound completed = (MazeRound)new MazeMiniGame(settings)
        .CreateRound(Context(1015, 72));
    completed.ScheduleStart(DateTime.UtcNow);
    PlayerSession successfulPlayer = new(11, 51, "solver");
    Riptide.Server server = new();
    IReadOnlyList<int> path = FindMazeCellPath(completed.Layout);
    float sampledAt = 0.05f;
    foreach (int cell in path)
    {
        SendMaze(
            completed,
            successfulPlayer,
            server,
            MazeGenerator.GetCellCenter(cell, completed.Layout.GridSize),
            sampledAt);
        sampledAt += 0.05f;
    }
    AssertEx.True(successfulPlayer.SubmittedThisRound);
    AssertEx.Equal(100, successfulPlayer.RoundResult.BaseScore);
    AssertEx.True(successfulPlayer.RoundScore >= 100);

    MazeRound timedOut = (MazeRound)new MazeMiniGame(settings)
        .CreateRound(Context(1016, 73));
    PlayerSession timedOutPlayer = new(12, 52, "timeout");
    timedOut.FinalizeRound(new[] { timedOutPlayer }, server);
    AssertEx.Equal(0, timedOutPlayer.RoundResult.BaseScore);
    AssertEx.Equal(0, timedOutPlayer.RoundScore);
}


}
