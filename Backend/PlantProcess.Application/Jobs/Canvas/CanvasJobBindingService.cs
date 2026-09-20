using System.Text;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Canvas;

/// <summary>
/// THE CANVAS SIDE OF THE GOVERNED JOB MODEL, AND NOTHING MORE.
///
/// IDENTITY IS RESOLVED ON THE SERVER. A definition code arrives from a browser and is
/// resolved against the caller's tenant. A code that tenant does not own is a refusal,
/// not a lookup of someone else's definition, and no job id, definition id or run id
/// supplied by a client is ever treated as authority on its own.
///
/// EVERY RUN FACT IS READ BACK. The service never reconstructs an outcome from the
/// Canvas the author currently has open. A missing block is missing, not successful.
/// </summary>
public sealed class CanvasJobBindingService : ICanvasJobBindingService
{
    public const string JobCodePrefix = "CANVAS_";

    private readonly ICanonicalDefinitionWriter _definitions;
    private readonly IJobDefinitionService _jobs;
    private readonly IJobExecutionCapabilityAuthority _capability;
    private readonly IJobExecutorResolver _executors;
    private readonly ICanvasJobReadModel _readModel;

    public CanvasJobBindingService(
        ICanonicalDefinitionWriter definitions,
        IJobDefinitionService jobs,
        IJobExecutionCapabilityAuthority capability,
        IJobExecutorResolver executors,
        ICanvasJobReadModel readModel)
    {
        _definitions = definitions;
        _jobs = jobs;
        _capability = capability;
        _executors = executors;
        _readModel = readModel;
    }

    // The Canvas writes canonical rows, which is the CanonicalRefresh family, and the
    // target it declares is a Transformation definition. Both come from the existing
    // vocabularies; neither is a new concept of this service.
    private const JobDefinitionType Family = JobDefinitionType.CanonicalRefresh;

    // ------------------------------------------------------------- capability --

    public async Task<ApplicationResult<CanvasExecutionCapabilityDto>> DescribeExecutionAsync(
        Guid tenantId,
        string definitionCode,
        int? pinnedVersion,
        CancellationToken cancellationToken)
    {
        ApplicationResult<Guid> owned = await ResolveOwnedDefinitionAsync(tenantId, definitionCode, cancellationToken);
        if (owned.IsFailure)
        {
            return ApplicationResult<CanvasExecutionCapabilityDto>.Failure(owned.Error!);
        }

        Guid definitionId = owned.Value;
        JobTargetVersionPolicy policy = pinnedVersion.HasValue
            ? JobTargetVersionPolicy.Pinned
            : JobTargetVersionPolicy.CurrentPublished;

        ApplicationResult<CanonicalDefinitionVersion> version =
            await ResolveVersionAsync(definitionId, pinnedVersion, cancellationToken);

        JobExecutionCapability family = _capability.Describe(Family);

        if (version.IsFailure || version.Value is null)
        {
            return Capability(definitionCode, definitionId, null, policy, family, false, false,
                version.Error?.Code ?? "CANVAS_VERSION_UNAVAILABLE",
                version.Error?.Message ?? "No published version of this definition could be resolved.");
        }

        IJobExecutor? executor = _executors.Resolve(Family);

        if (!family.IsExecutableByRuntime || executor is null)
        {
            return Capability(definitionCode, definitionId, version.Value.VersionNumber, policy, family,
                executor is not null, false,
                JobExecutionDiagnosticCodes.ExecutorMissing,
                "This runtime has no executor registered for " + Family + ", so a launch would be refused before dispatch.");
        }

        // PURE ELIGIBILITY. AdmitAsync resolves the exact version, compiles it through the
        // shared grammar and reads catalogue metadata. It writes nothing, reserves nothing
        // and creates no run, which is why it is lawful behind a read.
        ApplicationResult<JobExecutionPlan> admitted = await executor.AdmitAsync(
            new ResolvedJobTarget
            {
                Kind = DefinitionKind.Transformation,
                DefinitionId = definitionId,
                ResolvedVersion = version.Value.VersionNumber,
                PolicyApplied = policy,
            },
            cancellationToken);

        if (admitted.IsFailure)
        {
            return Capability(definitionCode, definitionId, version.Value.VersionNumber, policy, family,
                true, false, admitted.Error?.Code, admitted.Error?.Message);
        }

        return Capability(definitionCode, definitionId, version.Value.VersionNumber, policy, family,
            true, true, null, null);
    }

    private static ApplicationResult<CanvasExecutionCapabilityDto> Capability(
        string definitionCode,
        Guid definitionId,
        int? resolvedVersion,
        JobTargetVersionPolicy policy,
        JobExecutionCapability family,
        bool executorRegistered,
        bool eligible,
        string? refusalCode,
        string? refusalDetail)
    {
        return ApplicationResult<CanvasExecutionCapabilityDto>.Success(new CanvasExecutionCapabilityDto(
            definitionCode,
            definitionId,
            DefinitionKind.Transformation.ToString(),
            resolvedVersion,
            policy.ToString(),
            family.IsExecutableByRuntime,
            executorRegistered,
            eligible,
            refusalCode,
            refusalDetail,
            "Static eligibility only. This reserves no capacity and guarantees no later admission: "
                + "a launch resolves the version again, checks authorization again and may still refuse."));
    }

    // ---------------------------------------------------------------- binding --

    public async Task<ApplicationResult<CanvasJobBindingDto>> BindAsync(
        Guid tenantId,
        CanvasJobBindingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ApplicationResult<Guid> owned = await ResolveOwnedDefinitionAsync(tenantId, request.DefinitionCode, cancellationToken);
        if (owned.IsFailure)
        {
            return ApplicationResult<CanvasJobBindingDto>.Failure(owned.Error!);
        }

        Guid definitionId = owned.Value;

        // THE PINNED VERSION IS PERSISTED AS ITSELF. "Latest" is a different promise and
        // is never substituted for a version an author chose.
        ApplicationResult<CanonicalDefinitionVersion> version =
            await ResolveVersionAsync(definitionId, request.PinnedVersion, cancellationToken);

        if (version.IsFailure || version.Value is null)
        {
            return ApplicationResult<CanvasJobBindingDto>.Failure(version.Error
                ?? ApplicationError.BusinessRule("No published version of this definition could be resolved."));
        }

        if (version.Value.Status != CanonicalVersionStatus.Published)
        {
            return ApplicationResult<CanvasJobBindingDto>.Failure(ApplicationError.BusinessRule(
                "Version " + version.Value.VersionNumber + " of " + request.DefinitionCode
                + " is " + version.Value.Status + ". Only a published version can be bound to a job."));
        }

        string jobCode = JobCodeFor(request.DefinitionCode);
        string jobName = string.IsNullOrWhiteSpace(request.JobName)
            ? "Canvas projection " + request.DefinitionCode
            : request.JobName!.Trim();

        ApplicationResult<GovernedJobBindingResult> bound = await _jobs.BindGovernedTargetAsync(
            new GovernedJobBindingRequest(
                jobCode,
                jobName,
                Family,
                DefinitionKind.Transformation.ToString(),
                definitionId,
                request.PinnedVersion.HasValue ? JobTargetVersionPolicy.Pinned : JobTargetVersionPolicy.CurrentPublished,
                request.PinnedVersion,
                null),
            cancellationToken);

        if (bound.IsFailure || bound.Value is null)
        {
            return ApplicationResult<CanvasJobBindingDto>.Failure(bound.Error
                ?? ApplicationError.Unexpected("The job authority returned no binding."));
        }

        return ApplicationResult<CanvasJobBindingDto>.Success(new CanvasJobBindingDto(
            bound.Value.Job.Id,
            bound.Value.Job.JobCode,
            bound.Value.Job.JobName,
            definitionId,
            request.DefinitionCode,
            DefinitionKind.Transformation.ToString(),
            request.PinnedVersion.HasValue ? JobTargetVersionPolicy.Pinned.ToString() : JobTargetVersionPolicy.CurrentPublished.ToString(),
            request.PinnedVersion,
            bound.Value.Created));
    }

    public async Task<ApplicationResult<CanvasJobBindingDto>> FindBindingAsync(
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        ApplicationResult<CanvasBoundJob> job = await ResolveBoundJobAsync(tenantId, definitionCode, cancellationToken);

        if (job.IsFailure || job.Value is null)
        {
            return ApplicationResult<CanvasJobBindingDto>.Failure(job.Error
                ?? ApplicationError.NotFound("This definition is not bound to a job."));
        }

        CanvasBoundJob bound = job.Value;

        return ApplicationResult<CanvasJobBindingDto>.Success(new CanvasJobBindingDto(
            bound.JobDefinitionId,
            bound.JobCode,
            bound.JobName,
            bound.TargetDefinitionId ?? Guid.Empty,
            definitionCode,
            bound.TargetDefinitionKind ?? DefinitionKind.Transformation.ToString(),
            (bound.VersionPolicy ?? JobTargetVersionPolicy.CurrentPublished).ToString(),
            bound.TargetDefinitionVersion,
            false));
    }

    // ---------------------------------------------------------------- evidence --

    public async Task<ApplicationResult<CanvasRunEvidenceDto?>> FindRunByCorrelationAsync(
        Guid tenantId,
        string definitionCode,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return ApplicationResult<CanvasRunEvidenceDto?>.Failure(
                ApplicationError.Validation("A run is attached by the correlation its launch carried."));
        }

        ApplicationResult<CanvasBoundJob> job = await ResolveBoundJobAsync(tenantId, definitionCode, cancellationToken);
        if (job.IsFailure || job.Value is null)
        {
            return ApplicationResult<CanvasRunEvidenceDto?>.Failure(job.Error
                ?? ApplicationError.NotFound("This definition is not bound to a job."));
        }

        // EXACTLY THIS LAUNCH. Two people launching the same job concurrently each carry
        // their own correlation, so neither can be shown the other's run.
        CanvasRunRecord? run = await _readModel.FindRunByCorrelationAsync(
            job.Value.JobDefinitionId, correlationId, cancellationToken);

        if (run is null)
        {
            return ApplicationResult<CanvasRunEvidenceDto?>.Success(null);
        }

        CanvasRunEvidenceDto evidence = await ComposeAsync(run, cancellationToken);
        return ApplicationResult<CanvasRunEvidenceDto?>.Success(evidence);
    }

    public async Task<ApplicationResult<CanvasRunEvidenceDto>> ReadRunAsync(
        Guid tenantId,
        string definitionCode,
        Guid runId,
        CancellationToken cancellationToken)
    {
        ApplicationResult<CanvasBoundJob> job = await ResolveBoundJobAsync(tenantId, definitionCode, cancellationToken);
        if (job.IsFailure || job.Value is null)
        {
            return ApplicationResult<CanvasRunEvidenceDto>.Failure(job.Error
                ?? ApplicationError.NotFound("This definition is not bound to a job."));
        }

        CanvasRunRecord? run = await _readModel.FindRunAsync(runId, cancellationToken);

        // A VALID RUN ID IS NOT AN ENTITLEMENT. The run must belong to the job this
        // tenant's definition is bound to, and the refusal says nothing about whether
        // the run exists elsewhere.
        if (run is null || run.JobDefinitionId != job.Value.JobDefinitionId)
        {
            return ApplicationResult<CanvasRunEvidenceDto>.Failure(
                ApplicationError.NotFound("No such run for this definition."));
        }

        return ApplicationResult<CanvasRunEvidenceDto>.Success(await ComposeAsync(run, cancellationToken));
    }

    private async Task<CanvasRunEvidenceDto> ComposeAsync(CanvasRunRecord run, CancellationToken cancellationToken)
    {
        IReadOnlyList<CanvasRunBlockDto> blocks = await _readModel.ListBlocksAsync(run.RunId, cancellationToken);
        var mapped = blocks.OrderBy(b => b.ExecutionOrdinal).ToArray();

        // Terminal is whatever the run authority says is no longer Running. The browser
        // never decides this, and a cancellation request does not make it terminal.
        bool terminal = run.Status != JobRunStatus.Running;

        return new CanvasRunEvidenceDto(
            run.RunId,
            run.JobDefinitionId,
            run.JobCode,
            run.Status.ToString(),
            terminal,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.CorrelationId,
            run.TargetDefinitionId,
            run.TargetDefinitionVersion,
            run.TargetDefinitionKind,
            run.TargetVersionPolicy?.ToString(),
            run.CancellationRequestedAtUtc.HasValue,
            run.CancellationRequestedAtUtc,
            run.CancellationAcknowledgedAtUtc,
            run.FailureReason,
            run.RunMessage,
            mapped);
    }

    // ------------------------------------------------------------------ shared --

    private async Task<ApplicationResult<Guid>> ResolveOwnedDefinitionAsync(
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(definitionCode))
        {
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("A definition code is required."));
        }

        if (tenantId == Guid.Empty)
        {
            return ApplicationResult<Guid>.Failure(new ApplicationError(
                "CANVAS_TENANT_UNRESOLVED",
                "The caller carries no resolvable tenant, so no definition can be resolved on their behalf.",
                ApplicationErrorType.Forbidden));
        }

        ApplicationResult<Guid?> found = await _definitions.FindByCodeAsync(tenantId, definitionCode.Trim(), cancellationToken);

        if (found.IsFailure)
        {
            return ApplicationResult<Guid>.Failure(found.Error!);
        }

        if (found.Value is null || found.Value == Guid.Empty)
        {
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound(
                "No definition with that code belongs to this tenant."));
        }

        return ApplicationResult<Guid>.Success(found.Value.Value);
    }

    private async Task<ApplicationResult<CanonicalDefinitionVersion>> ResolveVersionAsync(
        Guid definitionId,
        int? pinnedVersion,
        CancellationToken cancellationToken)
    {
        return pinnedVersion.HasValue
            ? await _definitions.ResolveExactAsync(definitionId, pinnedVersion.Value, cancellationToken)
            : await _definitions.ResolvePublishedAsync(definitionId, cancellationToken);
    }

    private async Task<ApplicationResult<CanvasBoundJob>> ResolveBoundJobAsync(
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        ApplicationResult<Guid> owned = await ResolveOwnedDefinitionAsync(tenantId, definitionCode, cancellationToken);
        if (owned.IsFailure)
        {
            return ApplicationResult<CanvasBoundJob>.Failure(owned.Error!);
        }

        CanvasBoundJob? job = await _readModel.FindBoundJobAsync(owned.Value, Family, cancellationToken);

        return job is null
            ? ApplicationResult<CanvasBoundJob>.Failure(ApplicationError.NotFound(
                "This definition is not bound to a job yet."))
            : ApplicationResult<CanvasBoundJob>.Success(job);
    }

    /// <summary>
    /// A deterministic job code derived from the definition code, so binding the same
    /// definition twice addresses the same job instead of creating a second one.
    /// </summary>
    public static string JobCodeFor(string definitionCode)
    {
        var text = new StringBuilder(JobCodePrefix);

        foreach (char c in definitionCode.Trim().ToUpperInvariant())
        {
            text.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return text.ToString();
    }
}
