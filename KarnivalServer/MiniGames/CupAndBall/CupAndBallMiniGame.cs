public sealed class CupAndBallMiniGame : IMiniGame
{
    private const int CupCount = 3;

    private static readonly CupAndBallSwap[] AvailableSwaps =
    {
        new(0, 1),
        new(0, 2),
        new(1, 2),
    };

    private readonly CupAndBallSettings settings;

    public CupAndBallMiniGame(CupAndBallSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.CupAndBall;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float ballRevealSeconds = Math.Max(0.1f, settings.BallRevealSeconds);
        float cupDropSeconds = Math.Max(0.05f, settings.CupDropSeconds);
        float preShufflePauseSeconds = Math.Max(
            0f,
            settings.PreShufflePauseSeconds);
        int shuffleCount = Math.Clamp(
            settings.ShuffleCount,
            1,
            byte.MaxValue);
        float shuffleStepSeconds = Math.Max(0.05f, settings.ShuffleStepSeconds);
        float inputDurationSeconds = Math.Max(1f, settings.InputDurationSeconds);
        float outcomeRevealSeconds = Math.Max(0.1f, settings.OutcomeRevealSeconds);
        int correctScore = Math.Max(0, settings.CorrectScore);

        byte initialBallSlot = (byte)context.Random.Next(CupCount);
        CupAndBallSwap[] swaps = CreateSwaps(shuffleCount, context.Random);
        byte correctSlot = TrackSlot(initialBallSlot, swaps);
        float inputStartsAtSeconds = ballRevealSeconds +
            cupDropSeconds +
            preShufflePauseSeconds +
            (shuffleCount * shuffleStepSeconds);
        float durationSeconds = inputStartsAtSeconds +
            inputDurationSeconds +
            outcomeRevealSeconds;

        return new CupAndBallRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            initialBallSlot,
            correctSlot,
            swaps,
            ballRevealSeconds,
            cupDropSeconds,
            preShufflePauseSeconds,
            shuffleStepSeconds,
            inputDurationSeconds,
            outcomeRevealSeconds,
            correctScore,
            settings);
    }

    private static CupAndBallSwap[] CreateSwaps(int count, Random random)
    {
        CupAndBallSwap[] swaps = new CupAndBallSwap[count];
        int previousSwapIndex = -1;
        for (int index = 0; index < swaps.Length; index++)
        {
            int swapIndex;
            do
            {
                swapIndex = random.Next(AvailableSwaps.Length);
            }
            while (swapIndex == previousSwapIndex);

            swaps[index] = AvailableSwaps[swapIndex];
            previousSwapIndex = swapIndex;
        }

        return swaps;
    }

    private static byte TrackSlot(
        byte initialSlot,
        IReadOnlyList<CupAndBallSwap> swaps)
    {
        byte slot = initialSlot;
        foreach (CupAndBallSwap swap in swaps)
        {
            if (slot == swap.FirstSlot)
                slot = swap.SecondSlot;
            else if (slot == swap.SecondSlot)
                slot = swap.FirstSlot;
        }

        return slot;
    }
}
