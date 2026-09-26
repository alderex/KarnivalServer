using Riptide;

public sealed class PlateStackerRound : MiniGameRoundBase
{
    private const byte PlateOutcomeEvent = 1;

    // Presentation-space geometry, validated against the Unity scene.
    public const float SpawnY = 248f;
    public const float FirstContactY = -130f;
    public const float StackSpacing = 22f;
    public const float ExitY = -247f;

    public static float FallSpeed(PlateStackerScheduleEntry entry) =>
        (SpawnY - FirstContactY) / Math.Max(0.1f, entry.FallDurationSeconds);

    public static float ContactSeconds(PlateStackerScheduleEntry entry, int caughtCount) =>
        entry.SpawnSeconds +
        (SpawnY - FirstContactY - caughtCount * StackSpacing) / FallSpeed(entry);

    public static float ExitSeconds(PlateStackerScheduleEntry entry) =>
        entry.SpawnSeconds + (SpawnY - ExitY) / FallSpeed(entry);

    private readonly record struct DirectionChange(
        sbyte Direction,
        float StartsAtSeconds);

    private sealed class PlayerState
    {
        public PlayerState(
            IReadOnlyList<PlateStackerScheduleEntry> eligiblePlates,
            float startsAtSeconds)
        {
            EligiblePlates = eligiblePlates;
            SimulatedAtSeconds = startsAtSeconds;
            PlateOffsets.Add(0f);
        }

        public IReadOnlyList<PlateStackerScheduleEntry> EligiblePlates { get; }
        public List<DirectionChange> DirectionChanges { get; } = new();
        public HashSet<ushort> EvaluatedPlateIds { get; } = new();
        public List<float> PlateOffsets { get; } = new();
        public float BaseXNormalized { get; set; } = 0.5f;
        public float SimulatedAtSeconds { get; set; }
        public sbyte Direction { get; set; }
        public int NextDirectionChangeIndex { get; set; }
        public int SuccessfulPlateCount { get; set; }
        public float LastLandingSeconds { get; set; }
        public bool Collapsed { get; set; }
    }

    private readonly IReadOnlyList<PlateStackerScheduleEntry> schedule;
    private readonly PlateStackerSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private readonly uint scheduleSeed;
    private readonly float firstLandingSeconds;
    private readonly float lastLandingSeconds;
    private readonly float minimumFallDurationSeconds;
    private readonly float maximumFallDurationSeconds;

    public PlateStackerRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        uint scheduleSeed,
        float firstLandingSeconds,
        float lastLandingSeconds,
        float minimumFallDurationSeconds,
        float maximumFallDurationSeconds,
        IReadOnlyList<PlateStackerScheduleEntry> schedule,
        PlateStackerSettings settings)
        : base(
            roundId,
            MiniGameType.PlateStacker,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            GetMaximumScore(settings))
    {
        this.scheduleSeed = scheduleSeed;
        this.firstLandingSeconds = firstLandingSeconds;
        this.lastLandingSeconds = lastLandingSeconds;
        this.minimumFallDurationSeconds = minimumFallDurationSeconds;
        this.maximumFallDurationSeconds = maximumFallDurationSeconds;
        this.schedule = schedule;
        this.settings = settings;
    }

    public IReadOnlyList<PlateStackerScheduleEntry> Schedule => schedule;

    protected override float SpeedBonusDurationSeconds => schedule.Count == 0
        ? DurationSeconds
        : Math.Min(DurationSeconds, schedule.Max(ExitSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        PlateStackerScheduleEntry[] eligiblePlates = schedule
            .Where(entry => entry.LandingSeconds >= elapsedSeconds)
            .ToArray();
        playerStates.Add(
            session.ClientId,
            new PlayerState(eligiblePlates, elapsedSeconds));
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        float elapsedSeconds = GetElapsedSeconds(nowUtc);
        float inputGraceSeconds = Math.Max(0f, settings.InputGraceSeconds);
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, nowUtc);
            if (session.SubmittedThisRound)
                continue;

            PlayerState state = playerStates[session.ClientId];
            ResolveThrough(session, state, Math.Max(0f, elapsedSeconds - inputGraceSeconds), server);

            TryCompletePlayer(session, state, elapsedSeconds, server);
        }
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        sbyte direction = message.GetSByte();
        float startsAtSeconds = message.GetFloat();
        if (direction < -1 ||
            direction > 1 ||
            !float.IsFinite(startsAtSeconds))
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        if (session.SubmittedThisRound || state.Collapsed)
            return;

        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        if (startsAtSeconds < 0f ||
            startsAtSeconds > DurationSeconds ||
            startsAtSeconds >
                receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) ||
            state.DirectionChanges.Count >=
                Math.Max(2, settings.MaximumInputSamples))
        {
            return;
        }

        float effectiveStartsAtSeconds = startsAtSeconds;
        if (startsAtSeconds < state.SimulatedAtSeconds ||
            receivedAtSeconds - startsAtSeconds >
                Math.Max(0f, settings.InputGraceSeconds))
        {
            // A delayed state sample cannot rewrite an evaluated landing, but
            // applying it at receipt repairs a dropped or excessively late
            // press/release for subsequent plates.
            effectiveStartsAtSeconds = Math.Max(
                state.SimulatedAtSeconds,
                receivedAtSeconds);
        }

        if (state.DirectionChanges.Count > 0)
        {
            DirectionChange previous = state.DirectionChanges[^1];
            if (effectiveStartsAtSeconds < previous.StartsAtSeconds)
                return;
            if (direction == previous.Direction)
                return;
        }
        else if (direction == 0)
        {
            return;
        }

        state.DirectionChanges.Add(
            new DirectionChange(direction, effectiveStartsAtSeconds));
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            if (session.SubmittedThisRound)
                continue;

            PlayerState state = playerStates[session.ClientId];
            ResolveThrough(session, state, DurationSeconds, server);

            TryCompletePlayer(session, state, DurationSeconds, server);
            if (!session.SubmittedThisRound)
                CompleteSubmission(session, GetScore(state), server,
                    completedAtSeconds: DurationSeconds);
        }
    }

    public override string Describe()
    {
        return $"seed={scheduleSeed} plates={schedule.Count}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddUInt(scheduleSeed);
        message.AddUShort((ushort)schedule.Count);
        message.AddFloat(firstLandingSeconds);
        message.AddFloat(lastLandingSeconds);
        message.AddFloat(minimumFallDurationSeconds);
        message.AddFloat(maximumFallDurationSeconds);
        message.AddFloat(Math.Max(
            0.01f,
            settings.StackSpeedNormalizedPerSecond));
        message.AddFloat(Math.Clamp(
            settings.PlateWidthNormalized,
            0.01f,
            0.49f));
        message.AddFloat(Math.Clamp(
            settings.StartingPlateWidthNormalized,
            0.01f,
            0.49f));
        message.AddFloat(Math.Max(
            0.01f,
            settings.CollapseOffsetPlateWidths));
        message.AddInt(Math.Max(0, settings.PointsPerPlate));
        message.AddUShort((ushort)schedule.Count);
        foreach (PlateStackerScheduleEntry entry in schedule)
        {
            message.AddUShort(entry.PlateId);
            message.AddFloat(entry.XNormalized);
            message.AddFloat(entry.SpawnSeconds);
            message.AddFloat(entry.LandingSeconds);
            message.AddFloat(entry.FallDurationSeconds);
        }
    }

    private void ResolveThrough(
        PlayerSession session, PlayerState state, float throughSeconds, Riptide.Server server)
    {
        while (!state.Collapsed)
        {
            PlateStackerScheduleEntry? next = null;
            float nextTime = float.PositiveInfinity;
            bool contact = false;
            foreach (PlateStackerScheduleEntry entry in state.EligiblePlates)
            {
                if (state.EvaluatedPlateIds.Contains(entry.PlateId))
                    continue;
                float contactTime = ContactSeconds(entry, state.SuccessfulPlateCount);
                // A plate already below a newly raised surface cannot teleport up to it.
                bool canContact = contactTime >= state.SimulatedAtSeconds;
                float eventTime = canContact ? contactTime : ExitSeconds(entry);
                if (eventTime < nextTime)
                {
                    next = entry;
                    nextTime = eventTime;
                    contact = canContact;
                }
            }
            if (!next.HasValue || nextTime > throughSeconds)
                break;
            EvaluatePlate(session, state, next.Value, nextTime, contact, server);
        }
    }

    private void EvaluatePlate(
        PlayerSession session,
        PlayerState state,
        PlateStackerScheduleEntry entry,
        float contactSeconds,
        bool canContact,
        Riptide.Server server)
    {
        AdvanceTo(state, contactSeconds);
        float topXNormalized =
            state.BaseXNormalized + state.PlateOffsets[^1];
        float plateWidthNormalized = Math.Clamp(
            settings.PlateWidthNormalized,
            0.01f,
            0.49f);
        float topWidthNormalized = state.PlateOffsets.Count == 1
            ? Math.Clamp(
                settings.StartingPlateWidthNormalized,
                0.01f,
                0.49f)
            : plateWidthNormalized;
        bool landed = canContact && HasVisibleOverlap(
            topXNormalized,
            topWidthNormalized,
            entry.XNormalized,
            plateWidthNormalized);
        float retainedOffsetNormalized = 0f;

        if (landed)
        {
            retainedOffsetNormalized =
                entry.XNormalized - state.BaseXNormalized;
            state.PlateOffsets.Add(retainedOffsetNormalized);
            state.SuccessfulPlateCount++;
            state.Collapsed = ExceedsCollapseThreshold(
                retainedOffsetNormalized,
                plateWidthNormalized,
                Math.Max(0.01f, settings.CollapseOffsetPlateWidths));
        }

        // Keep the simulation cursor at this landing. Advancing it to the
        // current server tick would make a delayed batch evaluate subsequent
        // plates at the wrong, later position.
        state.LastLandingSeconds = Math.Max(state.LastLandingSeconds,
            landed ? contactSeconds : ExitSeconds(entry));
        state.EvaluatedPlateIds.Add(entry.PlateId);
        int score = state.Collapsed ? 0 : GetScore(state);
        SendOutcome(
            session,
            entry.PlateId,
            landed,
            state.Collapsed,
            retainedOffsetNormalized,
            state.SuccessfulPlateCount,
            score,
            state.BaseXNormalized,
            state.SimulatedAtSeconds,
            server);

        if (state.Collapsed)
        {
            CompleteSubmission(
                session,
                0,
                server,
                completedAtSeconds: contactSeconds);
        }
    }

    private void TryCompletePlayer(
        PlayerSession session,
        PlayerState state,
        float elapsedSeconds,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound ||
            state.Collapsed ||
            state.EvaluatedPlateIds.Count < state.EligiblePlates.Count ||
            elapsedSeconds < state.LastLandingSeconds)
        {
            return;
        }

        float completedAtSeconds = state.EligiblePlates.Count == 0
            ? DurationSeconds
            : state.LastLandingSeconds;
        CompleteSubmission(
            session,
            GetScore(state),
            server,
            completedAtSeconds: completedAtSeconds);
    }

    private void AdvanceTo(PlayerState state, float targetSeconds)
    {
        float clampedTarget = Math.Clamp(
            targetSeconds,
            state.SimulatedAtSeconds,
            DurationSeconds);
        while (state.NextDirectionChangeIndex <
            state.DirectionChanges.Count)
        {
            DirectionChange change =
                state.DirectionChanges[state.NextDirectionChangeIndex];
            if (change.StartsAtSeconds > clampedTarget)
                break;

            AdvanceSegment(state, change.StartsAtSeconds);
            state.Direction = change.Direction;
            state.NextDirectionChangeIndex++;
        }

        AdvanceSegment(state, clampedTarget);
    }

    private void AdvanceSegment(PlayerState state, float endsAtSeconds)
    {
        float deltaSeconds = Math.Max(
            0f,
            endsAtSeconds - state.SimulatedAtSeconds);
        float speed = Math.Max(
            0.01f,
            settings.StackSpeedNormalizedPerSecond);
        (float minimumX, float maximumX) = GetMovementBounds(state);
        state.BaseXNormalized = Math.Clamp(
            state.BaseXNormalized +
                (state.Direction * speed * deltaSeconds),
            minimumX,
            maximumX);
        state.SimulatedAtSeconds = endsAtSeconds;
    }

    private (float MinimumX, float MaximumX) GetMovementBounds(
        PlayerState state)
    {
        float startingHalfWidth = Math.Clamp(
            settings.StartingPlateWidthNormalized * 0.5f,
            0.005f,
            0.245f);
        float plateHalfWidth = Math.Clamp(
            settings.PlateWidthNormalized * 0.5f,
            0.005f,
            0.245f);
        float minimumEdge = -startingHalfWidth;
        float maximumEdge = startingHalfWidth;
        for (int index = 1; index < state.PlateOffsets.Count; index++)
        {
            float offset = state.PlateOffsets[index];
            minimumEdge = Math.Min(
                minimumEdge,
                offset - plateHalfWidth);
            maximumEdge = Math.Max(
                maximumEdge,
                offset + plateHalfWidth);
        }

        return (
            -minimumEdge,
            1f - maximumEdge);
    }

    private int GetScore(PlayerState state)
    {
        return checked(
            state.SuccessfulPlateCount *
            Math.Max(0, settings.PointsPerPlate));
    }

    private void SendOutcome(
        PlayerSession session,
        ushort plateId,
        bool landed,
        bool collapsed,
        float retainedOffsetNormalized,
        int successfulPlateCount,
        int score,
        float baseXNormalized,
        float snapshotAtSeconds,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(PlateOutcomeEvent);
        outcome.AddUShort(plateId);
        outcome.AddBool(landed);
        outcome.AddBool(collapsed);
        outcome.AddFloat(retainedOffsetNormalized);
        outcome.AddUShort((ushort)successfulPlateCount);
        outcome.AddInt(score);
        outcome.AddFloat(baseXNormalized);
        outcome.AddFloat(snapshotAtSeconds);
        server.Send(outcome, session.ClientId);
    }

    public static bool HasVisibleOverlap(
        float firstCenter,
        float firstWidth,
        float secondCenter,
        float secondWidth)
    {
        return Math.Abs(firstCenter - secondCenter) <
            ((firstWidth + secondWidth) * 0.5f);
    }

    public static bool ExceedsCollapseThreshold(
        float topOffset,
        float plateWidth,
        float collapseOffsetPlateWidths)
    {
        return Math.Abs(topOffset) >
            (plateWidth * collapseOffsetPlateWidths);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }

    private static int GetMaximumScore(PlateStackerSettings settings)
    {
        long maximumScore =
            (long)Math.Clamp(
                settings.PlateCount,
                1,
                PlateStackerSettings.MaximumSupportedPlateCount) *
            Math.Max(0, settings.PointsPerPlate);
        return (int)Math.Min(maximumScore, int.MaxValue);
    }
}
