public static class FallingObjectScheduleGenerator
{
    private const float HorizontalMargin = 0.06f;

    private struct XorShift32
    {
        private uint state;

        public XorShift32(uint seed)
        {
            state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int NextInt(int maximumExclusive)
        {
            return maximumExclusive <= 1
                ? 0
                : (int)(NextUInt() % (uint)maximumExclusive);
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16_777_216f);
        }
    }

    public static FallingObjectScheduleEntry[] Generate(
        FallingObjectShape targetShape,
        uint seed,
        int targetObjectCount,
        int distractorCountPerShape,
        float firstCatchSeconds,
        float lastCatchSeconds,
        float minimumFallDurationSeconds,
        float maximumFallDurationSeconds)
    {
        int targetCount = Math.Max(1, targetObjectCount);
        int distractorCount = Math.Max(0, distractorCountPerShape);
        List<FallingObjectShape> shapes = new(targetCount + (2 * distractorCount));
        for (int index = 0; index < targetCount; index++)
            shapes.Add(targetShape);

        for (int shapeValue = 0; shapeValue < 3; shapeValue++)
        {
            FallingObjectShape shape = (FallingObjectShape)shapeValue;
            if (shape == targetShape)
                continue;

            for (int index = 0; index < distractorCount; index++)
                shapes.Add(shape);
        }

        XorShift32 random = new(seed);
        for (int index = shapes.Count - 1; index > 0; index--)
        {
            int otherIndex = random.NextInt(index + 1);
            (shapes[index], shapes[otherIndex]) = (shapes[otherIndex], shapes[index]);
        }

        float firstCatch = Math.Max(0f, firstCatchSeconds);
        float lastCatch = Math.Max(firstCatch, lastCatchSeconds);
        float minimumFall = Math.Max(0.1f, minimumFallDurationSeconds);
        float maximumFall = Math.Max(minimumFall, maximumFallDurationSeconds);
        FallingObjectScheduleEntry[] schedule = new FallingObjectScheduleEntry[shapes.Count];
        for (int index = 0; index < schedule.Length; index++)
        {
            float normalizedIndex = schedule.Length <= 1
                ? 0f
                : index / (float)(schedule.Length - 1);
            float catchSeconds = firstCatch + ((lastCatch - firstCatch) * normalizedIndex);
            float xNormalized = HorizontalMargin +
                ((1f - (2f * HorizontalMargin)) * random.NextFloat());
            float fallDuration = minimumFall +
                ((maximumFall - minimumFall) * random.NextFloat());
            schedule[index] = new FallingObjectScheduleEntry(
                (ushort)(index + 1),
                shapes[index],
                xNormalized,
                catchSeconds - fallDuration,
                catchSeconds,
                fallDuration);
        }

        return schedule;
    }
}