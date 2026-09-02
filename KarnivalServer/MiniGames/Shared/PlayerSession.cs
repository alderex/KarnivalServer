public sealed class PlayerSession
{
    public PlayerSession(
        ushort clientId,
        long userId,
        string username,
        int lifetimeWins = 0,
        bool isSimulated = false)
    {
        ClientId = clientId;
        UserId = userId;
        Username = username;
        LifetimeWins = Math.Max(0, lifetimeWins);
        IsSimulated = isSimulated;
    }

    public ushort ClientId { get; }
    public long UserId { get; }
    public string Username { get; }
    public bool IsSimulated { get; }
    public int LifetimeWins { get; private set; }
    public PlayerRoundResult RoundResult { get; private set; } = PlayerRoundResult.Pending;
    public int TotalScore { get; private set; }

    public bool SubmittedThisRound => RoundResult.Submitted;
    public int RoundScore => RoundResult.Score;

    public void ResetRoundState()
    {
        RoundResult = PlayerRoundResult.Pending;
    }

    public void RestoreRoundState(PlayerRoundResult result)
    {
        RoundResult = result;
    }

    public void ApplyRoundResult(PlayerRoundResult result)
    {
        if (RoundResult.Submitted)
            return;

        RoundResult = result;
        TotalScore += result.Score;
    }

    public void MarkMissedRound()
    {
        if (!RoundResult.Submitted)
            RoundResult = PlayerRoundResult.Missed;
    }
    public void ResetSessionScore()
    {
        TotalScore = 0;
        ResetRoundState();
    }

    public void RestoreSessionScore(int score)
    {
        TotalScore = Math.Max(0, score);
        ResetRoundState();
    }

    public bool TrySpendSessionScore(int amount)
    {
        if (amount < 0 || amount > TotalScore)
            return false;

        TotalScore -= amount;
        return true;
    }

    public void AddSessionScore(int amount)
    {
        if (amount <= 0)
            return;

        TotalScore = (int)Math.Min(int.MaxValue, (long)TotalScore + amount);
    }

    public void AddLifetimeWin()
    {
        LifetimeWins++;
    }

}
