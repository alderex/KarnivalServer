public enum CarParkOutcomeReason : byte
{
    Parked = 0,
    Collision = 1,
    Offscreen = 2,
    Timeout = 3,
}

public readonly record struct CarParkPose(float X, float Y, float HeadingDegrees);

public readonly record struct CarParkSteeringSample(
    float Steering,
    float SampledAtSeconds);
