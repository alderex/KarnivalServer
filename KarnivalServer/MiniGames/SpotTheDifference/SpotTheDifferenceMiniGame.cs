public sealed class SpotTheDifferenceMiniGame : IMiniGame
{
    private readonly SpotTheDifferenceSettings settings;

    public SpotTheDifferenceMiniGame(SpotTheDifferenceSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.SpotTheDifference;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        int availableCount = Math.Clamp(
            settings.AvailableDifferenceCount,
            1,
            7);
        int selectedCount = Math.Clamp(
            settings.SelectedDifferenceCount,
            1,
            availableCount);
        return new SpotTheDifferenceRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            SelectDifferenceIds(
                availableCount,
                selectedCount,
                context.Random),
            Math.Max(0, settings.PointsPerDifference),
            Math.Max(0, settings.FullCompletionScore),
            settings);
    }

    public static byte[] SelectDifferenceIds(
        int availableCount,
        int selectedCount,
        Random random)
    {
        byte[] candidates = Enumerable.Range(0, availableCount)
            .Select(index => (byte)index)
            .ToArray();
        for (int index = 0; index < selectedCount; index++)
        {
            int swapIndex = random.Next(index, candidates.Length);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
        }

        byte[] selected = candidates[..selectedCount];
        Array.Sort(selected);
        return selected;
    }
}
