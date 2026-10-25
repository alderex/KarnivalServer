using System.Diagnostics;

/// <summary>A UTC-shaped clock driven by monotonic elapsed time after startup.</summary>
public interface IGameplayClock
{
    DateTime UtcNow { get; }
}

public sealed class MonotonicGameplayClock : IGameplayClock
{
    public static readonly MonotonicGameplayClock Instance = new();
    private readonly DateTime origin = DateTime.UtcNow;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    public DateTime UtcNow => origin + elapsed.Elapsed;
}
