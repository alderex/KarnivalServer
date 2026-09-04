public sealed class WheelSpinnerMiniGame : IMiniGame
{
    public const int SliceCount = 10;

    private readonly WheelSpinnerSettings settings;

    public WheelSpinnerMiniGame(WheelSpinnerSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.WheelSpinner;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        WheelSpinnerSlice[] slices = settings.Slices
            .Select(slice => new WheelSpinnerSlice(
                Math.Max(0, slice.Points),
                Math.Max(0.01f, slice.ArcDegrees)))
            .ToArray();
        float initialRotationDegrees =
            context.Random.NextSingle() * 360f;
        return new WheelSpinnerRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            initialRotationDegrees,
            Math.Max(0.01f, settings.AngularSpeedDegreesPerSecond),
            slices,
            settings);
    }
}
