#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Riptide.Transports
{
    public sealed class NetworkSimulationSettings
    {
        public int IncomingLatencyMilliseconds { get; set; }
        public int OutgoingLatencyMilliseconds { get; set; }
        public int JitterMilliseconds { get; set; }
        public float IncomingLossChance { get; set; }
        public float OutgoingLossChance { get; set; }
        public int? RandomSeed { get; set; }
    }

    public sealed class SimulatedServer : IServer
    {
        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public ushort Port => inner.Port;

        private readonly IServer inner;
        private readonly NetworkSimulationSettings settings;
        private readonly Dictionary<Connection, SimulatedConnection> wrappedConnections = new Dictionary<Connection, SimulatedConnection>();
        private readonly List<DelayedPacket> delayedPackets = new List<DelayedPacket>();
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Random random;

        public SimulatedServer(IServer inner, NetworkSimulationSettings settings)
        {
            this.inner = inner;
            this.settings = settings ?? new NetworkSimulationSettings();
            random = this.settings.RandomSeed.HasValue
                ? new Random(this.settings.RandomSeed.Value)
                : new Random();

            inner.Connected += OnInnerConnected;
            inner.DataReceived += OnInnerDataReceived;
            inner.Disconnected += OnInnerDisconnected;
        }

        public void Start(ushort port)
        {
            clock.Restart();
            inner.Start(port);
        }

        public void Poll()
        {
            inner.Poll();
            FlushDelayedPackets();
        }

        public void Close(Connection connection)
        {
            inner.Close(Unwrap(connection));
        }

        public void Shutdown()
        {
            delayedPackets.Clear();
            wrappedConnections.Clear();
            inner.Shutdown();
            clock.Reset();
        }

        internal void SendOutgoing(Connection innerConnection, byte[] dataBuffer, int amount)
        {
            if (ShouldDrop(settings.OutgoingLossChance))
                return;

            byte[] packet = CopyPacket(dataBuffer, amount);
            Enqueue(settings.OutgoingLatencyMilliseconds, () => innerConnection.Send(packet, amount));
        }

        private void OnInnerConnected(object sender, ConnectedEventArgs eventArgs)
        {
            SimulatedConnection connection = GetOrCreateWrappedConnection(eventArgs.Connection);
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        private void OnInnerDataReceived(object sender, DataReceivedEventArgs eventArgs)
        {
            if (ShouldDrop(settings.IncomingLossChance))
                return;

            byte[] packet = CopyPacket(eventArgs.DataBuffer, eventArgs.Amount);
            SimulatedConnection connection = GetOrCreateWrappedConnection(eventArgs.FromConnection);

            Enqueue(settings.IncomingLatencyMilliseconds, () =>
            {
                DataReceived?.Invoke(this, new DataReceivedEventArgs(packet, eventArgs.Amount, connection));
            });
        }

        private void OnInnerDisconnected(object sender, DisconnectedEventArgs eventArgs)
        {
            SimulatedConnection connection = GetOrCreateWrappedConnection(eventArgs.Connection);
            wrappedConnections.Remove(eventArgs.Connection);
            Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, eventArgs.Reason));
        }

        private SimulatedConnection GetOrCreateWrappedConnection(Connection innerConnection)
        {
            if (wrappedConnections.TryGetValue(innerConnection, out SimulatedConnection connection))
                return connection;

            connection = new SimulatedConnection(innerConnection, this);
            wrappedConnections.Add(innerConnection, connection);
            return connection;
        }

        private Connection Unwrap(Connection connection)
        {
            return connection is SimulatedConnection simulatedConnection
                ? simulatedConnection.InnerConnection
                : connection;
        }

        private void Enqueue(int baseDelayMilliseconds, Action action)
        {
            int jitter = settings.JitterMilliseconds <= 0
                ? 0
                : random.Next(-settings.JitterMilliseconds, settings.JitterMilliseconds + 1);
            int delay = Math.Max(0, baseDelayMilliseconds + jitter);

            delayedPackets.Add(new DelayedPacket(clock.ElapsedMilliseconds + delay, action));
        }

        private bool ShouldDrop(float lossChance)
        {
            if (lossChance <= 0f)
                return false;
            if (lossChance >= 1f)
                return true;

            return random.NextDouble() < lossChance;
        }

        private void FlushDelayedPackets()
        {
            long now = clock.ElapsedMilliseconds;
            for (int i = 0; i < delayedPackets.Count;)
            {
                if (delayedPackets[i].DeliveryTime > now)
                {
                    i++;
                    continue;
                }

                DelayedPacket packet = delayedPackets[i];
                delayedPackets.RemoveAt(i);
                packet.Deliver();
            }
        }

        private static byte[] CopyPacket(byte[] dataBuffer, int amount)
        {
            byte[] packet = new byte[amount];
            Buffer.BlockCopy(dataBuffer, 0, packet, 0, amount);
            return packet;
        }

        private readonly struct DelayedPacket
        {
            public readonly long DeliveryTime;
            private readonly Action deliver;

            public DelayedPacket(long deliveryTime, Action deliver)
            {
                DeliveryTime = deliveryTime;
                this.deliver = deliver;
            }

            public void Deliver()
            {
                deliver();
            }
        }

        private sealed class SimulatedConnection : Connection
        {
            public readonly Connection InnerConnection;

            private readonly SimulatedServer server;

            public SimulatedConnection(Connection innerConnection, SimulatedServer server)
            {
                InnerConnection = innerConnection;
                this.server = server;
            }

            protected internal override void Send(byte[] dataBuffer, int amount)
            {
                server.SendOutgoing(InnerConnection, dataBuffer, amount);
            }

            public override string ToString()
            {
                return InnerConnection.ToString();
            }
        }
    }
}

#pragma warning restore CS1591
