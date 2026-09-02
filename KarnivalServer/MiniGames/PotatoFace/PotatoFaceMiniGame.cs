public sealed class PotatoFaceMiniGame : IMiniGame
{
    private readonly PotatoFaceSettings settings;

    public PotatoFaceMiniGame(PotatoFaceSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.PotatoFace;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        PotatoFaceTarget target = PotatoFaceTargetGenerator.Generate(context.Random);
        return new PotatoFaceRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            target,
            settings);
    }
}
