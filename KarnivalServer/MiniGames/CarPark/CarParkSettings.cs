public sealed class CarParkSettings
{
    public int ParkingSpotCount { get; init; } = 5;
    public float LeadInSeconds { get; init; } = 0.75f;
    public float DrivingDurationSeconds { get; init; } = 10f;
    public float OutcomeBufferSeconds { get; init; } = 2.5f;
    public float FixedStepSeconds { get; init; } = 1f / 60f;
    public float ForwardSpeed { get; init; } = 0.26f;
    public float MaximumTurnDegreesPerSecond { get; init; } = 100f;
    public float StartX { get; init; } = 0f;
    public float StartY { get; init; } = -0.75f;
    public float ParkingRowY { get; init; } = 0.58f;
    public float FirstParkingX { get; init; } = -0.72f;
    public float LastParkingX { get; init; } = 0.72f;
    public float ParkingHalfWidth { get; init; } = 0.18857143f;
    public float ParkingHalfHeight { get; init; } = 0.165f;
    public float CarHalfWidth { get; init; } = 0.12964286f;
    public float CarHalfHeight { get; init; } = 0.103125f;
    public float BoundsHalfWidth { get; init; } = 1f;
    public float BoundsHalfHeight { get; init; } = 1f;
    public float ParkingAngleToleranceDegrees { get; init; } = 18f;
    public float SteeringSendIntervalSeconds { get; init; } = 0.1f;
    public float SteeringChangeThreshold { get; init; } = 0.03f;
    public float WheelMaximumRotationDegrees { get; init; } = 120f;
    public float WheelReturnDegreesPerSecond { get; init; } = 360f;
    public int CorrectScore { get; init; } = 100;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
    public int MaximumInputSamples { get; init; } = 256;
}
