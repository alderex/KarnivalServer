using Riptide;
using System.Diagnostics.CodeAnalysis;
using System.Text;

public sealed class Server
{
    private const int MinimumUsernameLength = 1;
    private const int MaximumUsernameLength = 32;
    private const int Sha256HexLength = 64;

    private readonly ServerConfig config;
    private readonly SqliteAccountPersistence persistence;
    private readonly Dictionary<ushort, PlayerSession> sessions = new();
    private readonly Riptide.Server server;
    private readonly ServerCommandConsole commandConsole = new();
    private readonly MiniGameRoundManager miniGameRoundManager;
    private readonly ChatService chatService;

    private bool commandStopRequested;

    public Server(ServerConfig config)
    {
        this.config = config;
        Random random = CreateRandom(config.NetworkSimulation.RandomSeed);
        RiptideConsoleLogger.Info($"Startup: initializing account database at {config.DatabasePath}...");
        persistence = new SqliteAccountPersistence(config.DatabasePath);
        RiptideConsoleLogger.Info("Startup: creating Riptide server...");
        server = new Riptide.Server(TransportFactory.Create(config.NetworkSimulation), "KARNIVAL")
        {
            TimeoutTime = config.ConnectionTimeoutMilliseconds,
        };
        miniGameRoundManager = new MiniGameRoundManager(
            config,
            random,
            sessions,
            server,
            MiniGameCatalog.CreateDefault(config),
            persistence);
        chatService = new ChatService(
            sessions,
            server,
            miniGameRoundManager.CanChat);
        miniGameRoundManager.SimulatedPlayerChatRequested +=
            chatService.BroadcastSimulatedMessage;
        miniGameRoundManager.SystemChatRequested +=
            chatService.BroadcastSystemMessage;
        miniGameRoundManager.SystemChatToClientRequested +=
            chatService.SendSystemMessage;
        RegisterEventHandlers();
    }

    public void Run(CancellationToken shutdown)
    {
        RiptideConsoleLogger.Info($"Startup: starting Riptide server on port {config.Port}...");
        server.Start(config.Port, config.MaxClientCount, useMessageHandlers: false);
        RiptideConsoleLogger.Info("Startup: Riptide server started.");
        commandConsole.Start();
        RiptideConsoleLogger.Info("Server command prompt ready. Type 'help' for commands.");

        while (!shutdown.IsCancellationRequested && !commandStopRequested)
            RunTick();

        commandConsole.Stop();
        RiptideConsoleLogger.Info("Stopping server...");
        server.Stop();
        RiptideConsoleLogger.Info("Server stopped.");
    }

    private void RunTick()
    {
        DateTime tickStartedAt = DateTime.UtcNow;
        server.Update();
        ProcessConsoleCommands();
        miniGameRoundManager.Update(DateTime.UtcNow);
        SleepUntilNextTick(tickStartedAt);
    }

    private void ProcessConsoleCommands()
    {
        while (commandConsole.TryDequeue(out string command))
            HandleConsoleCommand(command);
    }

    private void HandleConsoleCommand(string command)
    {
        string[] args = SplitCommandLine(command);
        if (args.Length == 0)
            return;

        switch (args[0].ToLowerInvariant())
        {
            case "help":
            case "?":
                RiptideConsoleLogger.Info(
                    "Commands: help, players, bots [count], round, session [start|final], " +
                    "game [name|random], resetdb, stop");
                return;
            case "players":
                LogPlayers();
                return;
            case "bots":
                HandleBotsCommand(args);
                return;
            case "round":
                miniGameRoundManager.LogRound();
                return;
            case "session":
                HandleSessionCommand(args);
                return;
            case "game":
                HandleGameCommand(args);
                return;
            case "resetdb":
                miniGameRoundManager.AbortSession();
                persistence.ResetDatabase();
                ushort[] connectedClientIds = sessions.Values
                    .Where(session => !session.IsSimulated)
                    .Select(session => session.ClientId)
                    .ToArray();
                foreach (ushort clientId in connectedClientIds)
                    sessions.Remove(clientId);
                RiptideConsoleLogger.Warning(
                    "Account database reset. Connected clients must log in again.");
                return;
            case "stop":
            case "quit":
            case "exit":
                commandStopRequested = true;
                RiptideConsoleLogger.Info("Shutdown requested from server command prompt.");
                return;
            default:
                RiptideConsoleLogger.Warning($"Unknown command '{args[0]}'. Type 'help' for commands.");
                return;
        }
    }

    private void HandleSessionCommand(string[] args)
    {
        if (args.Length == 1)
        {
            miniGameRoundManager.LogRound();
            RiptideConsoleLogger.Info(
                "Usage: session start (skip the lobby countdown) | " +
                "session final (jump to the final round)");
            return;
        }

        if (args.Length != 2)
        {
            RiptideConsoleLogger.Warning("Usage: session [start|final]");
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        bool succeeded;
        string status;
        switch (args[1].ToLowerInvariant())
        {
            case "start":
                succeeded = miniGameRoundManager.TryStartSessionNow(nowUtc, out status);
                break;
            case "final":
                succeeded = miniGameRoundManager.TrySkipToFinalRound(nowUtc, out status);
                break;
            default:
                RiptideConsoleLogger.Warning("Usage: session [start|final]");
                return;
        }

        if (succeeded)
            RiptideConsoleLogger.Info(status);
        else
            RiptideConsoleLogger.Warning(status);
    }

    private void HandleBotsCommand(string[] args)
    {
        if (args.Length == 1)
        {
            RiptideConsoleLogger.Info(
                $"Simulated players: {miniGameRoundManager.SimulatedPlayerCount}/" +
                $"{miniGameRoundManager.MaximumSimulatedPlayerCount}. " +
                "Usage: bots [count]");
            return;
        }

        if (args.Length != 2 ||
            !int.TryParse(args[1], out int count) ||
            !miniGameRoundManager.TrySetSimulatedPlayerCount(count))
        {
            RiptideConsoleLogger.Warning(
                $"Usage: bots [count], where count is between 0 and " +
                $"{miniGameRoundManager.MaximumSimulatedPlayerCount}.");
            return;
        }

        RiptideConsoleLogger.Info(
            $"Simulated player count set to " +
            $"{miniGameRoundManager.SimulatedPlayerCount}.");
    }

    private void HandleGameCommand(string[] args)
    {
        if (args.Length == 1)
        {
            string selection = miniGameRoundManager.ForcedGameType?.ToString() ?? "random";
            string queued = miniGameRoundManager.QueuedNextGameType.HasValue
                ? $" Next already queued: {miniGameRoundManager.QueuedNextGameType}."
                : string.Empty;
            RiptideConsoleLogger.Info(
                $"Game selection: {selection}. " +
                $"Available: {miniGameRoundManager.AvailableGameNames}.{queued}");
            return;
        }

        if (args.Length != 2)
        {
            RiptideConsoleLogger.Warning("Usage: game [name|random]");
            return;
        }

        if (args[1].Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            miniGameRoundManager.ClearForcedGame();
            LogGameOverrideChange("Game override cleared; normal rotation restored.");
            return;
        }

        if (!miniGameRoundManager.TrySetForcedGame(args[1]))
        {
            RiptideConsoleLogger.Warning(
                $"Unknown mini game '{args[1]}'. " +
                $"Available: {miniGameRoundManager.AvailableGameNames}.");
            return;
        }

        LogGameOverrideChange(
            $"Game override set to {miniGameRoundManager.ForcedGameType}; " +
            "it will repeat until cleared.");
    }

    private void LogGameOverrideChange(string message)
    {
        if (miniGameRoundManager.QueuedNextGameType.HasValue)
        {
            message +=
                $" The already-announced {miniGameRoundManager.QueuedNextGameType} round " +
                "will run first.";
        }

        RiptideConsoleLogger.Info(message);
    }

    private void LogPlayers()
    {
        if (sessions.Count == 0)
        {
            RiptideConsoleLogger.Info("No logged-in players.");
            return;
        }

        foreach (PlayerSession session in sessions.Values.OrderBy(session => session.ClientId))
        {
            string simulated = session.IsSimulated ? " [bot]" : string.Empty;
            string submitted = session.SubmittedThisRound
                ? $" round={session.RoundScore}"
                : " pending";
            RiptideConsoleLogger.Info(
                $"{session.ClientId}: {session.Username}{simulated} " +
                $"total={session.TotalScore} wins={session.LifetimeWins}{submitted}");
        }
    }

    private void RegisterEventHandlers()
    {
        server.ClientConnected += OnClientConnected;
        server.ClientDisconnected += OnClientDisconnected;
        server.ConnectionFailed += OnConnectionFailed;
        server.MessageReceived += OnMessageReceived;
    }

    private void OnClientConnected(object? sender, ServerConnectedEventArgs eventArgs)
    {
        eventArgs.Client.MaxSendAttempts = Math.Max(15, config.ReliableMaxSendAttempts);
        RiptideConsoleLogger.Info(
            $"Client {eventArgs.Client.Id} connected from {eventArgs.Client}. Waiting for login. Active clients: {server.ClientCount}/{server.MaxClientCount}");
    }

    private void OnClientDisconnected(object? sender, ServerDisconnectedEventArgs eventArgs)
    {
        chatService.RemoveClient(eventArgs.Client.Id);
        if (sessions.Remove(eventArgs.Client.Id, out PlayerSession? disconnectedSession) &&
            !disconnectedSession.IsSimulated)
        {
            miniGameRoundManager.OnHumanDisconnected(disconnectedSession);
        }
        RiptideConsoleLogger.Info(
            $"Client {eventArgs.Client.Id} disconnected ({eventArgs.Reason}). Active clients: {server.ClientCount}/{server.MaxClientCount}");
    }

    private void OnConnectionFailed(object? sender, ServerConnectionFailedEventArgs eventArgs)
    {
        RiptideConsoleLogger.Warning($"Connection from {eventArgs.Client} failed before the handshake completed.");
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs eventArgs)
    {
        if (eventArgs.MessageId == (ushort)NetworkMessageId.LoginRequest)
        {
            HandleLoginRequest(eventArgs.FromConnection.Id, eventArgs.Message);
            return;
        }

        if (!sessions.ContainsKey(eventArgs.FromConnection.Id))
            return;

        NetworkMessageId messageId = (NetworkMessageId)eventArgs.MessageId;
        if (chatService.TryHandleMessage(eventArgs.FromConnection.Id, messageId, eventArgs.Message))
            return;
        if (miniGameRoundManager.TryHandleMessage(eventArgs.FromConnection.Id, messageId, eventArgs.Message))
            return;

        RiptideConsoleLogger.Warning(
            $"Unhandled message {eventArgs.MessageId} from client {eventArgs.FromConnection.Id} ({eventArgs.Message.BytesInUse} bytes).");
    }

    private void HandleLoginRequest(ushort clientId, Message message)
    {
        ushort protocolVersion = message.GetUShort();
        if (protocolVersion != KarnivalProtocol.Version)
        {
            SendLoginResult(
                clientId,
                false,
                $"Protocol mismatch. Server is version {KarnivalProtocol.Version}; client is version {protocolVersion}.");
            return;
        }

        string username = NormalizeUsername(message.GetString());
        string loginHash = NormalizeLoginHash(message.GetString());

        if (!IsValidUsername(username))
        {
            SendLoginResult(clientId, false, "Username can use letters, numbers, underscore, or hyphen.");
            return;
        }

        if (!IsValidLoginHash(loginHash))
        {
            SendLoginResult(clientId, false, "Invalid login request.");
            return;
        }

        if (sessions.ContainsKey(clientId))
        {
            SendLoginResult(clientId, true, "Already logged in.");
            miniGameRoundManager.SendCurrentPhase(clientId, DateTime.UtcNow);
            return;
        }

        try
        {
            AccountLoginRecord? login = persistence.TryGetOrCreateLogin(username, loginHash);
            if (login == null)
            {
                SendLoginResult(clientId, false, "Incorrect password.");
                return;
            }

            if (sessions.Values.Any(session => session.UserId == login.UserId))
            {
                SendLoginResult(clientId, false, "That account is already connected.");
                return;
            }

            sessions[clientId] = new PlayerSession(clientId, login.UserId, login.Username, login.Wins);
            SendLoginResult(clientId, true, "Login successful.");
            miniGameRoundManager.OnHumanLoggedIn(sessions[clientId], DateTime.UtcNow);
            RiptideConsoleLogger.Info($"Client {clientId} logged in as '{login.Username}' (user {login.UserId}).");
        }
        catch (Exception ex)
        {
            RiptideConsoleLogger.Error($"Login failed for client {clientId}: {ex}");
            SendLoginResult(clientId, false, "Login failed.");
        }
    }

    private void SendLoginResult(ushort clientId, bool success, string status)
    {
        Message response = Message.Create(MessageSendMode.Reliable, NetworkMessageId.LoginResult);
        response.AddUShort(KarnivalProtocol.Version);
        response.AddBool(success);
        response.AddString(status);
        server.Send(response, clientId);
    }

    private void SleepUntilNextTick(DateTime tickStartedAt)
    {
        TimeSpan elapsed = DateTime.UtcNow - tickStartedAt;
        TimeSpan remaining = config.TickInterval - elapsed;
        if (remaining > TimeSpan.Zero)
            Thread.Sleep(remaining);
    }

    private static Random CreateRandom(int? seed)
    {
        return seed.HasValue ? new Random(seed.Value) : new Random();
    }

    private static string NormalizeUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim().ToLowerInvariant();
    }

    private static string NormalizeLoginHash(string loginHash)
    {
        return string.IsNullOrWhiteSpace(loginHash) ? string.Empty : loginHash.Trim().ToLowerInvariant();
    }

    private static bool IsValidUsername([NotNullWhen(true)] string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < MinimumUsernameLength || username.Length > MaximumUsernameLength)
            return false;

        foreach (char character in username)
        {
            if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                return false;
        }

        return true;
    }

    private static bool IsValidLoginHash(string loginHash)
    {
        if (loginHash.Length != Sha256HexLength)
            return false;

        foreach (char character in loginHash)
        {
            bool isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
                return false;
        }

        return true;
    }

    private static string[] SplitCommandLine(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Array.Empty<string>();

        List<string> args = new();
        StringBuilder current = new();
        bool inQuotes = false;
        foreach (char character in command)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
    }
}
