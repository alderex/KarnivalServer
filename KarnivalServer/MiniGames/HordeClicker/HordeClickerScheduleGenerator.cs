public static class HordeClickerScheduleGenerator
{
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

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16_777_216f);
        }
    }

    public static HordeClickerScheduleEntry[] Generate(
        uint seed,
        int characterCount,
        float firstSpawnSeconds,
        float lastSpawnSeconds,
        float minimumTravelDurationSeconds,
        float maximumTravelDurationSeconds,
        float minimumYNormalized,
        float maximumYNormalized)
    {
        int count = Math.Max(1, characterCount);
        float firstSpawn = Math.Max(0f, firstSpawnSeconds);
        float lastSpawn = Math.Max(firstSpawn, lastSpawnSeconds);
        float minimumTravel = Math.Max(0.1f, minimumTravelDurationSeconds);
        float maximumTravel = Math.Max(minimumTravel, maximumTravelDurationSeconds);
        float minimumY = Math.Clamp(minimumYNormalized, 0f, 1f);
        float maximumY = Math.Clamp(maximumYNormalized, minimumY, 1f);
        XorShift32 random = new(seed);
        HordeClickerScheduleEntry[] schedule = new HordeClickerScheduleEntry[count];
        for (int index = 0; index < schedule.Length; index++)
        {
            float normalizedIndex = schedule.Length <= 1
                ? 0f
                : index / (float)(schedule.Length - 1);
            float travelDuration = minimumTravel +
                ((maximumTravel - minimumTravel) * random.NextFloat());
            float yNormalized = minimumY +
                ((maximumY - minimumY) * random.NextFloat());
            schedule[index] = new HordeClickerScheduleEntry(
                (ushort)(index + 1),
                firstSpawn + ((lastSpawn - firstSpawn) * normalizedIndex),
                travelDuration,
                yNormalized);
        }

        return schedule;
    }
}
