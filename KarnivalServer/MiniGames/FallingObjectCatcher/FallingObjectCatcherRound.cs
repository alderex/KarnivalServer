using Riptide;

public sealed class FallingObjectCatcherRound : MiniGameRoundBase
{
    private const byte ObjectOutcomeEvent = 1;

    private readonly record struct DirectionChange(sbyte Direction, float StartsAtSeconds);

    private sealed class PlayerState
    {
        public PlayerState(IReadOnlyList<FallingObjectScheduleEntry> eligibleObjects)
        {
            EligibleObjects = eligibleObjects;
        }

        public IReadOnlyList<FallingObjectScheduleEntry> EligibleObjects { get; }
        public List<DirectionChange> DirectionChanges { get; } = new();
        public HashSet<ushort> EvaluatedObjectIds { get; } = new();
        public int CorrectCatchCount { get; set; }
        public int WrongCatchCount { get; set; }
    }

    private readonly IReadOnlyList<FallingObjectScheduleEntry> schedule;
    private readonly FallingObjectCatcherSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private readonly uint scheduleSeed;
    private readonly int targetObjectCount;
    private readonly int distractorCountPerShape;
    private readonly float firstCatchSeconds;
    private readonly float lastCatchSeconds;
    private readonly float minimumFallDurationSeconds;
    private readonly float maximumFallDurationSeconds;

    public FallingObjectCatcherRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        FallingObjectShape targetShape,
        uint scheduleSeed,
        int targetObjectCount,
        int distractorCountPerShape,
        float firstCatchSeconds,
        float lastCatchSeconds,
        float minimumFallDurationSeconds,
        float maximumFallDurationSeconds,
        IReadOnlyList<FallingObjectScheduleEntry> schedule,
        FallingObjectCatcherSettings settings)
        : base(
            roundId,
            MiniGameType.FallingObjectCatcher,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds)
    {
        TargetShape = targetShape;
        this.scheduleSeed = scheduleSeed;
        this.targetObjectCount = targetObjectCount;
        this.distractorCountPerShape = distractorCountPerShape;
        this.firstCatchSeconds = firstCatchSeconds;
        this.lastCatchSeconds = lastCatchSeconds;
        this.minimumFallDurationSeconds = minimumFallDurationSeconds;
        this.maximumFallDurationSeconds = maximumFallDurationSeconds;
        this.schedule = schedule;
        this.settings = settings;
    }

    public FallingObjectShape TargetShape { get; }

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        FallingObjectScheduleEntry[] eligibleObjects = schedule
            .Where(entry =>
                entry.CatchSeconds + GetCatchWindowSeconds(entry) > elapsedSeconds)
            .ToArray();
        playerStates.Add(session.ClientId, new PlayerState(eligibleObjects));
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
            foreach (FallingObjectScheduleEntry entry in state.EligibleObjects)
            {
                if (!state.EvaluatedObjectIds.Contains(entry.ObjectId) &&
                    elapsedSeconds >= entry.CatchSeconds +
                        GetCatchWindowSeconds(entry) +
                        Math.Max(0f, settings.InputGraceSeconds))
                {
                    EvaluateObject(session, state, entry, server);
                }
            }

            TryCompletePlayer(session, state, server);
        }
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        sbyte direction = message.GetSByte();
        float startsAtSeconds = message.GetFloat();
        if (direction < -1 || direction > 1 || float.IsNaN(startsAtSeconds))
            return;

        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        float inputGraceSeconds = Math.Max(0f, settings.InputGraceSeconds);
        if (startsAtSeconds < 0f ||
            startsAtSeconds > DurationSeconds ||
            startsAtSeconds > receivedAtSeconds + Math.Max(0f, settings.FutureInputToleranceSeconds) ||
            receivedAtSeconds - startsAtSeconds > inputGraceSeconds ||
            state.DirectionChanges.Count >= 256)
        {
            return;
        }

        if (state.DirectionChanges.Count > 0)
        {
            DirectionChange previous = state.DirectionChanges[^1];
            if (startsAtSeconds < previous.StartsAtSeconds || direction == previous.Direction)
                return;
        }

        state.DirectionChanges.Add(new DirectionChange(direction, startsAtSeconds));
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            PlayerState state = playerStates[session.ClientId];
            foreach (FallingObjectScheduleEntry entry in state.EligibleObjects)
            {
                if (!state.EvaluatedObjectIds.Contains(entry.ObjectId))
                    EvaluateObject(session, state, entry, server);
            }

            TryCompletePlayer(session, state, server);
            if (!session.SubmittedThisRound)
                session.MarkMissedRound();
        }
    }

    public override string Describe()
    {
        return $"target={TargetShape} seed={scheduleSeed} objects={schedule.Count}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)TargetShape);
        message.AddUInt(scheduleSeed);
        message.AddUShort((ushort)targetObjectCount);
        message.AddUShort((ushort)distractorCountPerShape);
        message.AddFloat(firstCatchSeconds);
        message.AddFloat(lastCatchSeconds);
        message.AddFloat(minimumFallDurationSeconds);
        message.AddFloat(maximumFallDurationSeconds);
        message.AddFloat(Math.Max(0.01f, settings.CatcherSpeedNormalizedPerSecond));
        message.AddFloat(Math.Clamp(settings.CatcherHalfWidthNormalized, 0.01f, 0.49f));
        message.AddFloat(Math.Clamp(settings.CatchToleranceNormalized, 0.01f, 0.49f));
        message.AddFloat(Math.Clamp(settings.CatchWindowFallDurationFraction, 0.05f, 1f));
        message.AddUShort((ushort)schedule.Count);
        foreach (FallingObjectScheduleEntry entry in schedule)
        {
            message.AddUShort(entry.ObjectId);
            message.AddByte((byte)entry.Shape);
            message.AddFloat(entry.XNormalized);
            message.AddFloat(entry.SpawnSeconds);
            message.AddFloat(entry.CatchSeconds);
            message.AddFloat(entry.FallDurationSeconds);
        }
    }

    private void EvaluateObject(
        PlayerSession session,
        PlayerState state,
        FallingObjectScheduleEntry entry,
        Riptide.Server server)
    {
        bool caught = WasCaughtDuringWindow(state, entry);
        bool correct = caught && entry.Shape == TargetShape;
        int scoreDelta = 0;
        if (correct)
        {
            state.CorrectCatchCount++;
            scoreDelta = Math.Max(0, settings.CorrectCatchPoints);
        }
        else if (caught)
        {
            state.WrongCatchCount++;
            scoreDelta = -Math.Max(0, settings.WrongCatchPenalty);
        }

        state.EvaluatedObjectIds.Add(entry.ObjectId);
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(ObjectOutcomeEvent);
        outcome.AddUShort(entry.ObjectId);
        outcome.AddBool(caught);
        outcome.AddBool(correct);
        outcome.AddInt(scoreDelta);
        outcome.AddInt(GetScore(state));
        server.Send(outcome, session.ClientId);
    }

    private void TryCompletePlayer(
        PlayerSession session,
        PlayerState state,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound ||
            state.EvaluatedObjectIds.Count < state.EligibleObjects.Count)
        {
            return;
        }

        CompleteSubmission(session, GetScore(state), server);
    }

    private int GetScore(PlayerState state)
    {
        return Math.Max(
            0,
            (state.CorrectCatchCount * Math.Max(0, settings.CorrectCatchPoints)) -
            (state.WrongCatchCount * Math.Max(0, settings.WrongCatchPenalty)));
    }

    private bool WasCaughtDuringWindow(
        PlayerState state,
        FallingObjectScheduleEntry entry)
    {
        float windowStartsAt = entry.CatchSeconds;
        float windowEndsAt = Math.Min(
            DurationSeconds,
            windowStartsAt + GetCatchWindowSeconds(entry));
        float tolerance = Math.Clamp(
            settings.CatchToleranceNormalized,
            0.01f,
            0.49f);
        float previousX = GetCatcherXAt(state, windowStartsAt);

        foreach (DirectionChange change in state.DirectionChanges)
        {
            if (change.StartsAtSeconds <= windowStartsAt)
                continue;
            if (change.StartsAtSeconds >= windowEndsAt)
                break;

            float currentX = GetCatcherXAt(state, change.StartsAtSeconds);
            if (SegmentOverlapsTarget(previousX, currentX, entry.XNormalized, tolerance))
                return true;

            previousX = currentX;
        }

        float finalX = GetCatcherXAt(state, windowEndsAt);
        return SegmentOverlapsTarget(previousX, finalX, entry.XNormalized, tolerance);
    }

    private static bool SegmentOverlapsTarget(
        float startX,
        float endX,
        float targetX,
        float tolerance)
    {
        float minimumX = Math.Min(startX, endX);
        float maximumX = Math.Max(startX, endX);
        return minimumX <= targetX + tolerance &&
            maximumX >= targetX - tolerance;
    }

    private float GetCatchWindowSeconds(FallingObjectScheduleEntry entry)
    {
        return entry.FallDurationSeconds * Math.Clamp(
            settings.CatchWindowFallDurationFraction,
            0.05f,
            1f);
    }

    private float GetCatcherXAt(PlayerState state, float targetSeconds)
    {
        float position = 0.5f;
        float previousSeconds = 0f;
        sbyte direction = 0;
        float speed = Math.Max(0.01f, settings.CatcherSpeedNormalizedPerSecond);
        float halfWidth = Math.Clamp(settings.CatcherHalfWidthNormalized, 0.01f, 0.49f);
        foreach (DirectionChange change in state.DirectionChanges)
        {
            if (change.StartsAtSeconds > targetSeconds)
                break;

            position = Math.Clamp(
                position + (direction * speed * (change.StartsAtSeconds - previousSeconds)),
                halfWidth,
                1f - halfWidth);
            previousSeconds = change.StartsAtSeconds;
            direction = change.Direction;
        }

        return Math.Clamp(
            position + (direction * speed * (targetSeconds - previousSeconds)),
            halfWidth,
            1f - halfWidth);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
