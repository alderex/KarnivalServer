public enum FallingObjectShape : byte
{
    Circle = 0,
    Square = 1,
    Star = 2,
}

public readonly record struct FallingObjectScheduleEntry(
    ushort ObjectId,
    FallingObjectShape Shape,
    float XNormalized,
    float SpawnSeconds,
    float CatchSeconds,
    float FallDurationSeconds);