using Karnival.Protocol;
using Karnival.Rules;
using Riptide;

internal static partial class RegressionTests
{
    private sealed class ManualClock : IGameplayClock
    {
        public DateTime UtcNow { get; set; }
    }

    static void TestPlayableTimeline()
    {
        var timeline = new PlayableTimeline(0.75f, 10.75f);
        AssertEx.Equal(3f, timeline.Multiplier(0.75f));
        AssertEx.Equal(2f, timeline.Multiplier(5.75f));
        AssertEx.Equal(1f, timeline.Multiplier(10.75f));
        AssertEx.Equal(0f, timeline.Remaining(10.75f));
        AssertEx.Equal(1f, timeline.Multiplier(5f, CompletionReason.Exhausted));
        AssertEx.Equal(1f, timeline.Multiplier(5f, CompletionReason.Timeout));
        AssertEx.Equal(1f, new PlayableTimeline(2f, 1f).Multiplier(0f));
    }

    static void TestEveryGameTimeout()
    {
        MiniGameCatalog catalog = MiniGameCatalog.CreateDefault(new ServerConfig());
        var failures = new List<string>();
        foreach (MiniGameType type in Enum.GetValues<MiniGameType>())
        {
            catalog.TryGetGame(type.ToString(), out IMiniGame game);
            var clock = new ManualClock { UtcNow = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            // Stationary driving reaches timeout rather than an earlier out-of-bounds failure.
            if (type == MiniGameType.CarPark) game = new CarParkMiniGame(new CarParkSettings { ForwardSpeed = 0f });
            MiniGameRoundBase round = game.CreateRound(new MiniGameRoundStartContext(995, clock.UtcNow, 10f, 1f, new Random(7)));
            round.Clock = clock;
            round.ScheduleStart(clock.UtcNow);
            var player = new PlayerSession(1, 1, "timeout", isSimulated: true);
            round.RegisterPlayer(player, clock.UtcNow);
            round.FinalizeRound(new[] { player }, new Riptide.Server());
            if (player.RoundResult.SpeedMultiplier != 1f)
                failures.Add(type + ": " + player.RoundResult.SpeedMultiplier);
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(", ", failures));
    }

    static void TestSharedRoundPackets()
    {
        MiniGameCatalog catalog = MiniGameCatalog.CreateDefault(new ServerConfig());
        foreach (MiniGameType type in Enum.GetValues<MiniGameType>())
        {
            AssertEx.True(catalog.TryGetGame(type.ToString(), out IMiniGame game));
            MiniGameRoundBase round = game.CreateRound(Context(991, 71));
            Message message = round.CreateStartedMessage();
            try
            {
                AssertEx.Equal((ulong)NetworkMessageId.MiniGameStarted, message.GetVarULong());
            RoundStartedPacket packet = RoundStartedPacket.Read(message);
                AssertEx.Equal((ushort)type, packet.GameType);
                AssertEx.Equal(round.Timeline.StartsAt, packet.PlayableStartsAt);
                AssertEx.Equal(round.Timeline.EndsAt, packet.PlayableEndsAt);
                var timeline = new PlayableTimeline(packet.PlayableStartsAt, packet.PlayableEndsAt);
                AssertEx.Equal(0f, timeline.Remaining(timeline.EndsAt));
                AssertEx.Equal(1f, timeline.Multiplier(timeline.EndsAt));
            }
            finally { message.Release(); }
        }
    }

    static void TestSharedPlatePacket()
    {
        var round = (PlateStackerRound)new PlateStackerMiniGame(new PlateStackerSettings())
            .CreateRound(Context(992, 72));
        Message message = round.CreateStartedMessage();
        try
        {
            AssertEx.Equal((ulong)NetworkMessageId.MiniGameStarted, message.GetVarULong());
            RoundStartedPacket header = RoundStartedPacket.Read(message);
            PlateStackerPacket packet = PlateStackerPacket.Read(message);
            AssertEx.Equal((int)packet.PlateCount, packet.Schedule.Length);
            AssertEx.True(packet.Schedule.Length > 0);
            float lastExit = packet.Schedule.Max(entry =>
                PlateStackerRules.ExitSeconds(entry.SpawnSeconds, entry.FallDurationSeconds));
            AssertEx.Equal(Math.Min(header.DurationSeconds, lastExit), header.PlayableEndsAt);
            foreach (PlateSchedulePacket entry in packet.Schedule)
            {
                AssertEx.True(PlateStackerRules.ContactSeconds(entry.SpawnSeconds, entry.FallDurationSeconds, 2)
                    < PlateStackerRules.ContactSeconds(entry.SpawnSeconds, entry.FallDurationSeconds, 0));
            }
        }
        finally { message.Release(); }
    }

    static void TestCarParkTimeoutTimeline()
    {
        var clock = new ManualClock { UtcNow = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var settings = new CarParkSettings { ForwardSpeed = 0f };
        var round = (CarParkRound)new CarParkMiniGame(settings).CreateRound(
            new MiniGameRoundStartContext(993, clock.UtcNow, 10f, 1f, new Random(3)));
        round.Clock = clock;
        round.ScheduleStart(clock.UtcNow);
        var player = new PlayerSession(1, 1, "timeout", isSimulated: true);
        round.RegisterPlayer(player, clock.UtcNow);
        clock.UtcNow = round.StartsUtc.AddSeconds(round.DrivingEndsAtSeconds + settings.InputGraceSeconds + 0.01f);
        round.Update(clock.UtcNow, new[] { player }, new Riptide.Server());
        AssertEx.True(player.SubmittedThisRound);
        AssertEx.Equal(1f, player.RoundResult.SpeedMultiplier);
        AssertEx.Equal(0f, round.Timeline.Remaining(round.DrivingEndsAtSeconds));
    }

    static void TestArcheryEventTimestamp()
    {
        PlayerRoundResult Run(float delay)
        {
            var clock = new ManualClock { UtcNow = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            var settings = new ArcheryPracticeSettings
            {
                BowX = 0f, BowY = 0f, TargetX = 100f, TargetY = 0f,
                TargetHalfWidth = 200f, TargetHalfHeight = 200f,
                MinimumWindAcceleration = 0f, MaximumWindAcceleration = 0f,
            };
            var round = new ArcheryPracticeRound(994, clock.UtcNow, 13.5f, 1f, 10f, 3.5f, 0f, settings)
                { Clock = clock };
            round.ScheduleStart(clock.UtcNow);
            var player = new PlayerSession(1, 1, "archer", isSimulated: true);
            var server = new Riptide.Server();
            round.RegisterPlayer(player, clock.UtcNow);
            Message charge = Message.Create().AddByte((byte)ArcheryPracticeInputType.ChargeStarted)
                .AddUShort(1).AddFloat(0f);
            try { round.HandleInput(charge, player, server); }
            finally { charge.Release(); }
            clock.UtcNow = round.StartsUtc.AddSeconds(1);
            Message release = Message.Create().AddByte((byte)ArcheryPracticeInputType.ShotReleased)
                .AddUShort(1).AddFloat(0f).AddFloat(1f);
            try { round.HandleInput(release, player, server); }
            finally { release.Release(); }
            clock.UtcNow = round.StartsUtc.AddSeconds(delay);
            round.Update(clock.UtcNow, new[] { player }, server);
            AssertEx.True(player.SubmittedThisRound);
            AssertEx.True(player.RoundResult.BaseScore > 0);
            return player.RoundResult;
        }
        PlayerRoundResult immediate = Run(2f);
        PlayerRoundResult delayed = Run(8f);
        AssertEx.Equal(immediate.SpeedMultiplier, delayed.SpeedMultiplier);
        AssertEx.Equal(immediate.Score, delayed.Score);
    }

    static void TestSharedMazePrediction()
    {
        MazeLayout layout = MazeGenerator.Generate(123, 12);
        var authoritative = new MazeMovementSimulator(layout, 0.01f, 0.01f);
        var predicted = new MazeMotion(layout.GridSize, layout.Walls, 0.01f, 0.01f);
        var random = new Random(7);
        var position = new MazePoint(0.5f, 0.5f);
        for (int index = 0; index < 100; index++)
        {
            var target = new MazePoint((float)random.NextDouble(), (float)random.NextDouble());
            MazePoint expected = authoritative.Move(position, target).Position;
            position = predicted.MovePosition(position, target);
            AssertEx.Equal(expected.X, position.X);
            AssertEx.Equal(expected.Y, position.Y);
        }
    }

    static void TestDirectionalReplay()
    {
        var history = new DirectionalInputHistory();
        history.Record(0f, 1);
        history.Record(1f, -1);
        AssertEx.True(Math.Abs(history.Replay(0.5f, 0f, 2f, 0.2f, 0f, 1f) - 0.5f) < 0.00001f);
        AssertEx.True(Math.Abs(history.Replay(0.7f, 1f, 2f, 0.2f, 0f, 1f) - 0.5f) < 0.00001f);
        history.Clear();
        AssertEx.Equal(0.5f, history.Replay(0.5f, 0f, 2f, 0.2f, 0f, 1f));
    }

    static void TestBettingLedger()
    {
        var first = new PlayerSession(1, 1, "first");
        var second = new PlayerSession(2, 2, "second");
        first.RestoreSessionScore(100);
        second.RestoreSessionScore(100);
        var ledger = new SessionBettingLedger(50);
        ledger.Initialize(new[] { first, second }, new Random(1));
        AssertEx.True(!ledger.TryLock(first, 51, out _));
        AssertEx.True(ledger.TryLock(first, 40, out _));
        AssertEx.True(!ledger.TryLock(first, 1, out _));
        AssertEx.True(ledger.TryLock(second, 20, out _));
        AssertEx.True(ledger.AllHumansLocked(new[] { first, second }));
        var losses = new Dictionary<long, int>();
        ledger.Resolve(100, id => id == first.UserId ? 100 : 50,
            (id, amount) => { if (id == first.UserId) first.AddSessionScore(amount); }, losses);
        AssertEx.Equal(140, first.TotalScore);
        AssertEx.Equal(-20, losses[second.UserId]);
        AssertEx.True(!ledger.Values.Any());
        ledger.Ensure(second);
        AssertEx.True(ledger.TryLock(second, 10, out _));
        ledger.Refund((id, amount) => second.AddSessionScore(amount));
        AssertEx.Equal(80, second.TotalScore);
    }
}
