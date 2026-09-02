public enum StopGoRacerStatus : byte
{
    Racing = 0,
    Finished = 1,
    Eliminated = 2,
    Disconnected = 3,
    TimedOut = 4,
}

public readonly record struct StopGoLightInterval(
    bool IsGreen,
    float StartsAtSeconds,
    float EndsAtSeconds);

public readonly record struct StopGoMovementSample(
    bool IsRunning,
    float SampledAtSeconds);
