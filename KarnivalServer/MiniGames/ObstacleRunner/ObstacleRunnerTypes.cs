public enum ObstacleHeight : byte
{
    High = 0,
    Medium = 1,
    Low = 2,
}

public enum RunnerAction : byte
{
    Running = 0,
    Jumping = 1,
    Sliding = 2,
}

public readonly record struct ObstacleScheduleEntry(
    ushort ObstacleId,
    ObstacleHeight Height,
    float CollisionSeconds);