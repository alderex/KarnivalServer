using Riptide;

public sealed class ArcheryPracticeRound : MiniGameRoundBase
{
    private const byte ShotOutcomeEvent = 1;

    private readonly record struct Point(float X, float Y);

    private readonly record struct PendingShot(
        ushort ShotId,
        bool Hit,
        int Score,
        float ResolvesAtSeconds);

    private sealed class PlayerState
    {
        public PlayerState(bool eligible)
        {
            Eligible = eligible;
        }

        public bool Eligible { get; }
        public HashSet<ushort> UsedShotIds { get; } = new();
        public ushort? ChargingShotId { get; set; }
        public float ChargeStartedAtSeconds { get; set; }
        public PendingShot? Pending { get; set; }
    }

    private readonly float inputDurationSeconds;
    private readonly float outcomeBufferSeconds;
    private readonly float windAccelerationX;
    private readonly ArcheryPracticeSettings settings;
    private readonly Dictionary<ushort, PlayerState> playerStates = new();

    public ArcheryPracticeRound(
        uint roundId,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        float inputDurationSeconds,
        float outcomeBufferSeconds,
        float windAccelerationX,
        ArcheryPracticeSettings settings)
        : base(
            roundId,
            MiniGameType.ArcheryPractice,
            startsUtc,
            durationSeconds,
            resultsDurationSeconds,
            Math.Max(0, settings.CorrectScore))
    {
        this.inputDurationSeconds = inputDurationSeconds;
        this.outcomeBufferSeconds = outcomeBufferSeconds;
        this.windAccelerationX = windAccelerationX;
        this.settings = settings;
    }

    public override void RegisterPlayer(PlayerSession session, DateTime nowUtc)
    {
        if (playerStates.ContainsKey(session.ClientId))
            return;

        playerStates.Add(
            session.ClientId,
            new PlayerState(GetElapsedSeconds(nowUtc) < inputDurationSeconds));
    }

    public override void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        float elapsed = GetElapsedSeconds(nowUtc);
        foreach (PlayerSession session in sessions)
        {
            RegisterPlayer(session, nowUtc);
            PlayerState state = playerStates[session.ClientId];
            if (!state.Eligible || session.SubmittedThisRound)
                continue;

            ResolvePendingIfDue(session, state, elapsed, server);
            if (!session.SubmittedThisRound &&
                state.Pending == null &&
                elapsed >= inputDurationSeconds + InputGraceSeconds)
            {
                state.ChargingShotId = null;
                CompleteSubmission(session, 0, server);
            }
        }
    }

    public override void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server)
    {
        ArcheryPracticeInputType inputType =
            (ArcheryPracticeInputType)message.GetByte();
        DateTime nowUtc = DateTime.UtcNow;
        RegisterPlayer(session, nowUtc);
        PlayerState state = playerStates[session.ClientId];
        float receivedAt = GetElapsedSeconds(nowUtc);
        ResolvePendingIfDue(session, state, receivedAt, server);
        if (session.SubmittedThisRound)
            return;

        switch (inputType)
        {
            case ArcheryPracticeInputType.ChargeStarted:
                HandleChargeStarted(message, state, receivedAt);
                break;
            case ArcheryPracticeInputType.ShotReleased:
                HandleShotReleased(message, session, state, receivedAt, server);
                break;
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
            if (!state.Eligible)
            {
                session.MarkMissedRound();
                continue;
            }

            ResolvePendingIfDue(session, state, DurationSeconds, server);
            if (!session.SubmittedThisRound)
                CompleteSubmission(session, 0, server);
        }
    }

    public override string Describe()
    {
        return $"input={inputDurationSeconds:0.##}s charge={MaximumChargeSeconds:0.##}s wind={windAccelerationX:+0.##;-0.##;0}";
    }

    protected override void WriteStartedPayload(Message message)
    {
        message.AddFloat(inputDurationSeconds);
        message.AddFloat(outcomeBufferSeconds);
        message.AddFloat(MaximumChargeSeconds);
        message.AddFloat(MinimumLaunchSpeed);
        message.AddFloat(MaximumLaunchSpeed);
        message.AddFloat(Gravity);
        message.AddFloat(windAccelerationX);
        message.AddFloat(MaximumFlightSeconds);
        message.AddFloat(SimulationStepSeconds);
        message.AddFloat(MinimumAimDegrees);
        message.AddFloat(MaximumAimDegrees);
        message.AddFloat(PlayAreaHalfWidth);
        message.AddFloat(PlayAreaHalfHeight);
        message.AddFloat(settings.GroundY);
        message.AddFloat(settings.BowX);
        message.AddFloat(settings.BowY);
        message.AddFloat(ArrowHalfLength);
        message.AddFloat(settings.TargetX);
        message.AddFloat(settings.TargetY);
        message.AddFloat(TargetHalfWidth);
        message.AddFloat(TargetHalfHeight);
    }

    private void HandleChargeStarted(
        Message message,
        PlayerState state,
        float receivedAt)
    {
        ushort shotId = message.GetUShort();
        float startedAt = message.GetFloat();
        if (!state.Eligible ||
            state.Pending != null ||
            state.ChargingShotId.HasValue ||
            state.UsedShotIds.Contains(shotId) ||
            !IsValidInputTimestamp(startedAt, receivedAt))
        {
            return;
        }

        state.UsedShotIds.Add(shotId);
        state.ChargingShotId = shotId;
        state.ChargeStartedAtSeconds = startedAt;
    }

    private void HandleShotReleased(
        Message message,
        PlayerSession session,
        PlayerState state,
        float receivedAt,
        Riptide.Server server)
    {
        ushort shotId = message.GetUShort();
        float angleDegrees = message.GetFloat();
        float releasedAt = message.GetFloat();
        bool accepted = state.Eligible &&
            state.Pending == null &&
            state.ChargingShotId == shotId &&
            float.IsFinite(angleDegrees) &&
            angleDegrees >= MinimumAimDegrees &&
            angleDegrees <= MaximumAimDegrees &&
            IsValidInputTimestamp(releasedAt, receivedAt) &&
            releasedAt >= state.ChargeStartedAtSeconds;

        float power = 0f;
        ArcheryShotResult result = new(
            false,
            0f,
            settings.BowX,
            settings.BowY,
            0f);
        if (accepted)
        {
            power = Math.Clamp(
                (releasedAt - state.ChargeStartedAtSeconds) /
                    MaximumChargeSeconds,
                0f,
                1f);
            result = SimulateShot(angleDegrees, power);
            state.Pending = new PendingShot(
                shotId,
                result.Hit,
                GetHitScore(result),
                releasedAt + result.FlightSeconds);
        }

        if (state.ChargingShotId == shotId)
            state.ChargingShotId = null;

        SendShotOutcome(
            session,
            shotId,
            accepted,
            result,
            angleDegrees,
            power,
            releasedAt,
            server);
    }

    private void ResolvePendingIfDue(
        PlayerSession session,
        PlayerState state,
        float elapsed,
        Riptide.Server server)
    {
        if (state.Pending is not PendingShot pending ||
            elapsed + 0.00001f < pending.ResolvesAtSeconds)
        {
            return;
        }

        state.Pending = null;
        if (pending.Hit)
        {
            CompleteSubmission(session, pending.Score, server);
        }
        else if (elapsed >= inputDurationSeconds + InputGraceSeconds)
        {
            CompleteSubmission(session, 0, server);
        }
    }

    private int GetHitScore(ArcheryShotResult result)
    {
        if (!result.Hit)
            return 0;

        int fullScore = Math.Max(0, settings.CorrectScore);
        return result.ImpactY >= settings.TargetY
            ? fullScore
            : fullScore / 2;
    }

    private void SendShotOutcome(
        PlayerSession session,
        ushort shotId,
        bool accepted,
        ArcheryShotResult result,
        float angleDegrees,
        float power,
        float releasedAt,
        Riptide.Server server)
    {
        Message outcome = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameEvent);
        outcome.AddUInt(RoundId);
        outcome.AddUShort((ushort)GameType);
        outcome.AddByte(ShotOutcomeEvent);
        outcome.AddUShort(shotId);
        outcome.AddBool(accepted);
        outcome.AddBool(accepted && result.Hit);
        outcome.AddFloat(float.IsFinite(angleDegrees) ? angleDegrees : 0f);
        outcome.AddFloat(power);
        outcome.AddFloat(float.IsFinite(releasedAt) ? releasedAt : 0f);
        outcome.AddFloat(result.FlightSeconds);
        outcome.AddFloat(result.ImpactX);
        outcome.AddFloat(result.ImpactY);
        outcome.AddFloat(result.ImpactAngleDegrees);
        server.Send(outcome, session.ClientId);
    }

    private ArcheryShotResult SimulateShot(float angleDegrees, float power)
    {
        float radians = angleDegrees * (MathF.PI / 180f);
        float speed = MinimumLaunchSpeed +
            ((MaximumLaunchSpeed - MinimumLaunchSpeed) * power);
        float velocityX = MathF.Cos(radians) * speed;
        float velocityY = MathF.Sin(radians) * speed;
        float arrowHalfLength = ArrowHalfLength;
        Point bow = new(settings.BowX, settings.BowY);
        Point initialCenter = GetArrowTip(bow, angleDegrees, arrowHalfLength);
        Point previousCenter = initialCenter;
        Point previousTip = GetArrowTip(previousCenter, angleDegrees, arrowHalfLength);
        float previousTime = 0f;

        for (float time = SimulationStepSeconds;
            time <= MaximumFlightSeconds + 0.00001f;
            time += SimulationStepSeconds)
        {
            float clampedTime = Math.Min(time, MaximumFlightSeconds);
            Point center = new(
                initialCenter.X + (velocityX * clampedTime) +
                    (0.5f * windAccelerationX * clampedTime * clampedTime),
                initialCenter.Y + (velocityY * clampedTime) -
                    (0.5f * Gravity * clampedTime * clampedTime));
            float currentVelocityX = velocityX +
                (windAccelerationX * clampedTime);
            float currentVelocityY = velocityY - (Gravity * clampedTime);
            float heading = MathF.Atan2(currentVelocityY, currentVelocityX) *
                (180f / MathF.PI);
            Point tip = GetArrowTip(center, heading, arrowHalfLength);

            if (TryIntersectTarget(previousTip, tip, out float hitFraction, out Point hitPoint))
            {
                float hitTime = previousTime +
                    ((clampedTime - previousTime) * hitFraction);
                float hitVelocityX = velocityX +
                    (windAccelerationX * hitTime);
                float hitVelocityY = velocityY - (Gravity * hitTime);
                float hitHeading = MathF.Atan2(hitVelocityY, hitVelocityX) *
                    (180f / MathF.PI);
                return new ArcheryShotResult(
                    true,
                    hitTime,
                    hitPoint.X,
                    hitPoint.Y,
                    hitHeading);
            }

            if (tip.Y <= settings.GroundY)
            {
                float denominator = previousTip.Y - tip.Y;
                float fraction = denominator <= 0.00001f
                    ? 1f
                    : Math.Clamp(
                        (previousTip.Y - settings.GroundY) / denominator,
                        0f,
                        1f);
                Point impact = Lerp(previousTip, tip, fraction);
                float impactTime = previousTime +
                    ((clampedTime - previousTime) * fraction);
                float impactVelocityX = velocityX +
                    (windAccelerationX * impactTime);
                float impactVelocityY = velocityY - (Gravity * impactTime);
                return new ArcheryShotResult(
                    false,
                    impactTime,
                    impact.X,
                    settings.GroundY,
                    MathF.Atan2(impactVelocityY, impactVelocityX) * (180f / MathF.PI));
            }

            if (MathF.Abs(tip.X) > PlayAreaHalfWidth ||
                tip.Y > PlayAreaHalfHeight)
            {
                return new ArcheryShotResult(
                    false,
                    clampedTime,
                    tip.X,
                    tip.Y,
                    heading);
            }

            previousCenter = center;
            previousTip = tip;
            previousTime = clampedTime;
            if (clampedTime >= MaximumFlightSeconds)
                break;
        }

        float finalVelocityX = velocityX +
            (windAccelerationX * previousTime);
        float finalVelocityY = velocityY - (Gravity * previousTime);
        return new ArcheryShotResult(
            false,
            previousTime,
            previousTip.X,
            previousTip.Y,
            MathF.Atan2(finalVelocityY, finalVelocityX) * (180f / MathF.PI));
    }

    private bool TryIntersectTarget(
        Point start,
        Point end,
        out float fraction,
        out Point hitPoint)
    {
        float targetHalfWidth = TargetHalfWidth;
        float targetHalfHeight = TargetHalfHeight;
        float minimumX = settings.TargetX - targetHalfWidth;
        float maximumX = settings.TargetX + targetHalfWidth;
        float minimumY = settings.TargetY - targetHalfHeight;
        float maximumY = settings.TargetY + targetHalfHeight;
        float deltaX = end.X - start.X;
        float deltaY = end.Y - start.Y;
        float enter = 0f;
        float exit = 1f;
        bool intersects = Clip(-deltaX, start.X - minimumX, ref enter, ref exit) &&
            Clip(deltaX, maximumX - start.X, ref enter, ref exit) &&
            Clip(-deltaY, start.Y - minimumY, ref enter, ref exit) &&
            Clip(deltaY, maximumY - start.Y, ref enter, ref exit);
        fraction = intersects ? enter : 0f;
        hitPoint = intersects ? Lerp(start, end, enter) : end;
        return intersects;
    }

    private static bool Clip(float direction, float distance, ref float enter, ref float exit)
    {
        if (MathF.Abs(direction) < 0.00001f)
            return distance >= 0f;

        float ratio = distance / direction;
        if (direction < 0f)
        {
            if (ratio > exit)
                return false;
            enter = Math.Max(enter, ratio);
        }
        else
        {
            if (ratio < enter)
                return false;
            exit = Math.Min(exit, ratio);
        }

        return enter <= exit;
    }

    private bool IsValidInputTimestamp(float timestamp, float receivedAt)
    {
        return float.IsFinite(timestamp) &&
            timestamp >= 0f &&
            timestamp <= inputDurationSeconds &&
            timestamp <= receivedAt + FutureInputToleranceSeconds &&
            receivedAt - timestamp <= InputGraceSeconds;
    }

    private static Point GetArrowTip(Point center, float angleDegrees, float halfLength)
    {
        float radians = angleDegrees * (MathF.PI / 180f);
        return new Point(
            center.X + (MathF.Cos(radians) * halfLength),
            center.Y + (MathF.Sin(radians) * halfLength));
    }

    private static Point Lerp(Point start, Point end, float amount)
    {
        return new Point(
            start.X + ((end.X - start.X) * amount),
            start.Y + ((end.Y - start.Y) * amount));
    }

    private float GetElapsedSeconds(DateTime nowUtc)
    {
        return Math.Clamp(
            (float)(nowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
    }

    private float MaximumChargeSeconds => Math.Max(0.1f, settings.MaximumChargeSeconds);
    private float MinimumLaunchSpeed => Math.Max(1f, settings.MinimumLaunchSpeed);
    private float MaximumLaunchSpeed => Math.Max(MinimumLaunchSpeed, settings.MaximumLaunchSpeed);
    private float Gravity => Math.Max(0f, settings.Gravity);
    private float MaximumFlightSeconds => Math.Max(0.25f, settings.MaximumFlightSeconds);
    private float SimulationStepSeconds => Math.Clamp(settings.SimulationStepSeconds, 0.001f, 0.05f);
    private float MinimumAimDegrees => Math.Clamp(settings.MinimumAimDegrees, -89f, 89f);
    private float MaximumAimDegrees => Math.Clamp(settings.MaximumAimDegrees, MinimumAimDegrees, 89f);
    private float PlayAreaHalfWidth => Math.Max(1f, settings.PlayAreaHalfWidth);
    private float PlayAreaHalfHeight => Math.Max(1f, settings.PlayAreaHalfHeight);
    private float ArrowHalfLength => Math.Max(0f, settings.ArrowHalfLength);
    private float TargetHalfWidth => Math.Max(1f, settings.TargetHalfWidth);
    private float TargetHalfHeight => Math.Max(1f, settings.TargetHalfHeight);
    private float InputGraceSeconds => Math.Max(0f, settings.InputGraceSeconds);
    private float FutureInputToleranceSeconds => Math.Max(0f, settings.FutureInputToleranceSeconds);
}
