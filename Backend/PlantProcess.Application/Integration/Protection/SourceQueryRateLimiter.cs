using System;
using System.Collections.Concurrent;

namespace PlantProcess.Application.Integration.Protection;

/// <summary>
/// Counts live source queries per source within a trailing one-minute window,
/// so the load policy can enforce a per-source rate ceiling.
/// </summary>
public interface ISourceQueryRateLimiter
{
    int CountWithinLastMinute(string sourceKey, DateTime utcNow);
    void Record(string sourceKey, DateTime utcNow);
}

public sealed class SlidingWindowSourceQueryRateLimiter : ISourceQueryRateLimiter
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _hits = new();

    public int CountWithinLastMinute(string sourceKey, DateTime utcNow)
    {
        var queue = _hits.GetOrAdd(sourceKey, _ => new ConcurrentQueue<DateTime>());
        Prune(queue, utcNow);
        return queue.Count;
    }

    public void Record(string sourceKey, DateTime utcNow)
    {
        var queue = _hits.GetOrAdd(sourceKey, _ => new ConcurrentQueue<DateTime>());
        queue.Enqueue(utcNow);
        Prune(queue, utcNow);
    }

    private static void Prune(ConcurrentQueue<DateTime> queue, DateTime utcNow)
    {
        var cutoff = utcNow.AddMinutes(-1);
        while (queue.TryPeek(out var timestamp) && timestamp < cutoff) {
            queue.TryDequeue(out _);
        }
    }
}