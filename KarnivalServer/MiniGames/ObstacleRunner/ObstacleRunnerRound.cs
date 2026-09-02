using Riptide;

public sealed class ObstacleRunnerRound : MiniGameRoundBase
{
    private const byte ObstacleOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public PlayerState(IReadOnlyList<ObstacleScheduleEntry> eligibleObstacles)
        {
            EligibleObstacles = eligibleObstacles;
        }

        public IReadOnlyList<ObstacleScheduleEntry> EligibleObstacles { get; }
        public List<ActionInterval> Actions { get; } = new();
        public HashSet<ushort> EvaluatedObstacleIds { get; } = new();
        public int ClearedCount { get; set; }
    }

    private readonly record struct ActionInterval(
        RunnerAction Action,
        float StartsAtSeconds,
        float EndsAtSeconds);

    private readonly IReadOnlyList<ObstacleScheduleEntry> schedule;
    private readonly ObstacleRunnerSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public ObstacleRunnerRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        IReadOnlyList<ObstacleScheduleEntry> schedule,
        ObstacleRunnerSettings settings)
        : base(
            roundId,
            MiniGameType.ObstacleRunner,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds)
    {
        this.schedule = schedule;
        this.settings = settings;
    }

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        ObstacleScheduleEntry[] eligible = schedule
            .Where(obstacle => obstacle.CollisionSeconds > elapsedSeconds)
            .ToArray();
        playerStates.Add(session.ClientId, new PlayerState(eligible));
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, nowUtc);
            PlayerState state = playerStates[session.ClientId];
            foreach (ObstacleScheduleEntry obstacle in state.EligibleObstacles)
            {
                if (state.EvaluatedObstacleIds.Contains(obstacle.ObstacleId) ||
                    elapsedSeconds < obstacle.CollisionSeconds + settings.InputGraceSeconds)
                {
                    continue;
                }

                EvaluateObstacle(session, state, obstacle, server);
            }

            TryCompletePlayer(session, state, server);
        }
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        RunnerAction action = (RunnerAction)message.GetByte();
        float startsAtSeconds = message.GetFloat();
        if (action is not RunnerAction.Jumping and not RunnerAction.Sliding)
            return;

        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        if (float.IsNaN(startsAtSeconds) ||
            startsAtSeconds < 0f ||
            startsAtSeconds > DurationSeconds ||
            startsAtSeconds > receivedAtSeconds + settings.FutureInputToleranceSeconds ||
            receivedAtSeconds - startsAtSeconds > settings.InputGraceSeconds)
        {
            return;
        }

        float durationSeconds = action == RunnerAction.Jumping
            ? settings.JumpDurationSeconds
            : settings.SlideDurationSeconds;
        ActionInterval interval = new(
            action,
            startsAtSeconds,
            startsAtSeconds + Math.Max(0.1f, durationSeconds));
        if (state.Actions.Any(existing =>
            interval.StartsAtSeconds < existing.EndsAtSeconds &&
            interval.EndsAtSeconds > existing.StartsAtSeconds))
        {
            return;
        }

        state.Actions.Add(interval);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            PlayerState state = playerStates[session.ClientId];
            foreach (ObstacleScheduleEntry obstacle in state.EligibleObstacles)
            {
                if (!state.EvaluatedObstacleIds.Contains(obstacle.ObstacleId))
                    EvaluateObstacle(session, state, obstacle, server);
            }

            TryCompletePlayer(session, state, server);
            if (!session.SubmittedThisRound)
                session.MarkMissedRound();
        }
    }

    public override string Describe()
    {
        return $"obstacles={schedule.Count}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddFloat(Math.Max(0.1f, settings.ObstacleTravelSeconds));
        message.AddFloat(Math.Max(0.1f, settings.JumpDurationSeconds));
        message.AddFloat(Math.Max(0.1f, settings.SlideDurationSeconds));
        message.AddUShort((ushort)schedule.Count);
        foreach (ObstacleScheduleEntry obstacle in schedule)
        {
            message.AddUShort(obstacle.ObstacleId);
            message.AddByte((byte)obstacle.Height);
            message.AddFloat(obstacle.CollisionSeconds);
        }
    }

    private void EvaluateObstacle(
        PlayerSession session,
        PlayerState state,
        ObstacleScheduleEntry obstacle,
        Riptide.Server server)
    {
        RunnerAction action = GetActionAt(state, obstacle.CollisionSeconds);
        bool cleared = IsSuccessful(obstacle.Height, action);
        state.EvaluatedObstacleIds.Add(obstacle.ObstacleId);
        if (cleared)
            state.ClearedCount++;

        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(ObstacleOutcomeEvent);
        outcome.AddUShort(obstacle.ObstacleId);
        outcome.AddBool(cleared);
        outcome.AddByte((byte)action);
        outcome.AddUShort((ushort)state.ClearedCount);
        outcome.AddUShort((ushort)state.EvaluatedObstacleIds.Count);
        outcome.AddUShort((ushort)state.EligibleObstacles.Count);
        server.Send(outcome, session.ClientId);
    }

    private void TryCompletePlayer(
        PlayerSession session,
        PlayerState state,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound ||
            state.EvaluatedObstacleIds.Count < state.EligibleObstacles.Count)
        {
            return;
        }

        int score = state.EligibleObstacles.Count == 0
            ? 0
            : (int)MathF.Round(100f * state.ClearedCount / state.EligibleObstacles.Count);
        CompleteSubmission(
            session,
            score,
            server,
            completedAtSeconds: DurationSeconds);
    }

    private RunnerAction GetActionAt(PlayerState state, float collisionSeconds)
    {
        foreach (ActionInterval interval in state.Actions)
        {
            if (interval.StartsAtSeconds <= collisionSeconds &&
                collisionSeconds < interval.EndsAtSeconds)
            {
                return interval.Action;
            }
        }

        return RunnerAction.Running;
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }

    private static bool IsSuccessful(ObstacleHeight height, RunnerAction action)
    {
        return height switch
        {
            ObstacleHeight.High => action != RunnerAction.Jumping,
            ObstacleHeight.Medium => action == RunnerAction.Sliding,
            ObstacleHeight.Low => action == RunnerAction.Jumping,
            _ => false,
        };
    }
}
