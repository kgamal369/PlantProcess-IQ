using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// T-106. THE FIVE RESOLUTIONS CHAPTER 5.3.6 DECLARES, AND NO SIXTH.
/// </summary>
public enum JobDependencyResolution
{
    /// <summary>Upstream succeeded within tolerance.</summary>
    Satisfied = 1,

    /// <summary>
    /// Upstream succeeded but is older than tolerance and the dependency permits
    /// it. REPRESENTABLE AND NOT PRODUCED: the declared edge contract exposes a
    /// tolerance but no permission, so nothing in this runtime may claim it.
    /// </summary>
    StaleAccepted = 2,

    /// <summary>Upstream failed, never ran, or resolved to the wrong version.</summary>
    Blocked = 3,

    /// <summary>An optional dependency was unsatisfied and the child ran without it.</summary>
    SkippedOptional = 4,

    /// <summary>Upstream failed this cycle.</summary>
    FailedUpstream = 5
}

/// <summary>
/// T-106. RUN-LEVEL DEPENDENCY EVIDENCE, RECONSTRUCTABLE FROM THE DATABASE.
///
/// RunId is always a genuine job_run_histories identity, including for a child
/// that never computed - that run exists and is terminal Blocked. Nothing here
/// is ever a correlation id, a job definition id or a fabricated GUID.
///
/// DependsOnRunId is NULLABLE, and only for the one state the design describes
/// where no upstream run exists at all: a required upstream that has never run.
/// Manufacturing an upstream identity to fill the column would make the evidence
/// a lie about what happened.
/// </summary>
public class JobRunDependency : BaseEntity
{
    public Guid RunId { get; private set; }

    public Guid? DependsOnRunId { get; private set; }

    public Guid JobDefinitionId { get; private set; }

    public Guid DependsOnJobDefinitionId { get; private set; }

    public JobDependencyResolution Resolution { get; private set; }

    public DateTime ResolvedAtUtc { get; private set; }

    /// <summary>The version the edge pinned, when it pinned one.</summary>
    public int? ExpectedVersion { get; private set; }

    /// <summary>The version the upstream actually resolved to, when it ran.</summary>
    public int? ActualVersion { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>Chapter 5.3.6 watermark propagation. Not produced by this task.</summary>
    public string? WatermarkInherited { get; private set; }

    private JobRunDependency()
    {
    }

    public JobRunDependency(
        Guid runId,
        Guid? dependsOnRunId,
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        JobDependencyResolution resolution,
        int? expectedVersion,
        int? actualVersion,
        string? reason)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run dependency evidence must reference a real downstream run identity.", nameof(runId));
        }

        if (dependsOnRunId.HasValue && dependsOnRunId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An upstream run identity is either real or absent; it is never empty.", nameof(dependsOnRunId));
        }

        if (resolution == JobDependencyResolution.StaleAccepted)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution), resolution,
                "stale_accepted is representable in the schema and is not produced by this runtime: "
                    + "the declared edge contract exposes a staleness tolerance but no authority that "
                    + "permits accepting a stale upstream.");
        }

        RunId = runId;
        DependsOnRunId = dependsOnRunId;
        JobDefinitionId = jobDefinitionId;
        DependsOnJobDefinitionId = dependsOnJobDefinitionId;
        Resolution = resolution;
        ResolvedAtUtc = DateTime.UtcNow;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}