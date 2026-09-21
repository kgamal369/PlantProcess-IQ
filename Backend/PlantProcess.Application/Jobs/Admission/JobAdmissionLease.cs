namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// A reservation held while one job run executes. Releasing is idempotent: the normal path,
/// the exception path and the cancellation path all release the same reservation once.
/// No caller ever adjusts a capacity counter itself.
/// </summary>
public sealed class JobAdmissionLease : IAsyncDisposable
{
    private readonly Func<JobAdmissionLease, ValueTask> _release;
    private int _released;

    internal JobAdmissionLease(
        string laneCode,
        double computeWeight,
        Guid jobDefinitionId,
        string? correlationId,
        Func<JobAdmissionLease, ValueTask> release)
    {
        LaneCode = laneCode;
        ComputeWeight = computeWeight;
        JobDefinitionId = jobDefinitionId;
        CorrelationId = correlationId;
        LeaseId = Guid.NewGuid();
        AcquiredAtUtc = DateTimeOffset.UtcNow;
        _release = release;
    }

    public Guid LeaseId { get; }

    public string LaneCode { get; }

    public double ComputeWeight { get; }

    public Guid JobDefinitionId { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset AcquiredAtUtc { get; }

    public bool IsReleased => Volatile.Read(ref _released) != 0;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        return _release(this);
    }
}

/// <summary>
/// The typed result of one admission attempt. Disposing it releases the reservation when
/// one was granted and does nothing when it was not, so every call site can use the same
/// await using shape.
/// </summary>
public sealed class JobAdmissionDecision : IAsyncDisposable
{
    private JobAdmissionDecision(
        JobAdmissionOutcome outcome,
        JobAdmissionLease? lease,
        string? diagnosticCode,
        string detail,
        TimeSpan queueWait)
    {
        Outcome = outcome;
        Lease = lease;
        DiagnosticCode = diagnosticCode;
        Detail = detail;
        QueueWait = queueWait;
    }

    public JobAdmissionOutcome Outcome { get; }

    public JobAdmissionLease? Lease { get; }

    public string? DiagnosticCode { get; }

    public string Detail { get; }

    public TimeSpan QueueWait { get; }

    public bool IsAdmitted => Outcome == JobAdmissionOutcome.Admitted;

    internal static JobAdmissionDecision Admit(JobAdmissionLease lease, TimeSpan queueWait)
        => new(
            JobAdmissionOutcome.Admitted,
            lease,
            null,
            "Capacity reserved in lane " + lease.LaneCode + ".",
            queueWait);

    internal static JobAdmissionDecision Refuse(
        JobAdmissionOutcome outcome,
        string diagnosticCode,
        string detail,
        TimeSpan queueWait = default)
        => new(outcome, null, diagnosticCode, detail, queueWait);

    public ValueTask DisposeAsync()
        => Lease is null ? ValueTask.CompletedTask : Lease.DisposeAsync();
}
