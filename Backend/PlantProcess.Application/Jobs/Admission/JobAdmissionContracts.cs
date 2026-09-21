namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// The baseline execution lanes. A lane is a capacity pool, never a task name and never a
/// protocol. Families reuse a lane; they do not invent one. A later owning task may add one
/// governed lane through the lane catalogue, never through a pool of its own.
/// </summary>
public static class JobLaneCodes
{
    public const string Import = "import";
    public const string Projection = "projection";
    public const string Analysis = "analysis";
    public const string MlTraining = "ml.training";
    public const string MlBatchScoring = "ml.batch_scoring";
    public const string MlOnlineScoring = "ml.online_scoring";
    public const string Report = "report";

    /// <summary>The lanes the current design declares. The catalogue may hold more.</summary>
    public static readonly IReadOnlyList<string> Baseline = new[]
    {
        Import,
        Projection,
        Analysis,
        MlTraining,
        MlBatchScoring,
        MlOnlineScoring,
        Report
    };

    public static bool IsBaseline(string laneCode) => Baseline.Contains(laneCode, StringComparer.Ordinal);
}

/// <summary>Where a lane configuration came from. A test capacity is never a site limit.</summary>
public enum JobCapacityProvenance
{
    /// <summary>A capacity chosen by an acceptance test. It certifies behaviour, never sizing.</summary>
    TestConfiguration,

    /// <summary>A shipped default with no site benchmark behind it.</summary>
    UnqualifiedDefault,

    /// <summary>A capacity a site benchmark produced. Owned by the calibration tasks, not here.</summary>
    SiteCalibrated
}

/// <summary>
/// One lane and its two independent bounds: how many runs may be in flight, and how much
/// resource cost may be in flight. They are never collapsed into one number.
/// </summary>
public sealed record JobLaneDefinition(
    string LaneCode,
    int MaxConcurrency,
    double ResourceCapacity,
    int MaxQueueDepth,
    JobCapacityProvenance Provenance,
    bool IsHardReserved = false,
    bool IsPreemptible = false)
{
    /// <summary>A queue depth of zero is lawful: that lane admits now or refuses now.</summary>
    public bool AllowsWaiting => MaxQueueDepth > 0;

    public IReadOnlyList<string> Validate(JobLaneCatalogue? catalogue = null)
    {
        var errors = new List<string>();
        JobLaneCatalogue lanes = catalogue ?? JobLaneCatalogue.Shared;

        if (!lanes.IsRegistered(LaneCode))
        {
            errors.Add("LaneCode '" + LaneCode + "' is not registered in the job lane catalogue.");
        }

        if (MaxConcurrency < 1)
        {
            errors.Add("MaxConcurrency must be at least 1.");
        }

        if (!double.IsFinite(ResourceCapacity) || ResourceCapacity <= 0)
        {
            errors.Add("ResourceCapacity must be greater than zero.");
        }

        if (MaxQueueDepth < 0)
        {
            errors.Add("MaxQueueDepth must not be negative. Zero means no waiting, which is lawful.");
        }

        return errors;
    }
}

/// <summary>The resource cost of one candidate run. Cost is not a count.</summary>
public sealed record JobResourceDemand(double ComputeWeight)
{
    public bool IsValid => ComputeWeight > 0 && double.IsFinite(ComputeWeight);
}

/// <summary>
/// A request for capacity. It arrives only after the job is known executable: capability,
/// target version and executor resolution all happen before admission is asked anything.
/// </summary>
public sealed record JobAdmissionRequest(
    Guid JobDefinitionId,
    string JobDefinitionType,
    string LaneCode,
    JobResourceDemand Demand,
    string? CorrelationId = null);

/// <summary>Typed admission outcomes. A refusal is never an exception and never a failed run.</summary>
public enum JobAdmissionOutcome
{
    Admitted,
    RefusedUnknownLane,
    RefusedInvalidRequest,
    RefusedLaneNotPermitted,
    RefusedDemandExceedsLaneCapacity,
    RefusedQueueFull,
    Cancelled,
    WaitExpired
}


/// <summary>Where lane configuration comes from. Admission never authors its own capacities.</summary>
public interface IJobAdmissionConfigurationProvider
{
    IReadOnlyList<JobLaneDefinition> GetLanes();
}

/// <summary>
/// Resource admission for jobs that are already known to be executable.
///
/// It answers one question: may this valid job consume capacity now? It owns no scheduler,
/// no run history, no capability authority and no executor.
/// </summary>
public interface IJobAdmissionController
{
    /// <summary>
    /// Waits for capacity inside the lane's bounds and returns a typed decision. The caller
    /// disposes the decision; disposal releases any reservation exactly once.
    ///
    /// The reservation spans the whole in-flight execution: admission, run creation,
    /// execution and the terminal persistence that belongs to the same execution path. It is
    /// released in a finally, never at the moment an executor method returns. If run creation
    /// fails after admission, the reservation is released at once and no phantom capacity
    /// stays held.
    /// </summary>
    Task<JobAdmissionDecision> AcquireAsync(JobAdmissionRequest request, CancellationToken cancellationToken);

    /// <summary>A snapshot of lane occupancy, for evidence and for operator surfaces.</summary>
    IReadOnlyList<JobLaneOccupancy> Snapshot();
}

/// <summary>Live occupancy of one lane.</summary>
public sealed record JobLaneOccupancy(
    string LaneCode,
    int RunningCount,
    int MaxConcurrency,
    double ActiveWeight,
    double ResourceCapacity,
    int QueueDepth,
    int MaxQueueDepth);
