ServerConfig config;
try
{
    config = ServerConfigLoader.Load(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Configuration error: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

using CancellationTokenSource shutdown = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

RiptideConsoleLogger.Initialize();

Server server = new(config);
server.Run(shutdown.Token);
