public sealed class CarParkMiniGame : IMiniGame
{
    private readonly CarParkSettings settings;

    public CarParkMiniGame(CarParkSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.CarPark;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        int spotCount = Math.Clamp(settings.ParkingSpotCount, 3, 12);
        byte emptySpot = (byte)context.Random.Next(spotCount);
        float leadIn = Math.Max(0.1f, settings.LeadInSeconds);
        float drivingDuration = Math.Max(1f, settings.DrivingDurationSeconds);
        float outcomeBuffer = Math.Max(
            settings.InputGraceSeconds + 0.1f,
            settings.OutcomeBufferSeconds);
        float duration = leadIn + drivingDuration + outcomeBuffer;

        return new CarParkRound(
            context.RoundId,
            context.StartsUtc,
            duration,
            context.ResultsDurationSeconds,
            spotCount,
            emptySpot,
            leadIn,
            drivingDuration,
            outcomeBuffer,
            settings);
    }
}
