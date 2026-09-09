using Riptide;

public sealed class CarParkRound : MiniGameRoundBase
{
    private const float ParkingCenterToleranceFraction = 0.5f;

    private const byte TerminalOutcomeEvent = 1;

    private sealed class PlayerState
    {
        public PlayerState(bool eligible, float startsAt, CarParkPose pose)
        {
            Eligible = eligible;
            SimulatedAtSeconds = startsAt;
            Pose = pose;
            LastSampleAtSeconds = startsAt;
        }

        public bool Eligible { get; }
        public List<CarParkSteeringSample> Samples { get; } = new();
        public CarParkPose Pose { get; set; }
        public float SimulatedAtSeconds { get; set; }
        public float LastSampleAtSeconds { get; set; }
        public float Steering { get; set; }
        public int NextSampleIndex { get; set; }
        public bool Resolved { get; set; }
    }

    private readonly int spotCount;
    private readonly byte emptySpot;
    private readonly float leadInSeconds;
    private readonly float drivingDurationSeconds;
    private readonly float outcomeBufferSeconds;
    private readonly CarParkSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public CarParkRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        int spotCount,
        byte emptySpot,
        float leadInSeconds,
        float drivingDurationSeconds,
        float outcomeBufferSeconds,
        CarParkSettings settings)
        : base(
            roundId,
            MiniGameType.CarPark,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            Math.Max(0, settings.CorrectScore))
    {
        this.spotCount = spotCount;
        this.emptySpot = emptySpot;
        this.leadInSeconds = leadInSeconds;
        this.drivingDurationSeconds = drivingDurationSeconds;
        this.outcomeBufferSeconds = outcomeBufferSeconds;
        this.settings = settings;
    }

    public float DrivingEndsAtSeconds => leadInSeconds + drivingDurationSeconds;

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        float elapsed = GetElapsedSeconds(nowUtc);
        playerStates.Add(
            session.ClientId,
            new PlayerState(
                elapsed < leadInSeconds,
                leadInSeconds,
                new CarParkPose(settings.StartX, settings.StartY, 0f)));
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        float elapsed = GetElapsedSeconds(nowUtc);
        float simulationTarget = Math.Clamp(
            elapsed - Math.Max(0f, settings.InputGraceSeconds),
            leadInSeconds,
            DrivingEndsAtSeconds);
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, nowUtc);
            PlayerState state = playerStates[session.ClientId];
            if (!state.Eligible || state.Resolved)
                continue;

            CarParkOutcomeReason? outcome = SimulateTo(state, simulationTarget);
            if (outcome.HasValue)
                ResolvePlayer(session, state, outcome.Value, server);
            else if (simulationTarget >= DrivingEndsAtSeconds)
                ResolvePlayer(session, state, CarParkOutcomeReason.Timeout, server);
        }
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        float steering = message.GetFloat();
        float sampledAt = message.GetFloat();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAt = GetElapsedSeconds(nowUtc);
        if (!state.Eligible ||
            state.Resolved ||
            float.IsNaN(steering) ||
            float.IsNaN(sampledAt) ||
            steering < -1f ||
            steering > 1f ||
            sampledAt < leadInSeconds ||
            sampledAt > DrivingEndsAtSeconds ||
            sampledAt < state.LastSampleAtSeconds ||
            sampledAt < state.SimulatedAtSeconds ||
            sampledAt > receivedAt + Math.Max(0f, settings.FutureInputToleranceSeconds) ||
            receivedAt - sampledAt > Math.Max(0f, settings.InputGraceSeconds))
        {
            return;
        }

        if (state.Samples.Count > 0 &&
            Math.Abs(state.Samples[^1].SampledAtSeconds - sampledAt) <= 0.00001f)
        {
            if (state.NextSampleIndex < state.Samples.Count)
                state.Samples[^1] = new CarParkSteeringSample(steering, sampledAt);
            return;
        }

        float latestSteering = state.NextSampleIndex < state.Samples.Count
            ? state.Samples[^1].Steering
            : state.Steering;
        if (Math.Abs(latestSteering - steering) <= 0.00001f ||
            state.Samples.Count >= Math.Max(1, settings.MaximumInputSamples))
        {
            return;
        }

        state.Samples.Add(new CarParkSteeringSample(steering, sampledAt));
        state.LastSampleAtSeconds = sampledAt;
    }

    public override void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, EndsUtc);
            PlayerState state = playerStates[session.ClientId];
            if (!state.Eligible)
            {
                session.MarkMissedRound();
                continue;
            }

            if (!state.Resolved)
            {
                CarParkOutcomeReason outcome =
                    SimulateTo(state, DrivingEndsAtSeconds) ??
                    CarParkOutcomeReason.Timeout;
                ResolvePlayer(session, state, outcome, server);
            }
        }
    }

    public override string Describe()
    {
        return $"spots={spotCount} empty={emptySpot}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddByte((byte)spotCount);
        message.AddByte(emptySpot);
        message.AddFloat(leadInSeconds);
        message.AddFloat(drivingDurationSeconds);
        message.AddFloat(outcomeBufferSeconds);
        message.AddFloat(Math.Clamp(settings.FixedStepSeconds, 0.005f, 0.05f));
        message.AddFloat(Math.Max(0.01f, settings.ForwardSpeed));
        message.AddFloat(Math.Max(1f, settings.MaximumTurnDegreesPerSecond));
        message.AddFloat(settings.StartX);
        message.AddFloat(settings.StartY);
        message.AddFloat(settings.ParkingRowY);
        message.AddFloat(settings.FirstParkingX);
        message.AddFloat(settings.LastParkingX);
        message.AddFloat(Math.Max(0.01f, settings.ParkingHalfWidth));
        message.AddFloat(Math.Max(0.01f, settings.ParkingHalfHeight));
        message.AddFloat(Math.Max(0.005f, settings.CarHalfWidth));
        message.AddFloat(Math.Max(0.005f, settings.CarHalfHeight));
        message.AddFloat(Math.Max(0.1f, settings.BoundsHalfWidth));
        message.AddFloat(Math.Max(0.1f, settings.BoundsHalfHeight));
        message.AddFloat(Math.Clamp(settings.ParkingAngleToleranceDegrees, 0f, 90f));
        message.AddFloat(Math.Max(0.02f, settings.SteeringSendIntervalSeconds));
        message.AddFloat(Math.Clamp(settings.SteeringChangeThreshold, 0f, 1f));
        message.AddFloat(Math.Max(1f, settings.WheelMaximumRotationDegrees));
        message.AddFloat(Math.Max(1f, settings.WheelReturnDegreesPerSecond));
    }

    private CarParkOutcomeReason? SimulateTo(PlayerState state, float targetSeconds)
    {
        float fixedStep = Math.Clamp(settings.FixedStepSeconds, 0.005f, 0.05f);
        while (state.SimulatedAtSeconds + 0.00001f < targetSeconds)
        {
            while (state.NextSampleIndex < state.Samples.Count &&
                state.Samples[state.NextSampleIndex].SampledAtSeconds <=
                    state.SimulatedAtSeconds + 0.00001f)
            {
                state.Steering = state.Samples[state.NextSampleIndex].Steering;
                state.NextSampleIndex++;
            }

            float stepEnd = Math.Min(
                state.SimulatedAtSeconds + fixedStep,
                targetSeconds);
            if (state.NextSampleIndex < state.Samples.Count)
            {
                float nextSampleAt = state.Samples[state.NextSampleIndex].SampledAtSeconds;
                if (nextSampleAt > state.SimulatedAtSeconds + 0.00001f)
                    stepEnd = Math.Min(stepEnd, nextSampleAt);
            }

            float delta = stepEnd - state.SimulatedAtSeconds;
            state.Pose = Integrate(state.Pose, state.Steering, delta);
            state.SimulatedAtSeconds = stepEnd;
            CarParkOutcomeReason? outcome = EvaluatePose(state.Pose);
            if (outcome.HasValue)
                return outcome;
        }

        return null;
    }

    private CarParkPose Integrate(CarParkPose pose, float steering, float delta)
    {
        float heading = pose.HeadingDegrees +
            (steering * Math.Max(1f, settings.MaximumTurnDegreesPerSecond) * delta);
        float radians = heading * (MathF.PI / 180f);
        float distance = Math.Max(0.01f, settings.ForwardSpeed) * delta;
        return new CarParkPose(
            pose.X + (MathF.Sin(radians) * distance),
            pose.Y + (MathF.Cos(radians) * distance),
            NormalizeAngle(heading));
    }

    private CarParkOutcomeReason? EvaluatePose(CarParkPose pose)
    {
        if (IsParked(pose))
            return CarParkOutcomeReason.Parked;
        for (int slot = 0; slot < spotCount; slot++)
        {
            if (slot != emptySpot && IntersectsParkedCar(pose, GetSpotX(slot)))
                return CarParkOutcomeReason.Collision;
        }

        GetWorldExtents(pose.HeadingDegrees, out float extentX, out float extentY);
        if (MathF.Abs(pose.X) + extentX > Math.Max(0.1f, settings.BoundsHalfWidth) ||
            MathF.Abs(pose.Y) + extentY > Math.Max(0.1f, settings.BoundsHalfHeight))
        {
            return CarParkOutcomeReason.Offscreen;
        }

        return null;
    }

    private bool IsParked(CarParkPose pose)
    {
        float targetY = settings.ParkingRowY;
        return MathF.Abs(NormalizeAngle(pose.HeadingDegrees)) <=
                Math.Clamp(settings.ParkingAngleToleranceDegrees, 0f, 90f) &&
            MathF.Abs(pose.X - GetSpotX(emptySpot)) <=
                Math.Max(0.01f, settings.ParkingHalfWidth) *
                    ParkingCenterToleranceFraction &&
            pose.Y >= targetY &&
            pose.Y - targetY <=
                Math.Max(0.01f, settings.ParkingHalfHeight) *
                    ParkingCenterToleranceFraction;
    }

    private bool IntersectsParkedCar(CarParkPose pose, float parkedX)
    {
        float angle = pose.HeadingDegrees * (MathF.PI / 180f);
        float sin = MathF.Sin(angle);
        float cos = MathF.Cos(angle);
        float deltaX = parkedX - pose.X;
        float deltaY = settings.ParkingRowY - pose.Y;
        float halfWidth = Math.Max(0.005f, settings.CarHalfWidth);
        float halfHeight = Math.Max(0.005f, settings.CarHalfHeight);

        float onPlayerX = MathF.Abs((deltaX * cos) - (deltaY * sin));
        float onPlayerY = MathF.Abs((deltaX * sin) + (deltaY * cos));
        if (onPlayerX > halfWidth + (halfWidth * MathF.Abs(cos)) +
                (halfHeight * MathF.Abs(sin)) ||
            onPlayerY > halfHeight + (halfWidth * MathF.Abs(sin)) +
                (halfHeight * MathF.Abs(cos)))
        {
            return false;
        }

        float playerExtentX = (halfWidth * MathF.Abs(cos)) +
            (halfHeight * MathF.Abs(sin));
        float playerExtentY = (halfWidth * MathF.Abs(sin)) +
            (halfHeight * MathF.Abs(cos));
        return MathF.Abs(deltaX) <= halfWidth + playerExtentX &&
            MathF.Abs(deltaY) <= halfHeight + playerExtentY;
    }

    private void GetWorldExtents(float headingDegrees, out float x, out float y)
    {
        float radians = headingDegrees * (MathF.PI / 180f);
        float sin = MathF.Abs(MathF.Sin(radians));
        float cos = MathF.Abs(MathF.Cos(radians));
        float halfWidth = Math.Max(0.005f, settings.CarHalfWidth);
        float halfHeight = Math.Max(0.005f, settings.CarHalfHeight);
        x = (halfWidth * cos) + (halfHeight * sin);
        y = (halfWidth * sin) + (halfHeight * cos);
    }

    private float GetSpotX(int slot)
    {
        return spotCount <= 1
            ? settings.FirstParkingX
            : settings.FirstParkingX +
                ((settings.LastParkingX - settings.FirstParkingX) * slot /
                    (spotCount - 1));
    }

    private void ResolvePlayer(
        PlayerSession session,
        PlayerState state,
        CarParkOutcomeReason reason,
        Riptide.Server server)
    {
        if (state.Resolved)
            return;

        state.Resolved = true;
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(TerminalOutcomeEvent);
        outcome.AddByte((byte)reason);
        outcome.AddFloat(state.Pose.X);
        outcome.AddFloat(state.Pose.Y);
        outcome.AddFloat(state.Pose.HeadingDegrees);
        server.Send(outcome, session.ClientId);
        CompleteSubmission(
            session,
            reason == CarParkOutcomeReason.Parked
                ? Math.Max(0, settings.CorrectScore)
                : 0,
            server,
            completedAtSeconds: state.SimulatedAtSeconds);
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f)
            degrees -= 360f;
        else if (degrees < -180f)
            degrees += 360f;
        return degrees;
    }
}
