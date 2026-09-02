public enum MazeExitSide : byte
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

[Flags]
public enum MazeWallMask : byte
{
    None = 0,
    North = 1,
    East = 2,
    South = 4,
    West = 8,
    All = North | East | South | West,
}

public readonly record struct MazePoint(float X, float Y)
{
    public static MazePoint operator +(MazePoint left, MazePoint right) =>
        new(left.X + right.X, left.Y + right.Y);
    public static MazePoint operator -(MazePoint left, MazePoint right) =>
        new(left.X - right.X, left.Y - right.Y);
    public static MazePoint operator *(MazePoint point, float scalar) =>
        new(point.X * scalar, point.Y * scalar);
    public float LengthSquared => (X * X) + (Y * Y);
}

public readonly record struct MazeLayout(
    uint Seed,
    int GridSize,
    byte[] Walls,
    int StartCell,
    int ExitCell,
    MazeExitSide ExitSide);

public readonly record struct MazeInputSample(
    MazePoint DesiredPosition,
    float SampledAtSeconds);
