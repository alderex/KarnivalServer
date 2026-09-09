using Riptide;

public enum SessionLifecyclePhase
{
    Idle,
    Lobby,
    Round,
    Results,
    Betting,
    Victory,
}

public sealed class MiniGameRoundManager
{
    private const float MiniGameStartCountdownSeconds = 2.25f;
    private const float MiniGamePreparationTimeoutSeconds = 10f;
    private const int EntriesPerMessage = 8;

    private sealed class SessionParticipant
    {
        public SessionParticipant(long userId, string username, int lifetimeWins, int score = 0)
        {
            UserId = userId;
            Username = username;
            LifetimeWins = lifetimeWins;
            Score = score;
        }

        public long UserId { get; }
        public string Username { get; }
        public int LifetimeWins { get; set; }
        public int Score { get; set; }
        public PlayerRoundResult RoundResult { get; set; } = PlayerRoundResult.Pending;
    }

    private sealed class BettingEntry
    {
        public BettingEntry(PlayerSession session)
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

    private readonly record struct LeaderboardEntry(
        ushort ClientId,
        string Username,
        int RoundScore,
        int TotalScore,
        int BetLoss);

    private readonly ServerConfig config;
    private readonly Random random;
    private readonly IDictionary<ushort, PlayerSession> sessions;
    private readonly Riptide.Server server;
    private readonly MiniGameCatalog catalog;
    private readonly SqliteAccountPersistence persistence;
    private readonly SimulatedPlayerService simulatedPlayers;
    private readonly Dictionary<long, SessionParticipant> participants = new();
    private readonly HashSet<MiniGameType> gamesPlayedInPass = new();
    private readonly HashSet<ushort> readyClientIds = new();
    private readonly HashSet<long> waitingUserIds = new();
    private readonly Dictionary<MiniGameType, int> sessionGamePlayCounts = new();
    private readonly Dictionary<long, BettingEntry> bettingEntries = new();
    private readonly Dictionary<long, int> roundBetLosses = new();
    private PlayerSession[] cachedLobbyRoster = Array.Empty<PlayerSession>();
    private LeaderboardEntry[] cachedRoundResults = Array.Empty<LeaderboardEntry>();
    private bool lobbyRosterDirty = true;
    private uint nextRoundId = 1;
    private uint nextSessionId = 1;
    private IMiniGame? queuedNextGame;
    private IMiniGame? forcedGame;
    private DateTime phaseEndsUtc;
    private ushort sessionRoundNumber;
    private SessionParticipant[] winners = Array.Empty<SessionParticipant>();
    private int winningScore;
    private ushort bettingTargetRoundNumber;

    public MiniGameRoundManager(
        ServerConfig config,
        Random random,
        IDictionary<ushort, PlayerSession> sessions,
        Riptide.Server server,
        MiniGameCatalog catalog,
        SqliteAccountPersistence persistence)
    {
        this.config = config;
        this.random = random;
        this.sessions = sessions;
        this.server = server;
        this.catalog = catalog;
        this.persistence = persistence;
        simulatedPlayers = new SimulatedPlayerService(
            config.SimulatedPlayers,
            sessions,
            new Random(random.Next()));
    }

    public event Action<PlayerSession, string>? SimulatedPlayerChatRequested;
    public event Action<string>? SystemChatRequested;
    public event Action<ushort, string>? SystemChatToClientRequested;

    public SessionLifecyclePhase Phase { get; private set; } = SessionLifecyclePhase.Idle;
    public uint SessionId { get; private set; }
    public MiniGameRoundBase? CurrentRound { get; private set; }
    public int SimulatedPlayerCount => simulatedPlayers.Count;
    public int MaximumSimulatedPlayerCount => simulatedPlayers.MaximumCount;

    public void Update(DateTime nowUtc)
    {
        if (ConnectedHumans().Count == 0)
        {
            if (Phase != SessionLifecyclePhase.Idle)
                AbortSession();
            return;
        }

        switch (Phase)
        {
            case SessionLifecyclePhase.Idle:
                return;
            case SessionLifecyclePhase.Lobby:
                if (nowUtc >= phaseEndsUtc)
                    StartNextRound(nowUtc);
                return;
            case SessionLifecyclePhase.Round:
                UpdateRound(nowUtc);
                return;
            case SessionLifecyclePhase.Results:
                if (CurrentRound != null && nowUtc >= CurrentRound.ResultsEndUtc)
                {
                    if (sessionRoundNumber >= TotalRounds)
                        StartVictory(nowUtc);
                    else if (ShouldBetAfterRound(sessionRoundNumber))
                        StartBetting(nowUtc);
                    else
                        StartNextRound(nowUtc);
                }
                return;
            case SessionLifecyclePhase.Betting:
                if (nowUtc >= phaseEndsUtc || AllConnectedHumansLocked())
                    StartNextRound(nowUtc);
                return;
            case SessionLifecyclePhase.Victory:
                if (nowUtc >= phaseEndsUtc)
                    StartLobby(nowUtc);
                return;
        }
    }

    public void OnHumanLoggedIn(PlayerSession session, DateTime nowUtc)
    {
        if (session.IsSimulated)
            return;

        if (Phase == SessionLifecyclePhase.Idle)
        {
            StartLobby(nowUtc);
            return;
        }

        bool waitForNextGame =
            Phase is SessionLifecyclePhase.Round or SessionLifecyclePhase.Results;
        if (waitForNextGame)
            waitingUserIds.Add(session.UserId);

        if (participants.TryGetValue(session.UserId, out SessionParticipant? participant))
        {
            participant.LifetimeWins = session.LifetimeWins;
            session.RestoreSessionScore(participant.Score);
            session.RestoreRoundState(participant.RoundResult);
        }
        else if (Phase != SessionLifecyclePhase.Victory && !waitForNextGame)
        {
            participants.Add(
                session.UserId,
                new SessionParticipant(
                    session.UserId,
                    session.Username,
                    session.LifetimeWins));
            session.ResetSessionScore();
        }

        if (Phase == SessionLifecyclePhase.Betting)
            EnsureBettingEntry(session);

        lobbyRosterDirty = true;

        SendCurrentPhase(session.ClientId, nowUtc);
        if (Phase == SessionLifecyclePhase.Lobby)
            BroadcastLobbyRoster();
        else if (Phase == SessionLifecyclePhase.Betting)
            BroadcastBettingState(nowUtc);
    }

    public void OnHumanDisconnected(PlayerSession session)
    {
        if (Phase == SessionLifecyclePhase.Round &&
            CurrentRound != null &&
            !IsWaiting(session))
        {
            CurrentRound.UnregisterPlayer(session, DateTime.UtcNow, server);
        }

        if (participants.TryGetValue(session.UserId, out SessionParticipant? participant))
        {
            participant.Score = session.TotalScore;
            participant.RoundResult = session.RoundResult;
        }

        lobbyRosterDirty = true;

        if (ConnectedHumans().Count == 0)
        {
            AbortSession();
            return;
        }

        if (Phase == SessionLifecyclePhase.Lobby)
            BroadcastLobbyRoster();
    }

    public bool TryHandleMessage(
        ushort clientId,
        NetworkMessageId messageId,
        Message message)
    {
        if (messageId == NetworkMessageId.SessionBet)
        {
            uint sessionId = message.GetUInt();
            int wager = message.GetInt();
            if (Phase != SessionLifecyclePhase.Betting ||
                sessionId != SessionId ||
                !sessions.TryGetValue(clientId, out PlayerSession? bettingSession) ||
                bettingSession.IsSimulated ||
                !bettingEntries.TryGetValue(bettingSession.UserId, out BettingEntry? entry) ||
                entry.Locked ||
                wager < 0 ||
                wager > GetMaximumWager(entry) ||
                !bettingSession.TrySpendSessionScore(wager))
                return true;

            entry.Wager = wager;
            entry.Locked = true;
            entry.ClientId = bettingSession.ClientId;
            SyncParticipantScore(bettingSession);
            BroadcastBettingUpdate(entry, bettingSession.TotalScore);
            if (AllConnectedHumansLocked())
                StartNextRound(DateTime.UtcNow);
            return true;
        }

        if (messageId == NetworkMessageId.MiniGameReady)
        {
            uint readyRoundId = message.GetUInt();
            MiniGameType readyGameType = (MiniGameType)message.GetUShort();
            if (Phase == SessionLifecyclePhase.Round &&
                CurrentRound != null &&
                !CurrentRound.IsStartScheduled &&
                CurrentRound.RoundId == readyRoundId &&
                CurrentRound.GameType == readyGameType &&
                sessions.TryGetValue(clientId, out PlayerSession? readySession) &&
                !readySession.IsSimulated &&
                !IsWaiting(readySession))
                readyClientIds.Add(clientId);
            return true;
        }

        if (messageId != NetworkMessageId.MiniGameInput)
            return false;

        uint roundId = message.GetUInt();
        MiniGameType gameType = (MiniGameType)message.GetUShort();
        if (Phase != SessionLifecyclePhase.Round ||
            CurrentRound == null ||
            CurrentRound.Phase != RoundPhase.Active ||
            !CurrentRound.IsStartScheduled ||
            DateTime.UtcNow < CurrentRound.StartsUtc ||
            CurrentRound.RoundId != roundId ||
            CurrentRound.GameType != gameType ||
            !sessions.TryGetValue(clientId, out PlayerSession? session) ||
            session.IsSimulated ||
            IsWaiting(session) ||
            session.SubmittedThisRound)
        {
            return true;
        }

        CurrentRound.HandleInput(message, session, server);
        SyncParticipantScore(session);
        return true;
    }

    public bool CanChat(PlayerSession session)
    {
        if (session.IsSimulated)
            return false;

        if (IsWaiting(session))
            return true;

        return Phase == SessionLifecyclePhase.Lobby ||
            Phase == SessionLifecyclePhase.Betting ||
            Phase == SessionLifecyclePhase.Victory ||
            (CurrentRound != null &&
                (Phase == SessionLifecyclePhase.Results || session.SubmittedThisRound));
    }

    public void SendCurrentPhase(ushort clientId, DateTime nowUtc)
    {
        if (sessions.TryGetValue(clientId, out PlayerSession? currentSession) &&
            IsWaiting(currentSession))
        {
            SendWaiting(clientId);
            return;
        }

        switch (Phase)
        {
            case SessionLifecyclePhase.Lobby:
                SendLobbyCountdown(clientId, nowUtc);
                SendLobbyRoster(clientId);
                break;
            case SessionLifecyclePhase.Round:
                if (CurrentRound != null && sessions.TryGetValue(clientId, out PlayerSession? session))
                {
                    if (CurrentRound.IsStartScheduled)
                        CurrentRound.RegisterPlayer(session, nowUtc);
                    server.Send(CurrentRound.CreateStartedMessage(), clientId);
                    CurrentRound.SynchronizePlayer(session, server);
                }
                break;
            case SessionLifecyclePhase.Results:
                if (CurrentRound != null)
                {
                    server.Send(CurrentRound.CreateStartedMessage(), clientId);
                    if (sessions.TryGetValue(clientId, out PlayerSession? resultsSession))
                        CurrentRound.SynchronizePlayer(resultsSession, server);
                    SendResults(clientId);
                }
                break;
            case SessionLifecyclePhase.Betting:
                SendBettingState(clientId, nowUtc);
                break;
            case SessionLifecyclePhase.Victory:
                if (CurrentRound != null)
                {
                    server.Send(CurrentRound.CreateStartedMessage(), clientId);
                    if (sessions.TryGetValue(clientId, out PlayerSession? victorySession))
                        CurrentRound.SynchronizePlayer(victorySession, server);
                    SendResults(clientId);
                }
                SendVictory(clientId, nowUtc);
                foreach (SessionParticipant winner in winners)
                    SystemChatToClientRequested?.Invoke(clientId, CreateWinnerMessage(winner));
                break;
        }
    }

    public void AbortSession()
    {
        if (Phase == SessionLifecyclePhase.Idle)
            return;

        RiptideConsoleLogger.Info($"Session {SessionId} discarded because no human players remain.");
        Phase = SessionLifecyclePhase.Idle;
        CurrentRound = null;
        readyClientIds.Clear();
        waitingUserIds.Clear();
        queuedNextGame = null;
        gamesPlayedInPass.Clear();
        sessionGamePlayCounts.Clear();
        bettingEntries.Clear();
        bettingTargetRoundNumber = 0;
        participants.Clear();
        winners = Array.Empty<SessionParticipant>();
        winningScore = 0;
        sessionRoundNumber = 0;
        cachedRoundResults = Array.Empty<LeaderboardEntry>();
        lobbyRosterDirty = true;
        foreach (PlayerSession session in sessions.Values)
            session.ResetSessionScore();
    }

    public bool TrySetSimulatedPlayerCount(int count) => simulatedPlayers.TrySetCount(count);

    public bool TryStartSessionNow(DateTime nowUtc, out string status)
    {
        if (ConnectedHumans().Count == 0)
        {
            status = "Cannot start a session without a connected human player.";
            return false;
        }

        if (Phase == SessionLifecyclePhase.Idle)
            StartLobby(nowUtc);

        if (Phase != SessionLifecyclePhase.Lobby)
        {
            status = $"Cannot start now while session {SessionId} is in phase {Phase}.";
            return false;
        }

        StartNextRound(nowUtc);
        status = $"Session {SessionId} started immediately; the lobby countdown was skipped.";
        return true;
    }

    public bool TrySkipToFinalRound(DateTime nowUtc, out string status)
    {
        if (ConnectedHumans().Count == 0)
        {
            status = "Cannot skip to the final round without a connected human player.";
            return false;
        }

        if (Phase is SessionLifecyclePhase.Idle or SessionLifecyclePhase.Victory)
        {
            status = $"Cannot skip to the final round while the session phase is {Phase}.";
            return false;
        }

        if (sessionRoundNumber >= TotalRounds)
        {
            status = $"Session {SessionId} is already on its final round.";
            return false;
        }

        foreach (PlayerSession session in ParticipatingHumans())
            SyncParticipantScore(session);

        RefundAndClearBets();

        SessionLifecyclePhase previousPhase = Phase;
        ushort previousRoundNumber = sessionRoundNumber;
        sessionRoundNumber = (ushort)(TotalRounds - 1);
        StartNextRound(nowUtc);
        status =
            $"Session {SessionId} skipped from {previousPhase} " +
            $"round {previousRoundNumber} to final round {sessionRoundNumber}/{TotalRounds}.";
        return true;
    }

    public void LogRound()
    {
        if (Phase == SessionLifecyclePhase.Idle)
        {
            RiptideConsoleLogger.Info("Session state: Idle.");
            return;
        }

        string round = CurrentRound == null
            ? string.Empty
            : $" round={sessionRoundNumber}/{TotalRounds} networkRound={CurrentRound.RoundId} game={CurrentRound.GameType}";
        RiptideConsoleLogger.Info(
            $"Session {SessionId} phase={Phase}{round} ends={phaseEndsUtc:O}");
    }

    public MiniGameType? ForcedGameType => forcedGame?.GameType;
    public MiniGameType? QueuedNextGameType => queuedNextGame?.GameType;
    public string AvailableGameNames => catalog.GetGameNames();

    public bool TrySetForcedGame(string gameName)
    {
        if (!catalog.TryGetGame(gameName, out IMiniGame game))
            return false;
        forcedGame = game;
        return true;
    }

    public void ClearForcedGame() => forcedGame = null;

    private ushort TotalRounds => (ushort)Math.Clamp(config.SessionRoundCount, 1, ushort.MaxValue);

    private void StartLobby(DateTime nowUtc)
    {
        SessionId = nextSessionId++;
        Phase = SessionLifecyclePhase.Lobby;
        phaseEndsUtc = nowUtc.AddSeconds(Math.Max(0.1f, config.LobbyDurationSeconds));
        CurrentRound = null;
        queuedNextGame = null;
        gamesPlayedInPass.Clear();
        sessionGamePlayCounts.Clear();
        participants.Clear();
        bettingEntries.Clear();
        bettingTargetRoundNumber = 0;
        winners = Array.Empty<SessionParticipant>();
        winningScore = 0;
        sessionRoundNumber = 0;
        waitingUserIds.Clear();

        foreach (PlayerSession session in sessions.Values)
        {
            session.ResetSessionScore();
            if (!session.IsSimulated)
            {
                participants[session.UserId] = new SessionParticipant(
                    session.UserId,
                    session.Username,
                    session.LifetimeWins);
            }
        }

        RiptideConsoleLogger.Info(
            $"Session {SessionId} lobby started for {config.LobbyDurationSeconds:0.###} seconds.");
        foreach (PlayerSession session in ConnectedHumans())
            SendLobbyCountdown(session.ClientId, nowUtc);
        BroadcastLobbyRoster();
    }

    private IMiniGame ChooseSessionGame()
    {
        if (gamesPlayedInPass.Count >= catalog.Count)
            gamesPlayedInPass.Clear();

        HashSet<MiniGameType> unavailable = new(gamesPlayedInPass);
        foreach ((MiniGameType gameType, int playCount) in sessionGamePlayCounts)
        {
            if (playCount >= 2)
                unavailable.Add(gameType);
        }

        return catalog.ChooseNext(
            random,
            unavailable,
            CurrentRound?.GameType);
    }

    private bool ShouldBetAfterRound(ushort roundNumber)
    {
        return config.Betting.Enabled &&
            roundNumber < TotalRounds &&
            config.Betting.AfterRounds != null &&
            config.Betting.AfterRounds.Contains(roundNumber);
    }

    private void RecordSessionGame(IMiniGame game)
    {
        gamesPlayedInPass.Add(game.GameType);
        sessionGamePlayCounts.TryGetValue(game.GameType, out int playCount);
        sessionGamePlayCounts[game.GameType] = playCount + 1;
    }
    private void StartNextRound(DateTime nowUtc)
    {
        AdmitWaitingPlayers();
        roundBetLosses.Clear();
        cachedRoundResults = Array.Empty<LeaderboardEntry>();
        sessionRoundNumber++;
        IMiniGame game = queuedNextGame ?? forcedGame ?? ChooseSessionGame();
        queuedNextGame = null;
        RecordSessionGame(game);
        float resultsDuration = sessionRoundNumber >= TotalRounds
            ? Math.Max(0.1f, config.FinalSummaryDurationSeconds)
            : Math.Max(0.1f, config.ResultsDurationSeconds);
        CurrentRound = game.CreateRound(new MiniGameRoundStartContext(
            nextRoundId++,
            nowUtc,
            Math.Max(0.1f, config.RoundDurationSeconds),
            resultsDuration,
            random));
        CurrentRound.ConfigureSession(SessionId, sessionRoundNumber, TotalRounds);

        foreach (PlayerSession session in sessions.Values)
            session.ResetRoundState();
        foreach (SessionParticipant participant in participants.Values)
            participant.RoundResult = PlayerRoundResult.Pending;
        readyClientIds.Clear();
        Phase = SessionLifecyclePhase.Round;
        phaseEndsUtc = nowUtc.AddSeconds(MiniGamePreparationTimeoutSeconds);
        RiptideConsoleLogger.Info(
            $"Session {SessionId} round {sessionRoundNumber}/{TotalRounds} preparing. " +
            $"NetworkRound={CurrentRound.RoundId} Game={CurrentRound.GameType} {CurrentRound.Describe()}.");
        foreach (PlayerSession session in ParticipatingHumans())
            server.Send(CurrentRound.CreateStartedMessage(), session.ClientId);
    }

    private void UpdateRound(DateTime nowUtc)
    {
        if (CurrentRound == null)
            return;

        if (!CurrentRound.IsStartScheduled)
        {
            PlayerSession[] humans = ParticipatingHumans().ToArray();
            if (humans.All(session => readyClientIds.Contains(session.ClientId)) ||
                nowUtc >= phaseEndsUtc)
                StartRoundCountdown(nowUtc);
            return;
        }

        if (nowUtc < CurrentRound.StartsUtc)
            return;

        CurrentRound.Update(nowUtc, RoundSessions(CurrentRound), server);
        if (CurrentRound.IsComplete || nowUtc >= CurrentRound.SubmissionDeadlineUtc)
            FinalizeRound(nowUtc);
    }

    private void StartRoundCountdown(DateTime nowUtc)
    {
        if (CurrentRound == null || CurrentRound.IsStartScheduled)
            return;

        CurrentRound.ScheduleStart(nowUtc.AddSeconds(MiniGameStartCountdownSeconds));
        foreach (PlayerSession session in RoundSessions(CurrentRound))
            CurrentRound.RegisterPlayer(session, nowUtc);
        phaseEndsUtc = CurrentRound.EndsUtc;
        readyClientIds.Clear();

        RiptideConsoleLogger.Info(
            $"Session {SessionId} round {sessionRoundNumber}/{TotalRounds} countdown started.");
        foreach (PlayerSession session in ParticipatingHumans())
            server.Send(CurrentRound.CreateCountdownMessage(), session.ClientId);
    }

    private void FinalizeRound(DateTime nowUtc)
    {
        if (CurrentRound == null)
            return;

        bool isFinalRound = sessionRoundNumber >= TotalRounds;
        queuedNextGame = isFinalRound
            ? null
            : forcedGame ?? ChooseSessionGame();
        CurrentRound.FinalizeRound(RoundSessions(CurrentRound), server);
        IReadOnlyList<SimulatedPlayerChat> simulatedChats =
            simulatedPlayers.CompleteRound(CurrentRound.MaximumScore);
        if (bettingTargetRoundNumber == sessionRoundNumber)
            ResolveBets();
        foreach (PlayerSession session in ParticipatingHumans())
            SyncParticipantScore(session);
        CurrentRound.MarkResults(nowUtc);
        cachedRoundResults = BuildLeaderboardEntries()
            .OrderByDescending(entry => entry.RoundScore)
            .ThenByDescending(entry => entry.TotalScore)
            .ThenBy(entry => entry.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Phase = SessionLifecyclePhase.Results;
        phaseEndsUtc = CurrentRound.ResultsEndUtc;

        RiptideConsoleLogger.Info(
            $"Session {SessionId} round {sessionRoundNumber}/{TotalRounds} finished. " +
            (isFinalRound ? "Final summary." : $"Next game={queuedNextGame!.GameType}."));
        foreach (PlayerSession recipient in ParticipatingHumans())
            SendResults(recipient.ClientId);

        foreach (SimulatedPlayerChat chat in simulatedChats)
            SimulatedPlayerChatRequested?.Invoke(chat.Sender, chat.Message);
    }

    private void StartBetting(DateTime nowUtc)
    {
        if (queuedNextGame == null)
        {
            StartNextRound(nowUtc);
            return;
        }

        AdmitWaitingPlayers();
        bettingEntries.Clear();
        bettingTargetRoundNumber = (ushort)(sessionRoundNumber + 1);
        CreateBettingEntries();
        Phase = SessionLifecyclePhase.Betting;
        float duration = Math.Max(0.1f, config.Betting.DurationSeconds);
        phaseEndsUtc = nowUtc.AddSeconds(duration);
        BroadcastBettingState(nowUtc);
    }

    private void CreateBettingEntries()
    {
        foreach (PlayerSession session in sessions.Values)
        {
            BettingEntry entry = new(session);
            bettingEntries[session.UserId] = entry;
            if (!session.IsSimulated)
                continue;

            int maximumWager = GetMaximumWager(entry);
            int wager = maximumWager <= 0
                ? 0
                : (int)random.NextInt64((long)maximumWager + 1);
            session.TrySpendSessionScore(wager);
            entry.Wager = wager;
            entry.Locked = true;
        }
    }

    private void EnsureBettingEntry(PlayerSession session)
    {
        if (bettingEntries.TryGetValue(session.UserId, out BettingEntry? existing))
        {
            existing.ClientId = session.ClientId;
            return;
        }

        bettingEntries[session.UserId] = new BettingEntry(session);
    }

    private int GetMaximumWager(BettingEntry entry)
    {
        return Math.Min(
            entry.AvailableScore,
            Math.Max(0, config.Betting.MaximumWager));
    }

    private bool AllConnectedHumansLocked()
    {
        PlayerSession[] humans = ConnectedHumans().ToArray();
        return humans.Length > 0 &&
            humans.All(session =>
                bettingEntries.TryGetValue(session.UserId, out BettingEntry? entry) &&
                entry.Locked);
    }

    private void ResolveBets()
    {
        int highestScore = sessions.Values
            .Select(session => session.RoundScore)
            .Concat(participants.Values.Select(participant => participant.RoundResult.Score))
            .DefaultIfEmpty(0)
            .Max();

        if (highestScore > 0)
        {
            foreach (BettingEntry entry in bettingEntries.Values)
            {
                if (entry.Wager <= 0)
                    continue;

                if (GetRoundScore(entry.UserId) != highestScore)
                {
                    roundBetLosses[entry.UserId] = -entry.Wager;
                    continue;
                }

                int payout = (int)Math.Min(int.MaxValue, (long)entry.Wager * 2);
                AddScore(entry.UserId, payout);
            }
        }
        else
        {
            foreach (BettingEntry entry in bettingEntries.Values)
            {
                if (entry.Wager > 0)
                    roundBetLosses[entry.UserId] = -entry.Wager;
            }
        }

        bettingEntries.Clear();
        bettingTargetRoundNumber = 0;
    }

    private int GetRoundScore(long userId)
    {
        PlayerSession? connected = sessions.Values.FirstOrDefault(session => session.UserId == userId);
        if (connected != null)
            return connected.RoundScore;
        return participants.TryGetValue(userId, out SessionParticipant? participant)
            ? participant.RoundResult.Score
            : 0;
    }

    private void AddScore(long userId, int amount)
    {
        PlayerSession? connected = sessions.Values.FirstOrDefault(session => session.UserId == userId);
        if (connected != null)
        {
            connected.AddSessionScore(amount);
            if (!connected.IsSimulated)
                SyncParticipantScore(connected);
            return;
        }

        if (participants.TryGetValue(userId, out SessionParticipant? participant))
            participant.Score = (int)Math.Min(int.MaxValue, (long)participant.Score + amount);
    }

    private void RefundAndClearBets()
    {
        foreach (BettingEntry entry in bettingEntries.Values)
            AddScore(entry.UserId, entry.Wager);
        bettingEntries.Clear();
        bettingTargetRoundNumber = 0;
    }

    private void StartVictory(DateTime nowUtc)
    {
        if (participants.Count == 0)
        {
            AbortSession();
            return;
        }

        winningScore = participants.Values.Max(participant => participant.Score);
        winners = participants.Values
            .Where(participant => participant.Score == winningScore)
            .OrderBy(participant => participant.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        persistence.IncrementWins(winners.Select(winner => winner.UserId));
        HashSet<long> winnerIds = winners.Select(winner => winner.UserId).ToHashSet();
        foreach (SessionParticipant winner in winners)
            winner.LifetimeWins++;
        lobbyRosterDirty = true;
        foreach (PlayerSession session in ConnectedHumans())
        {
            if (winnerIds.Contains(session.UserId))
                session.AddLifetimeWin();
        }

        Phase = SessionLifecyclePhase.Victory;
        phaseEndsUtc = nowUtc.AddSeconds(Math.Max(0.1f, config.VictoryDurationSeconds));
        queuedNextGame = null;
        RiptideConsoleLogger.Info(
            $"Session {SessionId} won by {string.Join(", ", winners.Select(winner => winner.Username))} " +
            $"with {winningScore} points.");
        foreach (PlayerSession session in ParticipatingHumans())
            SendVictory(session.ClientId, nowUtc);
        foreach (SessionParticipant winner in winners)
            SystemChatRequested?.Invoke(CreateWinnerMessage(winner));
    }

    private static string CreateWinnerMessage(SessionParticipant winner) =>
        $"Congratulations to {winner.Username} for winning!";

    private void SyncParticipantScore(PlayerSession session)
    {
        if (!session.IsSimulated && participants.TryGetValue(session.UserId, out SessionParticipant? participant))
        {
            participant.Score = session.TotalScore;
            participant.RoundResult = session.RoundResult;
        }
    }

    private void SendLobbyCountdown(ushort clientId, DateTime nowUtc)
    {
        Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageId.SessionCountdown);
        message.AddUInt(SessionId);
        message.AddLong(new DateTimeOffset(phaseEndsUtc).ToUnixTimeMilliseconds());
        message.AddFloat(Math.Max(0.1f, config.LobbyDurationSeconds));
        message.AddFloat(Math.Clamp(
            (float)(nowUtc - phaseEndsUtc.AddSeconds(-Math.Max(0.1f, config.LobbyDurationSeconds))).TotalSeconds,
            0f,
            Math.Max(0.1f, config.LobbyDurationSeconds)));
        message.AddUShort(TotalRounds);
        server.Send(message, clientId);
    }

    private void SendWaiting(ushort clientId)
    {
        Message message = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.SessionWaiting);
        message.AddUInt(SessionId);
        server.Send(message, clientId);
    }

    private void BroadcastLobbyRoster()
    {
        foreach (PlayerSession recipient in ConnectedHumans())
            SendLobbyRoster(recipient.ClientId);
    }

    private void SendLobbyRoster(ushort clientId)
    {
        if (lobbyRosterDirty)
        {
            cachedLobbyRoster = ConnectedHumans()
                .OrderByDescending(session => session.LifetimeWins)
                .ThenBy(session => session.Username, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            lobbyRosterDirty = false;
        }

        PlayerSession[] roster = cachedLobbyRoster;
        for (int start = 0; start < Math.Max(1, roster.Length); start += EntriesPerMessage)
        {
            int count = Math.Min(EntriesPerMessage, roster.Length - start);
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageId.SessionLobbyRoster);
            message.AddUInt(SessionId);
            message.AddUShort((ushort)roster.Length);
            message.AddUShort((ushort)start);
            message.AddUShort((ushort)Math.Max(0, count));
            for (int index = start; index < start + count; index++)
            {
                message.AddUShort(roster[index].ClientId);
                message.AddString(roster[index].Username);
                message.AddInt(roster[index].LifetimeWins);
            }
            server.Send(message, clientId);
            if (roster.Length == 0)
                break;
        }
    }

    private void SendResults(ushort clientId)
    {
        if (CurrentRound == null)
            return;

        bool isFinalRound = sessionRoundNumber >= TotalRounds;
        LeaderboardEntry[] ordered = cachedRoundResults;
        for (int start = 0; start < Math.Max(1, ordered.Length); start += EntriesPerMessage)
        {
            int count = Math.Min(EntriesPerMessage, ordered.Length - start);
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageId.MiniGameResults);
            message.AddUInt(CurrentRound.RoundId);
            message.AddBool(isFinalRound);
            message.AddUShort((ushort)(queuedNextGame?.GameType ?? 0));
            message.AddUShort((ushort)ordered.Length);
            message.AddUShort((ushort)start);
            message.AddUShort((ushort)Math.Max(0, count));
            for (int index = start; index < start + count; index++)
            {
                LeaderboardEntry entry = ordered[index];
                message.AddUShort(entry.ClientId);
                message.AddString(entry.Username);
                message.AddInt(entry.RoundScore);
                message.AddInt(entry.TotalScore);
                message.AddInt(entry.BetLoss);
            }
            server.Send(message, clientId);
            if (ordered.Length == 0)
                break;
        }
    }

    private IEnumerable<LeaderboardEntry> BuildLeaderboardEntries()
    {
        Dictionary<long, PlayerSession> connectedHumans = sessions.Values
            .Where(session => !session.IsSimulated)
            .ToDictionary(session => session.UserId);
        IEnumerable<LeaderboardEntry> humans = participants.Values.Select(participant =>
        {
            connectedHumans.TryGetValue(participant.UserId, out PlayerSession? connected);
            return new LeaderboardEntry(
                connected?.ClientId ?? 0,
                participant.Username,
                participant.RoundResult.Score,
                participant.Score,
                GetRoundBetLoss(participant.UserId));
        });
        IEnumerable<LeaderboardEntry> bots = sessions.Values
            .Where(session => session.IsSimulated)
            .Select(session => new LeaderboardEntry(
                session.ClientId,
                session.Username,
                session.RoundScore,
                session.TotalScore,
                GetRoundBetLoss(session.UserId)));
        return humans.Concat(bots);
    }

    private int GetRoundBetLoss(long userId)
    {
        return roundBetLosses.TryGetValue(userId, out int loss)
            ? loss
            : 0;
    }

    private void BroadcastBettingState(DateTime nowUtc)
    {
        BettingEntry[] ordered = BuildBettingEntries();
        foreach (PlayerSession recipient in ConnectedHumans())
            SendBettingState(recipient.ClientId, nowUtc, ordered);
    }

    private void SendBettingState(ushort clientId, DateTime nowUtc)
    {
        SendBettingState(clientId, nowUtc, BuildBettingEntries());
    }

    private BettingEntry[] BuildBettingEntries()
    {
        return bettingEntries.Values
            .OrderBy(entry => entry.IsSimulated)
            .ThenBy(entry => entry.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SendBettingState(
        ushort clientId,
        DateTime nowUtc,
        BettingEntry[] ordered)
    {
        float duration = Math.Max(0.1f, config.Betting.DurationSeconds);
        float elapsed = Math.Clamp(
            (float)(nowUtc - phaseEndsUtc.AddSeconds(-duration)).TotalSeconds,
            0f,
            duration);

        for (int start = 0; start < Math.Max(1, ordered.Length); start += EntriesPerMessage)
        {
            int count = Math.Min(EntriesPerMessage, ordered.Length - start);
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageId.SessionBettingState);
            message.AddUInt(SessionId);
            message.AddUShort(sessionRoundNumber);
            message.AddUShort(TotalRounds);
            message.AddUShort((ushort)(queuedNextGame?.GameType ?? 0));
            message.AddLong(new DateTimeOffset(phaseEndsUtc).ToUnixTimeMilliseconds());
            message.AddFloat(duration);
            message.AddFloat(elapsed);
            message.AddUShort((ushort)ordered.Length);
            message.AddUShort((ushort)start);
            message.AddUShort((ushort)Math.Max(0, count));
            for (int index = start; index < start + count; index++)
                AddBettingEntry(message, ordered[index]);
            server.Send(message, clientId);
            if (ordered.Length == 0)
                break;
        }
    }

    private void AddBettingEntry(Message message, BettingEntry entry)
    {
        message.AddLong(entry.UserId);
        message.AddUShort(entry.ClientId);
        message.AddString(entry.Username);
        message.AddInt(GetTotalScore(entry.UserId));
        message.AddInt(entry.AvailableScore);
        message.AddInt(entry.Wager);
        message.AddBool(entry.Locked);
        message.AddBool(entry.IsSimulated);
    }

    private int GetTotalScore(long userId)
    {
        PlayerSession? connected = sessions.Values.FirstOrDefault(session => session.UserId == userId);
        if (connected != null)
            return connected.TotalScore;
        return participants.TryGetValue(userId, out SessionParticipant? participant)
            ? participant.Score
            : 0;
    }

    private void BroadcastBettingUpdate(BettingEntry entry, int totalScore)
    {
        foreach (PlayerSession recipient in ConnectedHumans())
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageId.SessionBettingUpdate);
            message.AddUInt(SessionId);
            message.AddLong(entry.UserId);
            message.AddUShort(entry.ClientId);
            message.AddInt(totalScore);
            message.AddInt(entry.Wager);
            message.AddBool(entry.Locked);
            server.Send(message, recipient.ClientId);
        }
    }

    private void SendVictory(ushort clientId, DateTime nowUtc)
    {
        for (int start = 0; start < Math.Max(1, winners.Length); start += EntriesPerMessage)
        {
            int count = Math.Min(EntriesPerMessage, winners.Length - start);
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageId.SessionVictory);
            message.AddUInt(SessionId);
            message.AddInt(winningScore);
            message.AddLong(new DateTimeOffset(phaseEndsUtc).ToUnixTimeMilliseconds());
            message.AddFloat(Math.Max(0.1f, config.VictoryDurationSeconds));
            message.AddFloat(Math.Clamp(
                (float)(nowUtc - phaseEndsUtc.AddSeconds(-Math.Max(0.1f, config.VictoryDurationSeconds))).TotalSeconds,
                0f,
                Math.Max(0.1f, config.VictoryDurationSeconds)));
            message.AddUShort((ushort)winners.Length);
            message.AddUShort((ushort)start);
            message.AddUShort((ushort)Math.Max(0, count));
            for (int index = start; index < start + count; index++)
            {
                message.AddLong(winners[index].UserId);
                message.AddString(winners[index].Username);
            }
            server.Send(message, clientId);
            if (winners.Length == 0)
                break;
        }
    }

    private List<PlayerSession> ConnectedHumans()
    {
        return sessions.Values.Where(session => !session.IsSimulated).ToList();
    }

    private IEnumerable<PlayerSession> ParticipatingHumans()
    {
        return sessions.Values.Where(session =>
            !session.IsSimulated &&
            !IsWaiting(session));
    }

    private bool IsWaiting(PlayerSession session) =>
        waitingUserIds.Contains(session.UserId);

    private void AdmitWaitingPlayers()
    {
        if (waitingUserIds.Count == 0)
            return;

        foreach (PlayerSession session in ConnectedHumans())
        {
            if (!waitingUserIds.Contains(session.UserId) ||
                participants.ContainsKey(session.UserId))
            {
                continue;
            }

            participants.Add(
                session.UserId,
                new SessionParticipant(
                    session.UserId,
                    session.Username,
                    session.LifetimeWins,
                    session.TotalScore));
        }

        waitingUserIds.Clear();
        lobbyRosterDirty = true;
    }

    private IEnumerable<PlayerSession> RoundSessions(MiniGameRoundBase round)
    {
        return round.IncludesSimulatedPlayers
            ? sessions.Values.Where(session =>
                session.IsSimulated || !IsWaiting(session)).ToArray()
            : ParticipatingHumans();
    }
}
