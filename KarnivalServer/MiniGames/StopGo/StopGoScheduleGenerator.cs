public static class StopGoScheduleGenerator
{
    public static IReadOnlyList<StopGoLightInterval> Generate(
        uint seed,
        float durationSeconds,
        float minimumGreenSeconds,
        float maximumGreenSeconds,
        float minimumRedSeconds,
        float maximumRedSeconds)
    {
        float duration = Math.Max(0.1f, durationSeconds);
        float minimumGreen = Math.Max(0.1f, minimumGreenSeconds);
        float maximumGreen = Math.Max(minimumGreen, maximumGreenSeconds);
        float minimumRed = Math.Max(0.1f, minimumRedSeconds);
        float maximumRed = Math.Max(minimumRed, maximumRedSeconds);
        Random random = new(unchecked((int)seed));
        List<StopGoLightInterval> result = new();
        float startsAt = 0f;
        bool isGreen = true;
        while (startsAt < duration)
        {
            float minimum = isGreen ? minimumGreen : minimumRed;
            float maximum = isGreen ? maximumGreen : maximumRed;
            float remaining = duration - startsAt;
            List<(float Lower, float Upper, int FutureCount)> choices = new();
            for (int futureCount = 0; futureCount <= 64; futureCount++)
            {
                GetTailBounds(futureCount, !isGreen, minimumGreen, maximumGreen,
                    minimumRed, maximumRed, out float tailMinimum, out float tailMaximum);
                float lower = Math.Max(minimum, remaining - tailMaximum);
                float upper = Math.Min(maximum, remaining - tailMinimum);
                if (lower <= upper + 0.00001f)
                    choices.Add((lower, upper, futureCount));
            }
            if (choices.Count == 0)
                throw new InvalidOperationException("StopGo duration cannot be filled by the configured light ranges.");

            (float selectedLower, float selectedUpper, int selectedFutureCount) =
                choices[random.Next(choices.Count)];
            float length = selectedFutureCount == 0
                ? remaining
                : selectedLower +
                    ((selectedUpper - selectedLower) * (float)random.NextDouble());
            float endsAt = selectedFutureCount == 0 ? duration : startsAt + length;
            result.Add(new StopGoLightInterval(isGreen, startsAt, endsAt));
            startsAt = endsAt;
            isGreen = !isGreen;
        }

        return result;
    }

    private static void GetTailBounds(
        int count,
        bool startsGreen,
        float minimumGreen,
        float maximumGreen,
        float minimumRed,
        float maximumRed,
        out float minimum,
        out float maximum)
    {
        minimum = 0f;
        maximum = 0f;
        bool isGreen = startsGreen;
        for (int index = 0; index < count; index++)
        {
            minimum += isGreen ? minimumGreen : minimumRed;
            maximum += isGreen ? maximumGreen : maximumRed;
            isGreen = !isGreen;
        }
    }
}
