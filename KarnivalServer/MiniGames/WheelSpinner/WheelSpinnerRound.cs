using Riptide;

public sealed class WheelSpinnerRound : MiniGameRoundBase
{
    private const byte StopOutcomeEvent = 1;
    private readonly IReadOnlyList<WheelSpinnerSlice> slices;
    private readonly WheelSpinnerSettings settings;

    public WheelSpinnerRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        float initialRotationDegrees,
        float angularSpeedDegreesPerSecond,
        IReadOnlyList<WheelSpinnerSlice> slices,
        WheelSpinnerSettings settings)
        : base(
            roundId,
            MiniGameType.WheelSpinner,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            slices.Count == 0 ? 0 : slices.Max(slice => slice.Points))
    {
        InitialRotationDegrees = NormalizeDegrees(initialRotationDegrees);
        AngularSpeedDegreesPerSecond = Math.Max(
            0.01f,
            angularSpeedDegreesPerSecond);
        this.slices = slices;
        this.settings = settings;
    }

    public float InitialRotationDegrees { get; }
    public float AngularSpeedDegreesPerSecond { get; }
    public IReadOnlyList<WheelSpinnerSlice> Slices => slices;

    public override DateTime SubmissionDeadlineUtc => EndsUtc.AddSeconds(
        Math.Max(0f, settings.InputGraceSeconds));

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        float stoppedAtSeconds = message.GetFloat();
        if (session.SubmittedThisRound)
        {
            SendOutcome(session, false, stoppedAtSeconds,
                InitialRotationDegrees, 0, 0, server);
            return;
        }

        float receivedAtSeconds = GetElapsedSeconds(DateTime.UtcNow);
        if (!IsValidTimestamp(stoppedAtSeconds, receivedAtSeconds))
        {
            SendOutcome(session, false, stoppedAtSeconds,
                InitialRotationDegrees, 0, 0, server);
            return;
        }

        CompleteAt(session, stoppedAtSeconds, server);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            if (!session.SubmittedThisRound)
                CompleteAt(session, DurationSeconds, server);
        }
    }

    public override string Describe()
    {
        return $"initial={InitialRotationDegrees:0.00} " +
            $"speed={AngularSpeedDegreesPerSecond:0.00} slices={slices.Count}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddFloat(InitialRotationDegrees);
        message.AddFloat(AngularSpeedDegreesPerSecond);
        message.AddByte((byte)slices.Count);
        foreach (WheelSpinnerSlice slice in slices)
        {
            message.AddInt(slice.Points);
            message.AddFloat(slice.ArcDegrees);
        }
    }

    private void CompleteAt(
        PlayerSession session,
        float stoppedAtSeconds,
        Riptide.Server server)
    {
        float rotationDegrees = CalculateRotationDegrees(
            InitialRotationDegrees,
            AngularSpeedDegreesPerSecond,
            stoppedAtSeconds);
        int sliceIndex = GetSliceIndex(rotationDegrees, slices);
        int points = slices[sliceIndex].Points;
        SendOutcome(session, true, stoppedAtSeconds, rotationDegrees,
            sliceIndex, points, server);

        // Wheel values are literal awards. Force the shared speed bonus to 1x.
        CompleteSubmission(session, points, server,
            completedAtSeconds: DurationSeconds);
    }

    private bool IsValidTimestamp(
        float stoppedAtSeconds,
        float receivedAtSeconds)
    {
        return float.IsFinite(stoppedAtSeconds) &&
            stoppedAtSeconds >= 0f &&
            stoppedAtSeconds <= DurationSeconds &&
            stoppedAtSeconds <= receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - stoppedAtSeconds <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private void SendOutcome(
        PlayerSession session,
        bool accepted,
        float stoppedAtSeconds,
        float rotationDegrees,
        int sliceIndex,
        int points,
        Riptide.Server server)
    {
        if (session.IsSimulated)
            return;

        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(StopOutcomeEvent);
        outcome.AddBool(accepted);
        outcome.AddFloat(stoppedAtSeconds);
        outcome.AddFloat(rotationDegrees);
        outcome.AddByte((byte)sliceIndex);
        outcome.AddInt(points);
        server.Send(outcome, session.ClientId);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }

    public static float CalculateRotationDegrees(
        float initialRotationDegrees,
        float angularSpeedDegreesPerSecond,
        float elapsedSeconds)
    {
        return NormalizeDegrees(
            initialRotationDegrees +
            (angularSpeedDegreesPerSecond * Math.Max(0f, elapsedSeconds)));
    }

    public static int GetSliceIndex(
        float rotationDegrees,
        IReadOnlyList<WheelSpinnerSlice> configuredSlices)
    {
        if (configuredSlices.Count == 0)
            throw new ArgumentException("At least one wheel slice is required.");

        float pointerAngleDegrees = NormalizeDegrees(-rotationDegrees);
        float cumulativeDegrees = 0f;
        for (int index = 0; index < configuredSlices.Count; index++)
        {
            cumulativeDegrees += configuredSlices[index].ArcDegrees;
            if (pointerAngleDegrees < cumulativeDegrees ||
                index == configuredSlices.Count - 1)
            {
                return index;
            }
        }

        return configuredSlices.Count - 1;
    }

    public static float NormalizeDegrees(float degrees)
    {
        float normalized = degrees % 360f;
        return normalized < 0f ? normalized + 360f : normalized;
    }
}
