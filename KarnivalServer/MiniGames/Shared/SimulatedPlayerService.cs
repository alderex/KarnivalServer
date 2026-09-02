public readonly record struct SimulatedPlayerChat(
    PlayerSession Sender,
    string Message);

public sealed class SimulatedPlayerService
{
    private static readonly string[] NamePrefixes =
    {
        "Brave", "Chill", "Clever", "Cosmic", "Dizzy",
        "Electric", "Fuzzy", "Golden", "Happy", "Hidden",
        "Jolly", "Lucky", "Mighty", "Neon", "Quick",
        "Rapid", "Sneaky", "Sunny", "Turbo", "Wild",
    };

    private static readonly string[] NameSuffixes =
    {
        "Ace", "Bean", "Bolt", "Comet", "Crown",
        "Dash", "Echo", "Fizz", "Flash", "Jester",
        "Knight", "Meteor", "Nova", "Pixel", "Rocket",
        "Shadow", "Spark", "Star", "Wizard", "Zapper",
    };

    private static readonly string[] ExcellentMessages =
    {
        "{0} points! I crushed that one.",
        "Yes! {0}! That round was mine.",
        "{0} points. Practically perfect.",
    };

    private static readonly string[] GoodMessages =
    {
        "{0} points. I will take it.",
        "Not bad at all: {0} points.",
        "{0}! That felt pretty good.",
    };

    private static readonly string[] AverageMessages =
    {
        "{0} points. I can do better.",
        "Only {0}. I left points on the table.",
        "{0}... that could have gone better.",
    };

    private static readonly string[] PoorMessages =
    {
        "Ouch. Just {0} points.",
        "{0} points. That round was rough.",
        "Can we forget that {0} ever happened?",
    };

    private readonly IDictionary<ushort, PlayerSession> sessions;
    private readonly Random random;
    private readonly List<ushort> simulatedClientIds = new();
    private int nextClientId = ushort.MaxValue;
    private long nextUserId = -1;

    public SimulatedPlayerService(
        SimulatedPlayerConfig config,
        IDictionary<ushort, PlayerSession> sessions,
        Random random)
    {
        this.sessions = sessions;
        this.random = random;
        MaximumCount = Math.Max(0, config.MaximumCount);
        SetCount(Math.Clamp(config.InitialCount, 0, MaximumCount));
    }

    public int Count => simulatedClientIds.Count;
    public int MaximumCount { get; }

    public bool TrySetCount(int count)
    {
        if (count < 0 || count > MaximumCount)
            return false;

        SetCount(count);
        return true;
    }

    public IReadOnlyList<SimulatedPlayerChat> CompleteRound(int maximumScore)
    {
        List<SimulatedPlayerChat> chats = new(simulatedClientIds.Count);
        foreach (ushort clientId in simulatedClientIds)
        {
            if (!sessions.TryGetValue(clientId, out PlayerSession? player))
                continue;

            int score;
            if (player.SubmittedThisRound)
            {
                score = player.RoundScore;
            }
            else
            {
                score = random.Next(0, Math.Max(0, maximumScore) + 1);
                player.ApplyRoundResult(new PlayerRoundResult(
                    true,
                    score,
                    score,
                    Math.Max(0, maximumScore),
                    1f));
            }
            chats.Add(new SimulatedPlayerChat(
                player,
                CreatePerformanceMessage(score, maximumScore)));
        }

        return chats;
    }

    private void SetCount(int count)
    {
        while (simulatedClientIds.Count < count)
            AddPlayer();
        while (simulatedClientIds.Count > count)
            RemoveLastPlayer();
    }

    private void AddPlayer()
    {
        ushort clientId = GetAvailableClientId();
        string username = CreateUniqueName();
        sessions.Add(
            clientId,
            new PlayerSession(
                clientId,
                nextUserId--,
                username,
                isSimulated: true));
        simulatedClientIds.Add(clientId);
    }

    private void RemoveLastPlayer()
    {
        int lastIndex = simulatedClientIds.Count - 1;
        ushort clientId = simulatedClientIds[lastIndex];
        simulatedClientIds.RemoveAt(lastIndex);
        sessions.Remove(clientId);
    }

    private ushort GetAvailableClientId()
    {
        while (nextClientId > 0 &&
            sessions.ContainsKey((ushort)nextClientId))
        {
            nextClientId--;
        }

        if (nextClientId <= 0)
            throw new InvalidOperationException("No client IDs remain for simulated players.");

        return (ushort)nextClientId--;
    }

    private string CreateUniqueName()
    {
        HashSet<string> existingNames = new(
            sessions.Values.Select(session => session.Username),
            StringComparer.OrdinalIgnoreCase);
        string baseName =
            NamePrefixes[random.Next(NamePrefixes.Length)] +
            NameSuffixes[random.Next(NameSuffixes.Length)];
        string candidate = baseName;
        int suffix = 2;
        while (existingNames.Contains(candidate))
            candidate = $"{baseName}{suffix++}";

        return candidate;
    }

    private string CreatePerformanceMessage(int score, int maximumScore)
    {
        float scoreRatio = maximumScore <= 0
            ? 0f
            : score / (float)maximumScore;
        string[] messages = scoreRatio switch
        {
            >= 0.85f => ExcellentMessages,
            >= 0.60f => GoodMessages,
            >= 0.40f => AverageMessages,
            _ => PoorMessages,
        };
        return string.Format(
            messages[random.Next(messages.Length)],
            score);
    }
}
