public sealed class ColorSequenceMiniGame : IMiniGame
{
    private const int MaximumSupportedBallCount = 5;

    private readonly ColorSequenceSettings settings;

    public ColorSequenceMiniGame(ColorSequenceSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.ColorSequence;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        int ballCount = Math.Clamp(settings.BallCount, 1, MaximumSupportedBallCount);
        int sequenceLength = Math.Clamp(settings.SequenceLength, 1, byte.MaxValue);
        float playbackLeadInSeconds = Math.Max(0f, settings.PlaybackLeadInSeconds);
        float playbackLitSeconds = Math.Max(0.05f, settings.PlaybackLitSeconds);
        float playbackGapSeconds = Math.Max(0f, settings.PlaybackGapSeconds);
        float inputDurationSeconds = Math.Max(1f, settings.InputDurationSeconds);
        int pointsPerCorrectPosition = Math.Max(0, settings.PointsPerCorrectPosition);
        int perfectSequenceBonus = Math.Max(0, settings.PerfectSequenceBonus);

        byte[] sequence = new byte[sequenceLength];
        for (int index = 0; index < sequence.Length; index++)
            sequence[index] = (byte)context.Random.Next(ballCount);

        float inputStartsAtSeconds = playbackLeadInSeconds +
            (sequenceLength * (playbackLitSeconds + playbackGapSeconds));
        float durationSeconds = inputStartsAtSeconds + inputDurationSeconds;

        return new ColorSequenceRound(
            context.RoundId,
            context.StartsUtc,
            durationSeconds,
            context.ResultsDurationSeconds,
            ballCount,
            sequence,
            playbackLeadInSeconds,
            playbackLitSeconds,
            playbackGapSeconds,
            inputDurationSeconds,
            pointsPerCorrectPosition,
            perfectSequenceBonus,
            settings);
    }
}
