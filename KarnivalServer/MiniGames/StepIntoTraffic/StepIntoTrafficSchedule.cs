public static class StepIntoTrafficSchedule
{
    public static StepIntoTrafficLane[] Generate(
        int laneCount,
        Random random,
        StepIntoTrafficSettings settings)
    {
        StepIntoTrafficLane[] lanes = new StepIntoTrafficLane[laneCount];
        for (int index = 0; index < lanes.Length; index++)
        {
            float speed = Lerp(
                settings.MinimumCarSpeedNormalizedPerSecond,
                settings.MaximumCarSpeedNormalizedPerSecond,
                random.NextSingle());
            float safeGap = Lerp(
                settings.MinimumSafeGapSeconds,
                settings.MaximumSafeGapSeconds,
                random.NextSingle());
            float occupiedSeconds = 2f *
                (settings.CarHalfWidthNormalized +
                    settings.PedestrianHalfWidthNormalized) / speed;
            float interval = occupiedSeconds + safeGap;
            lanes[index] = new StepIntoTrafficLane(
                (byte)(index + 1),
                index % 2 == 0,
                speed,
                random.NextSingle() * interval,
                interval);
        }

        return lanes;
    }

    public static float GetCarX(
        StepIntoTrafficLane lane,
        int sequence,
        float elapsedSeconds)
    {
        float crossing = lane.FirstCenterCrossingSeconds +
            (sequence * lane.CrossingIntervalSeconds);
        float direction = lane.MovesLeftToRight ? 1f : -1f;
        return direction * lane.SpeedNormalizedPerSecond *
            (elapsedSeconds - crossing);
    }

    public static bool TryGetCollision(
        IReadOnlyList<StepIntoTrafficLane> lanes,
        float playerRow,
        float elapsedSeconds,
        StepIntoTrafficSettings settings,
        out StepIntoTrafficCollision collision)
    {
        float verticalReach = settings.CarHalfHeightRows +
            settings.PedestrianHalfHeightRows;
        float horizontalReach = settings.CarHalfWidthNormalized +
            settings.PedestrianHalfWidthNormalized;
        foreach (StepIntoTrafficLane lane in lanes)
        {
            if (Math.Abs(playerRow - lane.LaneIndex) > verticalReach)
                continue;

            float relative = (elapsedSeconds - lane.FirstCenterCrossingSeconds) /
                lane.CrossingIntervalSeconds;
            int nearest = (int)MathF.Round(relative);
            if (Math.Abs(GetCarX(lane, nearest, elapsedSeconds)) <= horizontalReach)
            {
                collision = new StepIntoTrafficCollision(
                    lane.LaneIndex,
                    nearest,
                    elapsedSeconds);
                return true;
            }
        }

        collision = default;
        return false;
    }

    private static float Lerp(float from, float to, float amount) =>
        from + ((to - from) * amount);
}
