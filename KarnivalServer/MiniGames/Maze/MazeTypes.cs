public enum MazeEntranceSide : byte
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public readonly record struct MazeLayout(
    uint Seed,
    int GridSize,
    byte[] Walls,
    int StartCell,
    int GoalCell,
    MazeEntranceSide EntranceSide);

public readonly record struct MazeInputSample(
    MazePoint DesiredPosition,
    float SampledAtSeconds);
