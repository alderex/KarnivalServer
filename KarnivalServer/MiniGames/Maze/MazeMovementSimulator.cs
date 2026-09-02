public sealed class MazeMovementSimulator
{
    public readonly record struct Segment(MazePoint Start, MazePoint End);

    public sealed class Result
    {
        public Result(MazePoint position, IReadOnlyList<Segment> segments)
        {
            Position = position;
            Segments = segments;
        }

        public MazePoint Position { get; }
        public IReadOnlyList<Segment> Segments { get; }
    }

    private readonly record struct WallRect(
        float MinimumX,
        float MaximumX,
        float MinimumY,
        float MaximumY);

    private readonly IReadOnlyList<WallRect> walls;

    public MazeMovementSimulator(
        MazeLayout layout,
        float playerRadius,
        float wallThickness)
    {
        walls = BuildWallRectangles(
            layout,
            Math.Max(0f, playerRadius),
            Math.Max(0f, wallThickness));
    }

    public Result Move(MazePoint start, MazePoint target)
    {
        MazePoint current = start;
        MazePoint remaining = target - start;
        List<Segment> segments = new(3);
        const float separation = 0.00001f;

        for (int iteration = 0;
            iteration < 3 && remaining.LengthSquared > 0.0000000001f;
            iteration++)
        {
            bool foundHit = false;
            float earliestTime = 1f;
            MazePoint hitNormal = default;
            foreach (WallRect wall in walls)
            {
                if (TrySweep(
                    current,
                    remaining,
                    wall,
                    out float time,
                    out MazePoint normal) &&
                    time < earliestTime)
                {
                    foundHit = true;
                    earliestTime = time;
                    hitNormal = normal;
                }
            }

            if (!foundHit)
            {
                MazePoint end = current + remaining;
                segments.Add(new Segment(current, end));
                current = end;
                break;
            }

            float safeTime = Math.Max(0f, earliestTime - separation);
            MazePoint contact = current + (remaining * safeTime);
            if ((contact - current).LengthSquared > 0.0000000001f)
                segments.Add(new Segment(current, contact));
            MazePoint leftover = remaining * (1f - earliestTime);
            float intoWall =
                (leftover.X * hitNormal.X) +
                (leftover.Y * hitNormal.Y);
            if (intoWall < 0f)
                leftover -= hitNormal * intoWall;
            current = contact + (hitNormal * separation);
            remaining = leftover;
        }

        return new Result(current, segments);
    }

    public static bool TryGetCircleEntryFraction(
        MazePoint start,
        MazePoint end,
        MazePoint center,
        float radius,
        out float fraction)
    {
        MazePoint movement = end - start;
        MazePoint offset = start - center;
        float a = movement.LengthSquared;
        float radiusSquared = radius * radius;
        if (offset.LengthSquared <= radiusSquared)
        {
            fraction = 0f;
            return true;
        }
        if (a <= 0.0000000001f)
        {
            fraction = 0f;
            return false;
        }

        float b = 2f *
            ((offset.X * movement.X) + (offset.Y * movement.Y));
        float c = offset.LengthSquared - radiusSquared;
        float discriminant = (b * b) - (4f * a * c);
        if (discriminant < 0f)
        {
            fraction = 0f;
            return false;
        }

        float root = MathF.Sqrt(discriminant);
        float first = (-b - root) / (2f * a);
        float second = (-b + root) / (2f * a);
        fraction = first is >= 0f and <= 1f ? first : second;
        return fraction is >= 0f and <= 1f;
    }

    private static IReadOnlyList<WallRect> BuildWallRectangles(
        MazeLayout layout,
        float playerRadius,
        float wallThickness)
    {
        List<WallRect> result = new();
        int size = layout.GridSize;
        float cellSize = 1f / size;
        float expansion = playerRadius + (wallThickness * 0.5f);

        for (int boundaryRow = 0; boundaryRow <= size; boundaryRow++)
        {
            for (int column = 0; column < size; column++)
            {
                bool present = boundaryRow switch
                {
                    0 => HasWall(layout, column, 0, MazeWallMask.South),
                    _ when boundaryRow == size =>
                        HasWall(layout, column, size - 1, MazeWallMask.North),
                    _ => HasWall(
                        layout,
                        column,
                        boundaryRow - 1,
                        MazeWallMask.North),
                };
                if (!present)
                    continue;

                float y = boundaryRow * cellSize;
                result.Add(new WallRect(
                    (column * cellSize) - expansion,
                    ((column + 1) * cellSize) + expansion,
                    y - expansion,
                    y + expansion));
            }
        }

        for (int boundaryColumn = 0;
            boundaryColumn <= size;
            boundaryColumn++)
        {
            for (int row = 0; row < size; row++)
            {
                bool present = boundaryColumn switch
                {
                    0 => HasWall(layout, 0, row, MazeWallMask.West),
                    _ when boundaryColumn == size =>
                        HasWall(layout, size - 1, row, MazeWallMask.East),
                    _ => HasWall(
                        layout,
                        boundaryColumn - 1,
                        row,
                        MazeWallMask.East),
                };
                if (!present)
                    continue;

                float x = boundaryColumn * cellSize;
                result.Add(new WallRect(
                    x - expansion,
                    x + expansion,
                    (row * cellSize) - expansion,
                    ((row + 1) * cellSize) + expansion));
            }
        }

        return result;
    }

    private static bool HasWall(
        MazeLayout layout,
        int column,
        int row,
        MazeWallMask wall)
    {
        int cell = (row * layout.GridSize) + column;
        return ((MazeWallMask)layout.Walls[cell] & wall) != 0;
    }

    private static bool TrySweep(
        MazePoint start,
        MazePoint movement,
        WallRect wall,
        out float time,
        out MazePoint normal)
    {
        if (!GetAxisTimes(
                start.X,
                movement.X,
                wall.MinimumX,
                wall.MaximumX,
                out float xEntry,
                out float xExit,
                out float xNormal) ||
            !GetAxisTimes(
                start.Y,
                movement.Y,
                wall.MinimumY,
                wall.MaximumY,
                out float yEntry,
                out float yExit,
                out float yNormal))
        {
            time = 0f;
            normal = default;
            return false;
        }

        float entry = Math.Max(xEntry, yEntry);
        float exit = Math.Min(xExit, yExit);
        if (entry > exit || exit < 0f || entry < 0f || entry > 1f)
        {
            time = 0f;
            normal = default;
            return false;
        }

        time = entry;
        normal = xEntry > yEntry
            ? new MazePoint(xNormal, 0f)
            : new MazePoint(0f, yNormal);
        return true;
    }

    private static bool GetAxisTimes(
        float start,
        float movement,
        float minimum,
        float maximum,
        out float entry,
        out float exit,
        out float normal)
    {
        if (Math.Abs(movement) < 0.0000001f)
        {
            entry = float.NegativeInfinity;
            exit = float.PositiveInfinity;
            normal = 0f;
            return start >= minimum && start <= maximum;
        }

        float first = (minimum - start) / movement;
        float second = (maximum - start) / movement;
        if (first <= second)
        {
            entry = first;
            exit = second;
            normal = -1f;
        }
        else
        {
            entry = second;
            exit = first;
            normal = 1f;
        }

        return true;
    }
}
