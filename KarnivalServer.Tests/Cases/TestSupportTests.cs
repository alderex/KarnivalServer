using Karnival.ProtocolGeneration;
using Riptide;
using System.Collections;
using System.Reflection;

internal static partial class RegressionTests
{
static string FindServerRoot()
{
    foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        DirectoryInfo? directory = new(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Protocol", "protocol.json")) &&
                File.Exists(Path.Combine(directory.FullName, "KarnivalServer", "serverconfig.json")))
                return directory.FullName;

            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException(
        "Could not locate the Karnival server repository. Pass its path as the first argument.");
}

static void SendSmack(
    SmackManRound round,
    PlayerSession player,
    Riptide.Server server,
    ushort appearanceId,
    float clickedAtSeconds)
{
    Message message = Message.Create()
        .AddUShort(appearanceId)
        .AddFloat(clickedAtSeconds);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SkipStartedHeader(Message message, MiniGameType expectedType)
{
    AssertEx.Equal((ulong)NetworkMessageId.MiniGameStarted, message.GetVarULong());
    message.GetUInt();
    message.GetUInt();
    message.GetUShort();
    message.GetUShort();
    AssertEx.Equal((ushort)expectedType, message.GetUShort());
    message.GetLong();
    message.GetFloat();
    message.GetFloat();
    message.GetFloat();
    message.GetFloat(); // playable start
    message.GetFloat(); // playable end
}

static void SendPlateStackerMovement(
    PlateStackerRound round,
    PlayerSession player,
    Riptide.Server server,
    sbyte direction,
    float sampledAt)
{
    Message message = Message.Create()
        .AddSByte(direction)
        .AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SendStopGo(StopGoRound round, PlayerSession player, Riptide.Server server,
    bool running, float sampledAt)
{
    Message message = Message.Create().AddBool(running).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SendCarPark(CarParkRound round, PlayerSession player, Riptide.Server server,
    float steering, float sampledAt)
{
    Message message = Message.Create().AddFloat(steering).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SendSpotTheDifference(
    SpotTheDifferenceRound round,
    PlayerSession player,
    Riptide.Server server,
    byte differenceId,
    float sampledAt)
{
    Message message = Message.Create().AddByte(differenceId).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SendWheresBaldo(
    WheresBaldoRound round,
    PlayerSession player,
    Riptide.Server server,
    byte characterId,
    float sampledAt)
{
    Message message = Message.Create().AddByte(characterId).AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SendWheelSpinner(
    WheelSpinnerRound round,
    PlayerSession player,
    Riptide.Server server,
    float stoppedAt)
{
    Message message = Message.Create().AddFloat(stoppedAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static void SendStepIntoTraffic(
    StepIntoTrafficRound round,
    PlayerSession player,
    Riptide.Server server,
    float hoppedAt)
{
    Message message = Message.Create().AddFloat(hoppedAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}
static void SendMaze(
    MazeRound round,
    PlayerSession player,
    Riptide.Server server,
    MazePoint desiredPosition,
    float sampledAt)
{
    Message message = Message.Create()
        .AddByte(1)
        .AddFloat(desiredPosition.X)
        .AddFloat(desiredPosition.Y)
        .AddFloat(sampledAt);
    try { round.HandleInput(message, player, server); }
    finally { message.Release(); }
}

static IReadOnlyList<int> FindMazeCellPath(MazeLayout layout)
{
    int[] previous = Enumerable.Repeat(-1, layout.Walls.Length).ToArray();
    Queue<int> pending = new();
    previous[layout.StartCell] = layout.StartCell;
    pending.Enqueue(layout.StartCell);
    while (pending.Count > 0)
    {
        int cell = pending.Dequeue();
        if (cell == layout.GoalCell)
            break;
        foreach (int neighbor in GetOpenMazeNeighbors(layout, cell))
        {
            if (previous[neighbor] >= 0)
                continue;
            previous[neighbor] = cell;
            pending.Enqueue(neighbor);
        }
    }

    AssertEx.True(previous[layout.GoalCell] >= 0);
    List<int> path = new();
    for (int cell = layout.GoalCell;
        cell != layout.StartCell;
        cell = previous[cell])
    {
        path.Add(cell);
    }
    path.Add(layout.StartCell);
    path.Reverse();
    return path;
}

static int CountReachableCells(MazeLayout layout)
{
    HashSet<int> visited = new() { layout.StartCell };
    Queue<int> pending = new();
    pending.Enqueue(layout.StartCell);
    while (pending.Count > 0)
    {
        foreach (int neighbor in GetOpenMazeNeighbors(
            layout,
            pending.Dequeue()))
        {
            if (visited.Add(neighbor))
                pending.Enqueue(neighbor);
        }
    }
    return visited.Count;
}

static IEnumerable<int> GetOpenMazeNeighbors(MazeLayout layout, int cell)
{
    int size = layout.GridSize;
    int column = cell % size;
    int row = cell / size;
    MazeWallMask walls = (MazeWallMask)layout.Walls[cell];
    if (row < size - 1 && (walls & MazeWallMask.North) == 0)
        yield return cell + size;
    if (column < size - 1 && (walls & MazeWallMask.East) == 0)
        yield return cell + 1;
    if (row > 0 && (walls & MazeWallMask.South) == 0)
        yield return cell - size;
    if (column > 0 && (walls & MazeWallMask.West) == 0)
        yield return cell - 1;
}

static int GetSampleCount(object round)
{
    FieldInfo field = round.GetType().GetField(
        "playerStates", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("playerStates field was not found.");
    IEnumerable states = (IEnumerable)(field.GetValue(round)
        ?? throw new InvalidOperationException("playerStates is null."));
    foreach (object pair in states)
    {
        object state = pair.GetType().GetProperty("Value")!.GetValue(pair)!;
        ICollection samples = (ICollection)state.GetType().GetProperty("Samples")!
            .GetValue(state)!;
        return samples.Count;
    }
    return 0;
}

}
