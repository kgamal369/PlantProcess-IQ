using Microsoft.Extensions.Logging;

namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// One process-wide bound on started occurrences, shared by overlapping polls. This owns
/// no schedule or durable queue. Work is awaited by its caller and by shutdown; cancelling
/// never disposes its execution scope or resource lease while that work is still running.
/// </summary>
public sealed class BoundedJobDispatcher : IAsyncDisposable
{
    private readonly Dictionary<string, Task> _inFlight = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ILogger<BoundedJobDispatcher> _logger;
    private readonly int _maxOutstanding;
    private bool _disposed;
    private Task? _disposal;
    public BoundedJobDispatcher(int maxOutstanding, ILogger<BoundedJobDispatcher> logger)
    {
        if (maxOutstanding < 1 || maxOutstanding > 4096)
            throw new ArgumentOutOfRangeException(nameof(maxOutstanding));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxOutstanding = maxOutstanding;
    }
    public int Outstanding { get { lock (_gate) return _inFlight.Count; } }
    public int MaxOutstanding => _maxOutstanding;
    public string EnforcementScope { get; set; } = "in-process";
    public int RejectedForBound { get; private set; }
    public int SuppressedDuplicates { get; private set; }
    public int ObservedFaults { get; private set; }
    public int Completed { get; private set; }
    public int OutstandingAtShutdown { get; private set; }
    public bool AcceptingWork { get; private set; } = true;

    public bool TryDispatch(string identity, Func<CancellationToken, Task> work)
        => TryDispatch(identity, work, CancellationToken.None, out _);

    public bool TryDispatch(string identity, Func<CancellationToken, Task> work,
        CancellationToken cancellationToken, out Task completion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentNullException.ThrowIfNull(work);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        completion = Task.CompletedTask;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!AcceptingWork || cancellationToken.IsCancellationRequested) return false;
            if (_inFlight.ContainsKey(identity)) { SuppressedDuplicates++; return false; }
            if (_inFlight.Count >= _maxOutstanding) { RejectedForBound++; return false; }
            _inFlight.Add(identity, finished.Task);
        }
        // RunAsync observes every fault and completes the task already in _inFlight.
        // Register before invocation, including synchronous completion/throw paths.
        completion = RunAsync(identity, work, cancellationToken, finished);
        return true;
    }

    private async Task RunAsync(string identity, Func<CancellationToken, Task> work,
        CancellationToken pollCancellation, TaskCompletionSource finished)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(pollCancellation, _shutdown.Token);
        try
        {
            await work(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            _logger.LogInformation("Occurrence {Identity} observed cancellation.", identity);
        }
        catch (Exception ex)
        {
            lock (_gate) ObservedFaults++;
            _logger.LogError(ex, "Occurrence {Identity} failed.", identity);
        }
        finally
        {
            lock (_gate)
            {
                _inFlight.Remove(identity);
                Completed++;
                finished.SetResult();
            }
        }
    }

    /// <summary>
    /// Grace before requesting cancellation, NOT a completion deadline. This runtime uses
    /// cooperative shutdown: after grace it keeps awaiting owners. Non-cooperative work
    /// remains outstanding with its scope and lease held until it actually exits.
    /// </summary>
    public async Task DrainAsync(TimeSpan grace)
    {
        if (grace < TimeSpan.Zero || grace.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(grace));
        Task[] pending;
        lock (_gate) { AcceptingWork = false; pending = _inFlight.Values.ToArray(); }
        var all = Task.WhenAll(pending);
        if (await Task.WhenAny(all, Task.Delay(grace)).ConfigureAwait(false) != all)
        {
            lock (_gate) OutstandingAtShutdown = _inFlight.Count;
            _logger.LogWarning("Cancellation requested; still supervising {Count} occurrences.", OutstandingAtShutdown);
            _shutdown.Cancel();
        }
        await all.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposal is not null) return new ValueTask(_disposal);
            _disposed = true;
            _disposal = DisposeCoreAsync();
            return new ValueTask(_disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await DrainAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
