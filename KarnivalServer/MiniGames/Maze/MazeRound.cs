using Riptide;

public sealed class MazeRound : MiniGameRoundBase
{
    private const byte PositionSnapshotEvent = 1;

    private sealed class PlayerState
    {
        public PlayerState(MazePoint position)
        {
            Position = position;
        }

        public MazePoint Position { get; set; }
        public float LastSampleSeconds { get; set; }
        public int SampleCount { get; set; }
        public bool Completed { get; set; }
    }

    private readonly MazeLayout layout;
    private readonly MazeSettings settings;
    private readonly MazeMovementSimulator movementSimulator;
    private readonly MazePoint startPosition;
    private readonly MazePoint goalPosition;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public MazeRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        MazeLayout layout,
        MazeSettings settings)
        : base(
            roundId,
            MiniGameType.Maze,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            Math.Max(0, settings.CorrectScore))
    {
        this.layout = layout;
        this.settings = settings;
        startPosition = MazeGenerator.GetOutsideStartPosition(
            layout,
            settings.PlayerRadiusNormalized,
            settings.WallThicknessNormalized);
        goalPosition = MazeGenerator.GetCellCenter(
            layout.GoalCell,
            layout.GridSize);
        movementSimulator = new MazeMovementSimulator(
            layout,
            settings.PlayerRadiusNormalized,
            settings.WallThicknessNormalized);
    }

    public MazeLayout Layout => layout;
    public MazePoint StartPosition => startPosition;
    public MazePoint GoalPosition => goalPosition;

    public override DateTime SubmissionDeadlineUtc =>
        EndsUtc.AddSeconds(Math.Max(0f, settings.InputGraceSeconds));

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (!playerStates.ContainsKey(session.ClientId))
            playerStates.Add(
                session.ClientId,
                new PlayerState(startPosition));
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        int sampleCount = message.GetByte();
        if (sampleCount < 1 ||
            sampleCount > Math.Clamp(
                settings.MaximumBatchSamples,
                1,
                byte.MaxValue))
        {
            return;
        }

        MazeInputSample[] samples = new MazeInputSample[sampleCount];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = new MazeInputSample(
                new MazePoint(message.GetFloat(), message.GetFloat()),
                message.GetFloat());
        }

        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        if (session.SubmittedThisRound || state.Completed)
            return;

        float receivedAtSeconds = GetElapsedSeconds(nowUtc);
        float previousTimestamp = state.LastSampleSeconds;
        foreach (MazeInputSample sample in samples)
        {
            if (!IsValidSample(
                sample,
                previousTimestamp,
                receivedAtSeconds))
            {
                return;
            }
            previousTimestamp = sample.SampledAtSeconds;
        }

        if (state.SampleCount + samples.Length >
            Math.Max(1, settings.MaximumInputSamples))
        {
            return;
        }

        foreach (MazeInputSample sample in samples)
        {
            float segmentStartsAt = state.LastSampleSeconds;
            MazeMovementSimulator.Result movement = movementSimulator.Move(
                state.Position,
                sample.DesiredPosition);
            state.Position = movement.Position;
            state.LastSampleSeconds = sample.SampledAtSeconds;
            state.SampleCount++;

            if (!TryGetGoalFraction(
                    movement.Segments,
                    out float goalFraction))
            {
                continue;
            }

            float completedAtSeconds = segmentStartsAt +
                ((sample.SampledAtSeconds - segmentStartsAt) *
                goalFraction);
            state.Completed = true;
            SendSnapshot(session, state, true, server);
            CompleteSubmission(
                session,
                Math.Max(0, settings.CorrectScore),
                server,
                completedAtSeconds: completedAtSeconds);
            return;
        }

        SendSnapshot(session, state, false, server);
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            if (!session.SubmittedThisRound)
            {
                CompleteSubmission(
                    session,
                    0,
                    server,
                    completedAtSeconds: DurationSeconds);
            }
        }
    }

    public override string Describe()
    {
        return $"seed={layout.Seed} grid={layout.GridSize}x{layout.GridSize} " +
            $"start={layout.StartCell}:{layout.EntranceSide} " +
            $"goal={layout.GoalCell}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddUInt(layout.Seed);
        message.AddByte((byte)layout.GridSize);
        message.AddByte((byte)layout.StartCell);
        message.AddByte((byte)layout.GoalCell);
        message.AddByte((byte)layout.EntranceSide);
        message.AddFloat(startPosition.X);
        message.AddFloat(startPosition.Y);
        message.AddFloat(goalPosition.X);
        message.AddFloat(goalPosition.Y);
        message.AddFloat(Math.Clamp(
            settings.PlayerRadiusNormalized,
            0.001f,
            0.1f));
        message.AddFloat(Math.Clamp(
            settings.WallThicknessNormalized,
            0.0001f,
            0.05f));
        message.AddFloat(Math.Clamp(
            settings.ExitRadiusNormalized,
            0.001f,
            0.15f));
        message.AddFloat(Math.Clamp(
            settings.OuterPaddingNormalized,
            0.01f,
            0.25f));
        message.AddByte((byte)Math.Clamp(
            settings.MaximumBatchSamples,
            1,
            byte.MaxValue));
        message.AddUShort((ushort)Math.Clamp(
            settings.MaximumInputSamples,
            1,
            ushort.MaxValue));
        message.AddUShort((ushort)layout.Walls.Length);
        foreach (byte wallMask in layout.Walls)
            message.AddByte(wallMask);
    }

    private bool IsValidSample(
        MazeInputSample sample,
        float previousTimestamp,
        float receivedAtSeconds)
    {
        float x = sample.DesiredPosition.X;
        float y = sample.DesiredPosition.Y;
        float timestamp = sample.SampledAtSeconds;
        float padding = Math.Clamp(
            settings.OuterPaddingNormalized,
            0.01f,
            0.25f);
        return float.IsFinite(x) &&
            float.IsFinite(y) &&
            float.IsFinite(timestamp) &&
            x >= -padding &&
            x <= 1f + padding &&
            y >= -padding &&
            y <= 1f + padding &&
            timestamp > previousTimestamp &&
            timestamp <= DurationSeconds &&
            timestamp <=
                receivedAtSeconds +
                Math.Max(0f, settings.FutureInputToleranceSeconds) &&
            receivedAtSeconds - timestamp <=
                Math.Max(0f, settings.InputGraceSeconds);
    }

    private bool TryGetGoalFraction(
        IReadOnlyList<MazeMovementSimulator.Segment> segments,
        out float fraction)
    {
        float completionRadius =
            Math.Max(0f, settings.PlayerRadiusNormalized) +
            Math.Max(0f, settings.ExitRadiusNormalized);
        float totalLength = 0f;
        foreach (MazeMovementSimulator.Segment segment in segments)
            totalLength += MathF.Sqrt((segment.End - segment.Start).LengthSquared);
        if (totalLength <= 0.0000001f)
        {
            fraction = 0f;
            MazePoint position = segments.Count > 0
                ? segments[^1].End
                : startPosition;
            return (position - goalPosition).LengthSquared <=
                completionRadius * completionRadius;
        }

        float traversedLength = 0f;
        foreach (MazeMovementSimulator.Segment segment in segments)
        {
            float segmentLength = MathF.Sqrt(
                (segment.End - segment.Start).LengthSquared);
            if (MazeMovementSimulator.TryGetCircleEntryFraction(
                segment.Start,
                segment.End,
                goalPosition,
                completionRadius,
                out float segmentFraction))
            {
                fraction = Math.Clamp(
                    (traversedLength +
                    (segmentLength * segmentFraction)) /
                    totalLength,
                    0f,
                    1f);
                return true;
            }
            traversedLength += segmentLength;
        }

        fraction = 0f;
        return false;
    }

    private void SendSnapshot(
        PlayerSession session,
        PlayerState state,
        bool completed,
        Riptide.Server server)
    {
        Message snapshot = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        snapshot.AddUInt(RoundId);
        snapshot.AddUShort((ushort)GameType);
        snapshot.AddByte(PositionSnapshotEvent);
        snapshot.AddFloat(state.Position.X);
        snapshot.AddFloat(state.Position.Y);
        snapshot.AddFloat(state.LastSampleSeconds);
        snapshot.AddBool(completed);
        server.Send(snapshot, session.ClientId);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }
}
