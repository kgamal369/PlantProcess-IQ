namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// RUNTIME AND EXECUTOR FAILURE, AND NOTHING ELSE.
///
/// Three vocabularies meet at the executor and none of them may absorb another:
///
///   PROJECTION_*  a declaration or admission defect in the authored Transformation
///   PV01..PV15    a row or data validation outcome owned by the quarantine producer
///   JOB_EXEC_*    the executor, its compilation, its query, its write, its cancellation
///
/// A database outage is not bad customer data and never becomes a PV code. An unknown
/// target field is a declaration defect and never becomes a JOB_EXEC code. Every code
/// below is exercised by the implementation and its tests; there is no speculative
/// catalogue here, because a code nothing can produce is a promise about behaviour
/// that does not exist.
/// </summary>
public static class JobExecutionDiagnosticCodes
{
    public const string AdmissionUnknownLane = "JOB_EXEC_ADMISSION_UNKNOWN_LANE";
    public const string AdmissionInvalidRequest = "JOB_EXEC_ADMISSION_INVALID_REQUEST";
    public const string AdmissionLaneNotPermitted = "JOB_EXEC_ADMISSION_LANE_NOT_PERMITTED";
    public const string AdmissionDemandExceedsLaneCapacity = "JOB_EXEC_ADMISSION_DEMAND_EXCEEDS_LANE_CAPACITY";
    public const string AdmissionQueueFull = "JOB_EXEC_ADMISSION_QUEUE_FULL";
    public const string AdmissionCancelled = "JOB_EXEC_ADMISSION_CANCELLED";
    public const string AdmissionWaitExpired = "JOB_EXEC_ADMISSION_WAIT_EXPIRED";

    /// <summary>Capability admitted the family and no executor resolved for it.</summary>
    public const string ExecutorMissing = "JOB_EXEC_EXECUTOR_MISSING";

    /// <summary>No exact immutable definition version can be named for the run.</summary>
    public const string ExactVersionRequired = "JOB_EXEC_EXACT_VERSION_REQUIRED";

    /// <summary>The governed compiler refused or failed to produce a statement.</summary>
    public const string CompilationFailed = "JOB_EXEC_COMPILATION_FAILED";

    /// <summary>The version carries no authored block identity to record evidence against.</summary>
    public const string BlocksUnavailable = "JOB_EXEC_BLOCKS_UNAVAILABLE";

    /// <summary>No input relation exposes the governed provenance contract.</summary>
    public const string SourceIdentityUnavailable = "JOB_EXEC_SOURCE_IDENTITY_UNAVAILABLE";

    /// <summary>Source identity cannot be resolved to exactly one output row.</summary>
    public const string SourceIdentityAmbiguous = "JOB_EXEC_SOURCE_IDENTITY_AMBIGUOUS";

    /// <summary>A row requiring canonical persistence carries a null or blank identity.</summary>
    public const string SourceIdentityInvalid = "JOB_EXEC_SOURCE_IDENTITY_INVALID";

    /// <summary>The runtime holds no proven write and idempotency path for this target.</summary>
    public const string CanonicalTargetNotCommissioned = "JOB_EXEC_CANONICAL_TARGET_NOT_COMMISSIONED";

    /// <summary>A governed source identity already holds a different canonical effect.</summary>
    public const string CanonicalIdentityConflict = "JOB_EXEC_CANONICAL_IDENTITY_CONFLICT";

    /// <summary>
    /// The job's target parameters are outside the canonical refresh vocabulary, so no
    /// projection authority can be read from them.
    /// </summary>
    public const string ProjectionParametersInvalid = "JOB_EXEC_PROJECTION_PARAMETERS_INVALID";

    /// <summary>
    /// A run would replace a canonical effect recorded by a newer projection generation,
    /// or lost a concurrent race for the same identity. Nothing was written.
    /// </summary>
    public const string ProjectionEffectStale = "JOB_EXEC_PROJECTION_EFFECT_STALE";

    /// <summary>The compiled statement failed at the database.</summary>
    public const string QueryFailed = "JOB_EXEC_QUERY_FAILED";

    /// <summary>The canonical write failed after a statement produced rows.</summary>
    public const string CanonicalWriteFailed = "JOB_EXEC_CANONICAL_WRITE_FAILED";

    /// <summary>One executable block failed; dependents did not execute.</summary>
    public const string BlockFailed = "JOB_EXEC_BLOCK_FAILED";

    /// <summary>Execution stopped because the run was cancelled.</summary>
    public const string Cancelled = "JOB_EXEC_CANCELLED";
}
