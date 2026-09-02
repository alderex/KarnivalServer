public sealed class CardMatchMiniGame : IMiniGame
{
    private const int MaximumPairCount = 6;

    private readonly CardMatchSettings settings;

    public CardMatchMiniGame(CardMatchSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.CardMatch;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        int pairCount = Math.Clamp(settings.PairCount, 1, MaximumPairCount);
        int availableShapeCount = Math.Clamp(
            settings.AvailableShapeCount,
            1,
            Enum.GetValues<CardMatchShape>().Length);
        int pointsPerPair = Math.Max(0, settings.PointsPerPair);
        float durationSeconds = Math.Max(1f, settings.DurationSeconds);
        float mismatchRevealSeconds = Math.Max(0f, settings.MismatchRevealSeconds);

        CardMatchShape[] shapeOrder = Enum.GetValues<CardMatchShape>()
            .Take(availableShapeCount)
            .ToArray();
        Shuffle(shapeOrder, context.Random);

        CardMatchShape[] cards = new CardMatchShape[pairCount * 2];
        for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            CardMatchShape shape = shapeOrder[pairIndex % shapeOrder.Length];
            cards[pairIndex * 2] = shape;
            cards[(pairIndex * 2) + 1] = shape;
        }
        Shuffle(cards, context.Random);

        return new CardMatchRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            cards,
            pairCount,
            pointsPerPair,
            mismatchRevealSeconds,
            settings);
    }

    private static void Shuffle<T>(T[] values, Random random)
    {
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) =
                (values[swapIndex], values[index]);
        }
    }
}
