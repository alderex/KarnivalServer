using System.Diagnostics;

/// <summary>Low-overhead measurements; no per-tick strings or log traffic on the fast path.</summary>
public static class ServerPerformance
{
    private static long lastWarning;
    public static void RecordTick(TimeSpan elapsed, TimeSpan budget)
    {
        if (elapsed <= budget || Stopwatch.GetElapsedTime(lastWarning).TotalSeconds < 10)
            return;
        lastWarning = Stopwatch.GetTimestamp();
        RiptideConsoleLogger.Warning($"Server tick exceeded budget: {elapsed.TotalMilliseconds:F1} ms / {budget.TotalMilliseconds:F1} ms.");
    }

    public static T MeasureDatabase<T>(string operation, Func<T> work)
    {
        long started = Stopwatch.GetTimestamp();
        try { return work(); }
        finally
        {
            double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (milliseconds >= 20)
                RiptideConsoleLogger.Warning($"Database {operation} blocked the server for {milliseconds:F1} ms.");
        }
    }
}
