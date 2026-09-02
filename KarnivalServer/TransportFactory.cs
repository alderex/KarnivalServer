using Riptide.Transports;
using Riptide.Transports.Udp;

public static class TransportFactory
{
    public static IServer Create(NetworkSimulationConfig simulation)
    {
        IServer transport = new UdpServer();
        if (!simulation.Enabled)
            return transport;

        return new SimulatedServer(
            transport,
            new NetworkSimulationSettings
            {
                IncomingLatencyMilliseconds = simulation.IncomingLatencyMilliseconds,
                OutgoingLatencyMilliseconds = simulation.OutgoingLatencyMilliseconds,
                JitterMilliseconds = simulation.JitterMilliseconds,
                IncomingLossChance = simulation.IncomingLossChance,
                OutgoingLossChance = simulation.OutgoingLossChance,
                RandomSeed = simulation.RandomSeed,
            });
    }
}
