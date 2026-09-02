public sealed class ArcheryPracticeSettings
{
    public float InputDurationSeconds { get; init; } = 10f;
    public float OutcomeBufferSeconds { get; init; } = 3.5f;
    public float MaximumChargeSeconds { get; init; } = 1.5f;
    public float MinimumLaunchSpeed { get; init; } = 300f;
    public float MaximumLaunchSpeed { get; init; } = 850f;
    public float Gravity { get; init; } = 650f;
    public float MinimumWindAcceleration { get; init; } = 50f;
    public float MaximumWindAcceleration { get; init; } = 150f;
    public float MaximumFlightSeconds { get; init; } = 3f;
    public float SimulationStepSeconds { get; init; } = 1f / 120f;
    public float MinimumAimDegrees { get; init; } = -10f;
    public float MaximumAimDegrees { get; init; } = 65f;
    public float PlayAreaHalfWidth { get; init; } = 500f;
    public float PlayAreaHalfHeight { get; init; } = 250f;
    public float GroundY { get; init; } = -220f;
    public float BowX { get; init; } = -380f;
    public float BowY { get; init; } = -175f;
    public float ArrowHalfLength { get; init; } = 30f;
    public float TargetX { get; init; } = 410f;
    public float TargetY { get; init; } = -175f;
    public float TargetHalfWidth { get; init; } = 22.5f;
    public float TargetHalfHeight { get; init; } = 45f;
    public int CorrectScore { get; init; } = 100;
    public float InputGraceSeconds { get; init; } = 0.5f;
    public float FutureInputToleranceSeconds { get; init; } = 0.1f;
}
