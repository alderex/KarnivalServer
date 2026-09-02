public sealed class ArcheryPracticeMiniGame : IMiniGame
{
    private readonly ArcheryPracticeSettings settings;

    public ArcheryPracticeMiniGame(ArcheryPracticeSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.ArcheryPractice;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        float inputDuration = Math.Max(1f, settings.InputDurationSeconds);
        float maximumFlight = Math.Max(0.25f, settings.MaximumFlightSeconds);
        float outcomeBuffer = Math.Max(
            settings.OutcomeBufferSeconds,
            maximumFlight + Math.Max(0f, settings.InputGraceSeconds));
        float minimumWind = Math.Max(0f, settings.MinimumWindAcceleration);
        float maximumWind = Math.Max(
            minimumWind,
            settings.MaximumWindAcceleration);
        float windMagnitude = minimumWind +
            ((maximumWind - minimumWind) * context.Random.NextSingle());
        float windAccelerationX = context.Random.Next(2) == 0
            ? -windMagnitude
            : windMagnitude;

        return new ArcheryPracticeRound(
            context.RoundId,
            context.StartsUtc,
            inputDuration + outcomeBuffer,
            context.ResultsDurationSeconds,
            inputDuration,
            outcomeBuffer,
            windAccelerationX,
            settings);
    }
}
