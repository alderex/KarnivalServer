public enum ArcheryPracticeInputType : byte
{
    ChargeStarted = 1,
    ShotReleased = 2,
}

public readonly record struct ArcheryShotResult(
    bool Hit,
    float FlightSeconds,
    float ImpactX,
    float ImpactY,
    float ImpactAngleDegrees);
