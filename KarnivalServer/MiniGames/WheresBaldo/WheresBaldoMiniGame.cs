public sealed class WheresBaldoMiniGame : IMiniGame
{
    public const int CharacterCount = 100;

    private readonly WheresBaldoSettings settings;

    public WheresBaldoMiniGame(WheresBaldoSettings settings)
    {
        this.settings = settings;
    }

    public MiniGameType GameType => MiniGameType.WheresBaldo;

    public MiniGameRoundBase CreateRound(MiniGameRoundStartContext context)
    {
        uint crowdSeed = (uint)context.Random.NextInt64(
            1,
            (long)uint.MaxValue + 1L);
        byte baldoCharacterId = (byte)context.Random.Next(CharacterCount);
        return new WheresBaldoRound(
            context.RoundId,
            context.StartsUtc,
            Math.Max(1f, settings.DurationSeconds),
            context.ResultsDurationSeconds,
            crowdSeed,
            baldoCharacterId,
            Math.Max(0, settings.CorrectScore),
            Math.Max(0, settings.WrongSelectionPenalty),
            settings);
    }
}
