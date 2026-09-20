using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Canvas;

/// <summary>
/// WHAT THE RUNTIME CAN SAY BEFORE ANYTHING RUNS.
///
/// Three different claims live here and none of them may absorb another:
///
///   ExecutorRegistered   a runtime exists for this job family at all
///   StaticallyEligible   this exact version reads, compiles, names a lawful
///                        source identity and writes a commissioned target
///   nothing about later  capacity, authorization at launch time, or the state
///                        of the database a minute from now
///
/// A true answer here is NOT a promise that a launch will be admitted. It means
/// the failures that can be known before dispatch are known now, so a person is
/// not told "Run" by a surface that has no runtime behind it.
/// </summary>
public sealed record CanvasExecutionCapabilityDto(
    string DefinitionCode,
    Guid DefinitionId,
    string TargetKind,
    int? ResolvedVersion,
    string VersionPolicy,
    bool FamilyIsExecutable,
    bool ExecutorRegistered,
    bool StaticallyEligible,
    string? RefusalCode,
    string? RefusalDetail,
    string Assurance);

/// <summary>What an author asks for when binding a Canvas definition to a job.</summary>
public sealed record CanvasJobBindingRequest(
    string DefinitionCode,
    int? PinnedVersion,
    string? JobName);

public sealed record CanvasJobBindingDto(
    Guid JobDefinitionId,
    string JobCode,
    string JobName,
    Guid DefinitionId,
    string DefinitionCode,
    string TargetKind,
    string VersionPolicy,
    int? PinnedVersion,
    bool Created);

/// <summary>One authored block of one real run, exactly as it was persisted.</summary>
public sealed record CanvasRunBlockDto(
    string BlockId,
    int ExecutionOrdinal,
    string Status,
    int? InputRows,
    int? OutputRows,
    string? DiagnosticCode,
    string? DiagnosticDetail,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc);

/// <summary>
/// THE RUN, AS THE CANONICAL AUTHORITY HOLDS IT.
///
/// Every field is read from the run record and the persisted block evidence. Nothing
/// is recomputed from the Canvas the browser currently shows: an author who edits a
/// board after launching must still see what the launched version did.
///
/// CancellationRequested is not a result. It stays requested until the run authority
/// records the acknowledgement and a terminal status.
/// </summary>
public sealed record CanvasRunEvidenceDto(
    Guid RunId,
    Guid JobDefinitionId,
    string JobCode,
    string Status,
    bool IsTerminal,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? CorrelationId,
    Guid? TargetDefinitionId,
    int? TargetDefinitionVersion,
    string? TargetDefinitionKind,
    string? TargetVersionPolicy,
    bool CancellationRequested,
    DateTime? CancellationRequestedAtUtc,
    DateTime? CancellationAcknowledgedAtUtc,
    string? FailureReason,
    string? RunMessage,
    IReadOnlyList<CanvasRunBlockDto> Blocks);

/// <summary>
/// THE CANVAS CONSUMER OF THE GOVERNED JOB MODEL.
///
/// It compiles nothing of its own, schedules nothing of its own and stores no run
/// history of its own. It resolves identity on the server, asks the existing
/// authorities, and reads back what they recorded.
/// </summary>
public interface ICanvasJobBindingService
{
    Task<ApplicationResult<CanvasExecutionCapabilityDto>> DescribeExecutionAsync(
        Guid tenantId, string definitionCode, int? pinnedVersion, CancellationToken cancellationToken);

    Task<ApplicationResult<CanvasJobBindingDto>> BindAsync(
        Guid tenantId, CanvasJobBindingRequest request, CancellationToken cancellationToken);

    /// <summary>The bound job for a definition this tenant owns, or a typed refusal.</summary>
    Task<ApplicationResult<CanvasJobBindingDto>> FindBindingAsync(
        Guid tenantId, string definitionCode, CancellationToken cancellationToken);

    /// <summary>
    /// The run this launch created, found by the correlation the caller generated.
    /// Never "the latest run": two people launching the same job at once must each
    /// attach to their own.
    /// </summary>
    Task<ApplicationResult<CanvasRunEvidenceDto?>> FindRunByCorrelationAsync(
        Guid tenantId, string definitionCode, string correlationId, CancellationToken cancellationToken);

    Task<ApplicationResult<CanvasRunEvidenceDto>> ReadRunAsync(
        Guid tenantId, string definitionCode, Guid runId, CancellationToken cancellationToken);
}

/// <summary>The job bound to a definition, as the job authority holds it.</summary>
public sealed record CanvasBoundJob(
    Guid JobDefinitionId,
    string JobCode,
    string JobName,
    string? TargetDefinitionKind,
    Guid? TargetDefinitionId,
    int? TargetDefinitionVersion,
    JobTargetVersionPolicy? VersionPolicy);

/// <summary>One run, as the run authority holds it. Nothing here is derived.</summary>
public sealed record CanvasRunRecord(
    Guid RunId,
    Guid JobDefinitionId,
    string JobCode,
    JobRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? CorrelationId,
    Guid? TargetDefinitionId,
    int? TargetDefinitionVersion,
    string? TargetDefinitionKind,
    JobTargetVersionPolicy? TargetVersionPolicy,
    DateTime? CancellationRequestedAtUtc,
    DateTime? CancellationAcknowledgedAtUtc,
    string? FailureReason,
    string? RunMessage);

/// <summary>
/// THE READ SIDE, AND ONLY THE READ SIDE.
///
/// A narrow port over the job and run authorities so this consumer never holds a
/// database context of its own and never grows a write path by accident.
/// </summary>
public interface ICanvasJobReadModel
{
    Task<CanvasBoundJob?> FindBoundJobAsync(Guid targetDefinitionId, JobDefinitionType family, CancellationToken cancellationToken);

    Task<CanvasRunRecord?> FindRunAsync(Guid runId, CancellationToken cancellationToken);

    Task<CanvasRunRecord?> FindRunByCorrelationAsync(Guid jobDefinitionId, string correlationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CanvasRunBlockDto>> ListBlocksAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>What the job authority did: the job itself, and whether it was created now.</summary>
public sealed record GovernedJobBindingResult(JobDefinitionDto Job, bool Created);

/// <summary>The governed binding the job authority is asked to persist.</summary>
public sealed record GovernedJobBindingRequest(
    string JobCode,
    string JobName,
    JobDefinitionType JobType,
    string TargetDefinitionKind,
    Guid TargetDefinitionId,
    JobTargetVersionPolicy VersionPolicy,
    int? PinnedVersion,
    string? ParametersJson);
