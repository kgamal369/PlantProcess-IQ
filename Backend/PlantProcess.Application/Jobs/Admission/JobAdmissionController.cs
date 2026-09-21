using Microsoft.Extensions.Logging;

namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// Bounded resource admission with a dual predicate, per lane:
///
///   running_count &lt; max_concurrency
///   AND active_weight + candidate_weight &lt;= resource_capacity
///
/// Both must hold. A compute weight is a resource cost and is never counted as a run.
///
/// Waiting is FIFO inside a lane and bounded by the lane queue depth. A waiting job holds
/// no capacity and produces no run; a refused job produces a typed diagnostic, not an
/// exception and not a failed run. Every granted reservation is released exactly once by
/// the lease, on the normal, exception and cancellation paths alike.
/// </summary>
public sealed class JobAdmissionController : IJobAdmissionController
{
    private readonly Dictionary<string, LaneState> _lanes = new(StringComparer.Ordinal);
    private readonly ILogger<JobAdmissionController> _logger;
    private readonly object _gate = new();
    private readonly TimeSpan _maxWait;

    /// <summary>
    /// Capacity here governs this one admission instance. It is not a cluster-wide or
    /// site-wide guarantee, and diagnostics carry the scope so no reader assumes one.
    /// </summary>
    public string AdmissionScope { get; }

    public JobAdmissionController(
        IJobAdmissionConfigurationProvider configuration,
        ILogger<JobAdmissionController> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _maxWait = configuration is JobAdmissionOptionsConfigurationProvider configured
            ? configured.AdmissionWait : TimeSpan.FromSeconds(30);
        AdmissionScope = configuration is JobAdmissionOptionsConfigurationProvider options
            ? options.AdmissionScope
            : "in-process";

        foreach (JobLaneDefinition lane in configuration.GetLanes())
        {
            IReadOnlyList<string> errors = lane.Validate();
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    "Lane '" + lane.LaneCode + "' is not configurable: " + string.Join(" ", errors),
                    nameof(configuration));
            }

            if (_lanes.ContainsKey(lane.LaneCode))
            {
                throw new ArgumentException("Lane '" + lane.LaneCode + "' is configured twice.", nameof(configuration));
            }

            _lanes[lane.LaneCode] = new LaneState(lane);
        }
    }

    public async Task<JobAdmissionDecision> AcquireAsync(
        JobAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Demand is null || !request.Demand.IsValid || string.IsNullOrWhiteSpace(request.JobDefinitionType))
        {
            return JobAdmissionDecision.Refuse(
                JobAdmissionOutcome.RefusedInvalidRequest,
                PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionInvalidRequest,
                "A candidate needs a job definition type and a positive finite compute weight.");
        }

        if (string.IsNullOrWhiteSpace(request.LaneCode))
            return JobAdmissionDecision.Refuse(JobAdmissionOutcome.RefusedUnknownLane,
                PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionUnknownLane,
                "A persisted configured lane is required.");

        LaneState? lane;
        lock (_gate)
        {
            _lanes.TryGetValue(request.LaneCode, out lane);
        }

        if (lane is null)
        {
            return JobAdmissionDecision.Refuse(
                JobAdmissionOutcome.RefusedUnknownLane,
                PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionUnknownLane,
                "Lane '" + request.LaneCode + "' is not configured for this deployment.");
        }

        if (!JobLaneAssignment.IsPermitted(request.JobDefinitionType, request.LaneCode))
        {
            return JobAdmissionDecision.Refuse(
                JobAdmissionOutcome.RefusedLaneNotPermitted,
                PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionLaneNotPermitted,
                "Job family '" + request.JobDefinitionType + "' may not consume lane '" + request.LaneCode + "'.");
        }

        if (request.Demand.ComputeWeight > lane.Definition.ResourceCapacity)
        {
            return JobAdmissionDecision.Refuse(
                JobAdmissionOutcome.RefusedDemandExceedsLaneCapacity,
                PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionDemandExceedsLaneCapacity,
                "A compute weight of " + request.Demand.ComputeWeight + " can never fit lane capacity " +
                lane.Definition.ResourceCapacity + ". Waiting would never end, so the candidate is refused now.");
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waitCancellation.CancelAfter(_maxWait);
        Waiter waiter;
        DateTimeOffset queuedAt = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            if (waitCancellation.IsCancellationRequested)
                return JobAdmissionDecision.Refuse(
                    cancellationToken.IsCancellationRequested ? JobAdmissionOutcome.Cancelled : JobAdmissionOutcome.WaitExpired,
                    cancellationToken.IsCancellationRequested
                        ? PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionCancelled
                        : PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionWaitExpired,
                    "The candidate stopped waiting before capacity was reserved.");
            if (lane.Waiters.Count == 0 && lane.CanAdmit(request.Demand.ComputeWeight))
            {
                JobAdmissionLease granted = lane.Reserve(request, ReleaseAsync);
                return JobAdmissionDecision.Admit(granted, TimeSpan.Zero);
            }

            if (lane.Waiters.Count >= lane.Definition.MaxQueueDepth)
            {
                return JobAdmissionDecision.Refuse(
                    JobAdmissionOutcome.RefusedQueueFull,
                    PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionQueueFull,
                    "Lane '" + lane.Definition.LaneCode + "' is saturated and its queue bound of " +
                    lane.Definition.MaxQueueDepth + " is reached.");
            }

            waiter = new Waiter(request, waitCancellation.Token);
            lane.Waiters.AddLast(waiter.Node);
        }

        using CancellationTokenRegistration registration = waitCancellation.Token.Register(
            state => CancelWaiter(lane, (Waiter)state!),
            waiter);

        JobAdmissionLease? lease = await waiter.Completion.Task.ConfigureAwait(false);
        TimeSpan waited = DateTimeOffset.UtcNow - queuedAt;

        if (lease is null)
        {
            return JobAdmissionDecision.Refuse(
                cancellationToken.IsCancellationRequested ? JobAdmissionOutcome.Cancelled : JobAdmissionOutcome.WaitExpired,
                cancellationToken.IsCancellationRequested
                    ? PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionCancelled
                    : PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionWaitExpired,
                "The candidate was cancelled while queued. No capacity was consumed and no run was created.",
                waited);
        }

        return JobAdmissionDecision.Admit(lease, waited);
    }

    public IReadOnlyList<JobLaneOccupancy> Snapshot()
    {
        lock (_gate)
        {
            return _lanes.Values
                .Select(lane => new JobLaneOccupancy(
                    lane.Definition.LaneCode,
                    lane.RunningCount,
                    lane.Definition.MaxConcurrency,
                    lane.ActiveWeight,
                    lane.Definition.ResourceCapacity,
                    lane.Waiters.Count,
                    lane.Definition.MaxQueueDepth))
                .OrderBy(occupancy => occupancy.LaneCode, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private void CancelWaiter(LaneState lane, Waiter waiter)
    {
        List<(Waiter Waiter, JobAdmissionLease? Lease)> ready;
        lock (_gate)
        {
            if (waiter.State != WaiterState.Queued) return;
            waiter.State = WaiterState.Cancelled;
            lane.Waiters.Remove(waiter.Node);
            ready = new() { (waiter, null) };
            Drain(lane, ready);
        }
        CompleteWaiters(ready);
    }

    private ValueTask ReleaseAsync(JobAdmissionLease lease)
    {
        var ready = new List<(Waiter Waiter, JobAdmissionLease? Lease)>();
        lock (_gate)
        {
            var lane = _lanes[lease.LaneCode];
            lane.Release(lease);
            Drain(lane, ready);
        }
        CompleteWaiters(ready);
        _logger.LogDebug("Released {LeaseId} in lane {LaneCode}", lease.LeaseId, lease.LaneCode);
        return ValueTask.CompletedTask;
    }

    // Only called under _gate. Cancellation removes a blocked head and immediately
    // considers its successor; a lighter live waiter never overtakes a live FIFO head.
    private void Drain(LaneState lane, List<(Waiter Waiter, JobAdmissionLease? Lease)> ready)
    {
        while (lane.Waiters.First is { Value: Waiter head })
        {
            if (head.Cancellation.IsCancellationRequested)
            {
                lane.Waiters.RemoveFirst();
                head.State = WaiterState.Cancelled;
                ready.Add((head, null));
                continue;
            }
            if (!lane.CanAdmit(head.Request.Demand.ComputeWeight)) break;
            lane.Waiters.RemoveFirst();
            head.State = WaiterState.Admitted;
            ready.Add((head, lane.Reserve(head.Request, ReleaseAsync)));
        }
    }

    private static void CompleteWaiters(List<(Waiter Waiter, JobAdmissionLease? Lease)> ready)
    {
        foreach (var (waiter, lease) in ready) waiter.Completion.SetResult(lease);
    }

    private sealed class LaneState
    {
        internal LaneState(JobLaneDefinition definition) => Definition = definition;

        internal JobLaneDefinition Definition { get; }

        internal int RunningCount { get; private set; }

        internal double ActiveWeight { get; private set; }

        internal LinkedList<Waiter> Waiters { get; } = new();

        internal bool CanAdmit(double candidateWeight)
            => RunningCount < Definition.MaxConcurrency &&
               ActiveWeight + candidateWeight <= Definition.ResourceCapacity;

        internal JobAdmissionLease Reserve(JobAdmissionRequest request, Func<JobAdmissionLease, ValueTask> release)
        {
            RunningCount++;
            ActiveWeight += request.Demand.ComputeWeight;

            return new JobAdmissionLease(
                Definition.LaneCode,
                request.Demand.ComputeWeight,
                request.JobDefinitionId,
                request.CorrelationId,
                release);
        }

        internal void Release(JobAdmissionLease lease)
        {
            if (RunningCount <= 0) throw new InvalidOperationException("Admission lease released without occupancy.");
            RunningCount--;
            ActiveWeight = RunningCount == 0 ? 0 : Math.Max(0, ActiveWeight - lease.ComputeWeight);
        }
    }

    /// <summary>
    /// The state of one queued candidate. Exactly one transition out of Queued wins, and it
    /// happens under the lane lock that also guards the counters and the queue.
    /// </summary>
    private enum WaiterState
    {
        Queued,
        Admitted,
        Cancelled
    }

    private sealed class Waiter
    {
        internal Waiter(JobAdmissionRequest request, CancellationToken cancellation)
        {
            Request = request;
            Cancellation = cancellation;
            Node = new LinkedListNode<Waiter>(this);
            Completion = new TaskCompletionSource<JobAdmissionLease?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal JobAdmissionRequest Request { get; }
        internal CancellationToken Cancellation { get; }

        internal LinkedListNode<Waiter> Node { get; }

        internal TaskCompletionSource<JobAdmissionLease?> Completion { get; }

        /// <summary>Only ever read or written under the lane lock.</summary>
        internal WaiterState State { get; set; } = WaiterState.Queued;
    }
}
