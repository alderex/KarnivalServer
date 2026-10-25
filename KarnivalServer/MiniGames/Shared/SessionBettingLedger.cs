/// <summary>Wager validation and settlement, independent of session phases and transport.</summary>
public sealed class SessionBettingLedger
{
    public sealed class Entry
    {
        public Entry(PlayerSession session)
        {
            UserId = session.UserId;
            ClientId = session.ClientId;
            Username = session.Username;
            IsSimulated = session.IsSimulated;
            AvailableScore = session.TotalScore;
        }

        public long UserId { get; }
        public ushort ClientId { get; set; }
        public string Username { get; }
        public bool IsSimulated { get; }
        public int AvailableScore { get; }
        public int Wager { get; set; }
        public bool Locked { get; set; }
    }


    private readonly Dictionary<long, Entry> entries = new();
    private readonly int maximumWager;
    public SessionBettingLedger(int maximumWager) => this.maximumWager = Math.Max(0, maximumWager);
    public IEnumerable<Entry> Values => entries.Values;
    public void Clear() => entries.Clear();
    public bool TryGetValue(long userId, out Entry? entry) => entries.TryGetValue(userId, out entry);
    public int MaximumWager(Entry entry) => Math.Min(entry.AvailableScore, maximumWager);

    public void Ensure(PlayerSession session)
    {
        if (entries.TryGetValue(session.UserId, out Entry? entry)) entry.ClientId = session.ClientId;
        else entries.Add(session.UserId, new Entry(session));
    }

    public void Initialize(IEnumerable<PlayerSession> sessions, Random random)
    {
        foreach (PlayerSession session in sessions)
        {
            Ensure(session);
            if (!session.IsSimulated) continue;
            int maximum = MaximumWager(entries[session.UserId]);
            TryLock(session, maximum <= 0 ? 0 : (int)random.NextInt64((long)maximum + 1), out _);
        }
    }

    public bool TryLock(PlayerSession session, int wager, out Entry? entry)
    {
        if (!entries.TryGetValue(session.UserId, out entry) || entry.Locked ||
            wager < 0 || wager > MaximumWager(entry) || !session.TrySpendSessionScore(wager))
            return false;
        entry.Wager = wager;
        entry.Locked = true;
        entry.ClientId = session.ClientId;
        return true;
    }

    public bool AllHumansLocked(IEnumerable<PlayerSession> sessions)
    {
        bool found = false;
        foreach (PlayerSession session in sessions)
        {
            if (session.IsSimulated) continue;
            found = true;
            if (!entries.TryGetValue(session.UserId, out Entry? entry) || !entry.Locked) return false;
        }
        return found;
    }

    public void Resolve(int highestScore, Func<long, int> roundScore,
        Action<long, int> addScore, IDictionary<long, int> losses)
    {
        foreach (Entry entry in entries.Values)
        {
            if (entry.Wager <= 0) continue;
            if (highestScore <= 0 || roundScore(entry.UserId) != highestScore)
                losses[entry.UserId] = -entry.Wager;
            else
                addScore(entry.UserId, (int)Math.Min(int.MaxValue, (long)entry.Wager * 2));
        }
        Clear();
    }

    public void Refund(Action<long, int> addScore)
    {
        foreach (Entry entry in entries.Values) addScore(entry.UserId, entry.Wager);
        Clear();
    }
}
