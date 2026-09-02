using Riptide.Utils;

public static class RiptideConsoleLogger
{
    public static void Initialize()
    {
        RiptideLogger.Initialize(
            debugMethod: Info,
            infoMethod: Info,
            warningMethod: Warning,
            errorMethod: Error,
            includeTimestamps: true);
    }

    public static void Info(string message)
    {
        Console.WriteLine(message);
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }
}
