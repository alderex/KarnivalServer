public interface IMiniGame
{
    MiniGameType GameType { get; }
    MiniGameRoundBase CreateRound(MiniGameRoundStartContext context);
}