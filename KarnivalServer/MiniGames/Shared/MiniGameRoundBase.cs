using Riptide;
using Karnival.Rules;

public abstract class MiniGameRoundBase
{
    public IGameplayClock Clock { get; set; } = MonotonicGameplayClock.Instance;
    protected DateTime NowUtc => Clock.UtcNow;

    protected MiniGameRoundBase(
        uint roundId,
        MiniGameType gameType,
        DateTime startsUtc,
        float durationSeconds,
        float resultsDurationSeconds,
        int maximumScore = 100)
    {
        RoundId = roundId;
        GameType = gameType;
        StartsUtc = startsUtc;
        IsStartScheduled = false;
        DurationSeconds = durationSeconds;
        ResultsDurationSeconds = resultsDurationSeconds;
        MaximumScore = Math.Max(0, maximumScore);
        Phase = RoundPhase.Active;
    }

    public uint RoundId { get; }
    public uint SessionId { get; private set; }
    public ushort SessionRoundNumber { get; private set; }
    public ushort TotalRounds { get; private set; }
    public MiniGameType GameType { get; }
    public DateTime StartsUtc { get; private set; }
    public bool IsStartScheduled { get; private set; }
    public float DurationSeconds { get; }
    public float ResultsDurationSeconds { get; }
    public int MaximumScore { get; }

    // Scheduled games may exhaust all opportunities before the round timer.
    protected virtual float SpeedBonusDurationSeconds => DurationSeconds;
    protected virtual float PlayableStartsAtSeconds => 0f;
    public PlayableTimeline Timeline => new(PlayableStartsAtSeconds,
        Math.Clamp(SpeedBonusDurationSeconds, 0f, DurationSeconds));
    public RoundPhase Phase { get; private set; }
    public DateTime EndsUtc => StartsUtc.AddSeconds(DurationSeconds);
    public virtual DateTime SubmissionDeadlineUtc => EndsUtc;
    public DateTime ResultsEndUtc { get; private set; }
    public virtual bool IncludesSimulatedPlayers => false;
    public virtual bool IsComplete => false;

    public Message CreateStartedMessage()
    {
        Message message = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameStarted);
        new Karnival.Protocol.RoundStartedPacket
        {
            RoundId = RoundId,
            SessionId = SessionId,
            RoundNumber = SessionRoundNumber,
            TotalRounds = TotalRounds,
            GameType = (ushort)GameType,
            StartUnixMs = IsStartScheduled ? new DateTimeOffset(StartsUtc).ToUnixTimeMilliseconds() : 0L,
            DurationSeconds = DurationSeconds,
            ResultsDurationSeconds = ResultsDurationSeconds,
            ElapsedSeconds = IsStartScheduled ? Math.Min((float)(NowUtc - StartsUtc).TotalSeconds, DurationSeconds) : 0f,
            PlayableStartsAt = Timeline.StartsAt,
            PlayableEndsAt = Timeline.EndsAt,
        }.Write(message);
        WriteStartedPayload(message);
        return message;
    }

    public Message CreateCountdownMessage()
    {
        Message message = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameCountdown);
        message.AddUInt(RoundId);
        message.AddUShort((ushort)GameType);
        message.AddLong(new DateTimeOffset(StartsUtc).ToUnixTimeMilliseconds());
        message.AddFloat(Math.Min(
            (float)(NowUtc - StartsUtc).TotalSeconds,
            DurationSeconds));
        return message;
    }

    public void ScheduleStart(DateTime startsUtc)
    {
        if (IsStartScheduled)
            return;
        StartsUtc = startsUtc;
        IsStartScheduled = true;
    }

    public void ConfigureSession(uint sessionId, ushort sessionRoundNumber, ushort totalRounds)
    {
        SessionId = sessionId;
        SessionRoundNumber = sessionRoundNumber;
        TotalRounds = totalRounds;
    }

    public void MarkResults(DateTime nowUtc)
    {
        Phase = RoundPhase.Results;
        ResultsEndUtc = nowUtc.AddSeconds(ResultsDurationSeconds);
    }

    public virtual void RegisterPlayer(PlayerSession session, DateTime nowUtc) { }

    public virtual void UnregisterPlayer(
        PlayerSession session,
        DateTime nowUtc,
        Riptide.Server server) { }

    public virtual void SynchronizePlayer(
        PlayerSession session,
        Riptide.Server server)
    {
        if (session.SubmittedThisRound)
            SendScore(session, server);
    }

    public virtual void Update(
        DateTime nowUtc,
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server) { }

    public abstract void HandleInput(
        Message message,
        PlayerSession session,
        Riptide.Server server);

    public virtual void FinalizeRound(
        IEnumerable<PlayerSession> sessions,
        Riptide.Server server)
    {
        foreach (PlayerSession session in sessions)
            CompleteSubmission(session, 0, server, completedAtSeconds: DurationSeconds,
                reason: CompletionReason.Timeout);
    }

    public abstract string Describe();

    protected abstract void WriteStartedPayload(Message message);

    protected bool CompleteSubmission(
        PlayerSession session,
        int score,
        Riptide.Server server,
        int? maximumScore = null,
        float? completedAtSeconds = null,
        CompletionReason reason = CompletionReason.Success)
    {
        if (Phase != RoundPhase.Active || session.SubmittedThisRound)
            return false;

        int applicableMaximumScore = Math.Max(0, maximumScore ?? MaximumScore);
        int normalizedScore = Math.Clamp(score, 0, applicableMaximumScore);
        float completionElapsed = Math.Clamp(
            completedAtSeconds ?? (float)(NowUtc - StartsUtc).TotalSeconds,
            0f,
            DurationSeconds);
        float speedMultiplier = Timeline.Multiplier(completionElapsed, reason);
        int finalScore = ApplyMultiplier(normalizedScore, speedMultiplier);
        int finalMaximumScore = ApplyMultiplier(
            applicableMaximumScore,
            speedMultiplier);
        session.ApplyRoundResult(new PlayerRoundResult(
            true,
            normalizedScore,
            finalScore,
            finalMaximumScore,
            speedMultiplier));

        if (session.IsSimulated)
            return true;

        SendScore(session, server);
        return true;
    }

    protected void SendScore(PlayerSession session, Riptide.Server server)
    {
        if (session.IsSimulated || !session.SubmittedThisRound)
            return;

        Message response = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.MiniGameScore);
        response.AddUInt(RoundId);
        response.AddUShort((ushort)GameType);
        response.AddInt(session.RoundResult.BaseScore);
        response.AddInt(session.RoundScore);
        response.AddInt(session.TotalScore);
        response.AddInt(session.RoundResult.MaximumScore);
        response.AddFloat(session.RoundResult.SpeedMultiplier);
        server.Send(response, session.ClientId);
    }

    private static int ApplyMultiplier(int score, float multiplier)
    {
        double scaled = Math.Round(
            score * (double)multiplier,
            MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(scaled, 0d, int.MaxValue);
    }

    protected static float Clamp01(float value)
    {
        if (float.IsNaN(value))
            return 0f;

        return Math.Clamp(value, 0f, 1f);
    }
}
