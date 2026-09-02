using System.Collections.Concurrent;

public sealed class ServerCommandConsole
{
    private readonly ConcurrentQueue<string> pendingCommands = new();
    private readonly Thread inputThread;
    private volatile bool isRunning;

    public ServerCommandConsole()
    {
        inputThread = new Thread(ReadCommands)
        {
            IsBackground = true,
            Name = "Server Command Console",
        };
    }

    public void Start()
    {
        if (isRunning)
            return;

        isRunning = true;
        inputThread.Start();
    }

    public bool TryDequeue(out string command)
    {
        return pendingCommands.TryDequeue(out command!);
    }

    public void Stop()
    {
        isRunning = false;
    }

    private void ReadCommands()
    {
        while (isRunning)
        {
            string? command = Console.ReadLine();
            if (command == null)
                return;

            if (!string.IsNullOrWhiteSpace(command))
                pendingCommands.Enqueue(command);
        }
    }
}
