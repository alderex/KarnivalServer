using Riptide;

public sealed class SpotTheDifferenceRound : MiniGameRoundBase
{
    private const byte SelectionOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public HashSet<byte> SelectedDifferenceIds { get; } = new();
    }

    private readonly Dictionary<ushort, PlayerState> playerStates = new();
    private readonly byte[] selectedDifferenceIds;
    private readonly HashSet<byte> selectedDifferenceIdSet;
    private readonly int pointsPerDifference;
    private readonly int fullCompletionScore;
    private readonly SpotTheDifferenceSettings settings;

    public SpotTheDifferenceRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        byte[] selectedDifferenceIds,
        int pointsPerDifference,
        int fullCompletionScore,
        SpotTheDifferenceSettings settings)
        : base(
            roundId,
            MiniGameType.SpotTheDifference,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            Math.Max(
                checked(selectedDifferenceIds.Length * pointsPerDifference),
                fullCompletionScore))
    {
        this.selectedDifferenceIds = selectedDifferenceIds.ToArray();
        selectedDifferenceIdSet = new HashSet<byte>(selectedDifferenceIds);
        this.pointsPerDifference = pointsPerDifference;
        this.fullCompletionScore = fullCompletionScore;
        this.settings = settings;
    }

    public IReadOnlyList<byte> SelectedDifferenceIds => selectedDifferenceIds;
    public int SelectedDifferenceCount => selectedDifferenceIds.Length;
    public int PointsPerDifference => pointsPerDifference;
    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (!playerStates.ContainsKey(session.ClientId))
            playerStates.Add(session.ClientId, new PlayerState());
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        byte differenceId = message.GetByte();
        float clickedAtSeconds = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        bool accepted = selectedDifferenceIdSet.Contains(differenceId) &&
            !state.SelectedDifferenceIds.Contains(differenceId) &&
            IsValidTimestamp(clickedAtSeconds, receivedAtSeconds);

        if (accepted)
            state.SelectedDifferenceIds.Add(differenceId);

        SendSelectionOutcome(
            session,
            differenceId,
            accepted,
            state.SelectedDifferenceIds.Count,
            server);

        if (accepted && state.SelectedDifferenceIds.Count == selectedDifferenceIds.Length)
        {
            CompleteSubmission(
                session,
                fullCompletionScore,
                server,
                completedAtSeconds: clickedAtSeconds);
        }
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            PlayerState state = playerStates[session.ClientId];
            CompleteSubmission(
                session,
                state.SelectedDifferenceIds.Count * pointsPerDifference,
                server);
        }
    }

    public override string Describe() =>
        $"differences=[{string.Join(',', selectedDifferenceIds)}] " +
        $"points={pointsPerDifference} completion={fullCompletionScore}";

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)selectedDifferenceIds.Length);
        foreach (byte differenceId in selectedDifferenceIds)
            message.AddByte(differenceId);
        message.AddInt(pointsPerDifference);
    }

    private bool IsValidTimestamp(float clickedAtSeconds, float receivedAtSeconds)
    {
        return float.IsFinite(clickedAtSeconds) &&
            clickedAtSeconds >= 0f &&
            clickedAtSeconds <= DurationSeconds &&
            clickedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - clickedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private void SendSelectionOutcome(
        PlayerSession session,
        byte differenceId,
        bool accepted,
        int foundCount,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(SelectionOutcomeEvent);
        outcome.AddByte(differenceId);
        outcome.AddBool(accepted);
        outcome.AddByte((byte)Math.Clamp(foundCount, 0, byte.MaxValue));
        server.Send(outcome, session.ClientId);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
