public sealed class ColorMatchMiniGame : IMiniGame
{
    private readonly ColorMatchSettings settings;

    public ColorMatchMiniGame(ColorMatchSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.ColorMatch;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        byte minimumChannel = Math.Min(
            settings.MinimumTargetChannel,
            settings.MaximumTargetChannel);
        byte maximumChannel = Math.Max(
            settings.MinimumTargetChannel,
            settings.MaximumTargetChannel);

        return new ColorMatchRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            (byte)context.Random.Next(minimumChannel, maximumChannel + 1),
            (byte)context.Random.Next(minimumChannel, maximumChannel + 1),
            (byte)context.Random.Next(minimumChannel, maximumChannel + 1),
            settings.InitialRed,
            settings.InitialGreen,
            settings.InitialBlue,
            settings);
    }
}
