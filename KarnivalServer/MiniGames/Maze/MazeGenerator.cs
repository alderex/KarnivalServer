public static class MazeGenerator
{
    private readonly record struct Neighbor(
        int Cell,
        MazeWallMask FromWall,
        MazeWallMask ToWall);

    private sealed class XorShiftRandom
    {
        private uint state;

        public XorShiftRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public int Next(int maximum)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)maximum);
        }
    }

    public static MazeLayout Generate(uint seed, int requestedGridSize)
    {
        int gridSize = Math.Clamp(requestedGridSize, 3, 15);
        if (gridSize % 2 == 0)
            gridSize--;

        int cellCount = checked(gridSize * gridSize);
        byte[] walls = Enumerable
            .Repeat((byte)MazeWallMask.All, cellCount)
            .ToArray();
        bool[] visited = new bool[cellCount];
        int startCell =
            ((gridSize / 2) * gridSize) + (gridSize / 2);
        Stack<int> stack = new();
        XorShiftRandom random = new(seed);
        visited[startCell] = true;
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            int cell = stack.Peek();
            Neighbor[] candidates = GetNeighbors(cell, gridSize)
                .Where(neighbor => !visited[neighbor.Cell])
                .ToArray();
            if (candidates.Length == 0)
            {
                stack.Pop();
                continue;
            }

            Neighbor selected = candidates[random.Next(candidates.Length)];
            walls[cell] &= (byte)~selected.FromWall;
            walls[selected.Cell] &= (byte)~selected.ToWall;
            visited[selected.Cell] = true;
            stack.Push(selected.Cell);
        }

        int[] distances = CalculateDistances(
            walls,
            gridSize,
            startCell);
        int exitCell = Enumerable.Range(0, cellCount)
            .Where(cell => IsPerimeter(cell, gridSize))
            .OrderByDescending(cell => distances[cell])
            .ThenBy(cell => cell)
            .First();
        MazeExitSide exitSide = ChooseExitSide(
            exitCell,
            gridSize,
            seed);
        walls[exitCell] &= (byte)~SideToWall(exitSide);

        return new MazeLayout(
            seed,
            gridSize,
            walls,
            startCell,
            exitCell,
            exitSide);
    }

    public static MazePoint GetCellCenter(int cell, int gridSize)
    {
        int column = cell % gridSize;
        int row = cell / gridSize;
        return new MazePoint(
            (column + 0.5f) / gridSize,
            (row + 0.5f) / gridSize);
    }

    public static MazePoint GetExitCenter(
        MazeLayout layout,
        float playerRadius,
        float exitRadius)
    {
        MazePoint cellCenter = GetCellCenter(
            layout.ExitCell,
            layout.GridSize);
        float outsideDistance =
            (0.5f / layout.GridSize) +
            Math.Max(0f, playerRadius) +
            Math.Max(0f, exitRadius);
        return layout.ExitSide switch
        {
            MazeExitSide.North =>
                new MazePoint(cellCenter.X, cellCenter.Y + outsideDistance),
            MazeExitSide.East =>
                new MazePoint(cellCenter.X + outsideDistance, cellCenter.Y),
            MazeExitSide.South =>
                new MazePoint(cellCenter.X, cellCenter.Y - outsideDistance),
            _ =>
                new MazePoint(cellCenter.X - outsideDistance, cellCenter.Y),
        };
    }

    private static IEnumerable<Neighbor> GetNeighbors(
        int cell,
        int gridSize)
    {
        int column = cell % gridSize;
        int row = cell / gridSize;
        if (row < gridSize - 1)
            yield return new Neighbor(
                cell + gridSize,
                MazeWallMask.North,
                MazeWallMask.South);
        if (column < gridSize - 1)
            yield return new Neighbor(
                cell + 1,
                MazeWallMask.East,
                MazeWallMask.West);
        if (row > 0)
            yield return new Neighbor(
                cell - gridSize,
                MazeWallMask.South,
                MazeWallMask.North);
        if (column > 0)
            yield return new Neighbor(
                cell - 1,
                MazeWallMask.West,
                MazeWallMask.East);
    }

    private static int[] CalculateDistances(
        byte[] walls,
        int gridSize,
        int startCell)
    {
        int[] distances = Enumerable.Repeat(-1, walls.Length).ToArray();
        Queue<int> pending = new();
        distances[startCell] = 0;
        pending.Enqueue(startCell);
        while (pending.Count > 0)
        {
            int cell = pending.Dequeue();
            foreach (Neighbor neighbor in GetNeighbors(cell, gridSize))
            {
                if (((MazeWallMask)walls[cell] & neighbor.FromWall) != 0 ||
                    distances[neighbor.Cell] >= 0)
                {
                    continue;
                }

                distances[neighbor.Cell] = distances[cell] + 1;
                pending.Enqueue(neighbor.Cell);
            }
        }

        return distances;
    }

    private static bool IsPerimeter(int cell, int gridSize)
    {
        int column = cell % gridSize;
        int row = cell / gridSize;
        return column == 0 ||
            column == gridSize - 1 ||
            row == 0 ||
            row == gridSize - 1;
    }

    private static MazeExitSide ChooseExitSide(
        int cell,
        int gridSize,
        uint seed)
    {
        int column = cell % gridSize;
        int row = cell / gridSize;
        List<MazeExitSide> sides = new(2);
        if (row == gridSize - 1)
            sides.Add(MazeExitSide.North);
        if (column == gridSize - 1)
            sides.Add(MazeExitSide.East);
        if (row == 0)
            sides.Add(MazeExitSide.South);
        if (column == 0)
            sides.Add(MazeExitSide.West);
        return sides[(int)((seed + (uint)cell) % (uint)sides.Count)];
    }

    private static MazeWallMask SideToWall(MazeExitSide side)
    {
        return side switch
        {
            MazeExitSide.North => MazeWallMask.North,
            MazeExitSide.East => MazeWallMask.East,
            MazeExitSide.South => MazeWallMask.South,
            _ => MazeWallMask.West,
        };
    }
}
