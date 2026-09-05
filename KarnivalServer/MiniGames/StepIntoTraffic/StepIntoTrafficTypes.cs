public readonly record struct StepIntoTrafficLane(
    byte LaneIndex,
    bool MovesLeftToRight,
    float SpeedNormalizedPerSecond,
    float FirstCenterCrossingSeconds,
    float CrossingIntervalSeconds);

public readonly record struct StepIntoTrafficCollision(
    byte LaneIndex,
    int CarSequence,
    float CollisionSeconds);
