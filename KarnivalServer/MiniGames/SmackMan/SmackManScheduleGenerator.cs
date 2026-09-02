public static class SmackManScheduleGenerator
{
    public const int HoleCount = 16;

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
    }

    public static SmackManScheduleEntry[] Generate(
        uint seed,
        int groupCount,
        int headsPerGroup,
        float firstGroupSeconds,
        float groupIntervalSeconds,
        float activeDurationSeconds)
    {
        int heads = Math.Clamp(headsPerGroup, 1, HoleCount);
        int groups = Math.Clamp(groupCount, 1, ushort.MaxValue / heads);
        int count = groups * heads;
        float firstGroup = Math.Max(0f, firstGroupSeconds);
        float interval = Math.Max(0.0001f, groupIntervalSeconds);
        float activeDuration = Math.Max(0.0001f, activeDurationSeconds);
        XorShift32 random = new(seed);
        float[] occupiedUntil = new float[HoleCount];
        SmackManScheduleEntry[] schedule = new SmackManScheduleEntry[count];
        Span<byte> availableHoles = stackalloc byte[HoleCount];

        int appearanceIndex = 0;
        for (int groupIndex = 0; groupIndex < groups; groupIndex++)
        {
            float spawnSeconds = firstGroup + (groupIndex * interval);
            for (int headIndex = 0; headIndex < heads; headIndex++)
            {
                int availableCount = 0;
                for (byte holeIndex = 0; holeIndex < HoleCount; holeIndex++)
                {
                    if (occupiedUntil[holeIndex] <= spawnSeconds)
                        availableHoles[availableCount++] = holeIndex;
                }

                if (availableCount == 0)
                    throw new InvalidOperationException("Smack Man has no available hole for an appearance.");

                int selectedIndex = (int)(random.NextUInt() % (uint)availableCount);
                byte selectedHole = availableHoles[selectedIndex];
                occupiedUntil[selectedHole] = spawnSeconds + activeDuration;
                schedule[appearanceIndex] = new SmackManScheduleEntry(
                    (ushort)(appearanceIndex + 1),
                    selectedHole,
                    spawnSeconds);
                appearanceIndex++;
            }
        }

        return schedule;
    }
}
