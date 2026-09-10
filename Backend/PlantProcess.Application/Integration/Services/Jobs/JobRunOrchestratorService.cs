using PlantProcess.Application.Analytics.Contracts;
using System.Text.Json;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Dependencies;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Services.Jobs;

public sealed class JobRunOrchestratorService : IJobRunOrchestratorService
{
    private readonly IRunnableJobLookup _jobs;
    private readonly IJobRuntimeService _jobRuntimeService;
    private readonly IImportBatchQueueProcessorService _importBatchQueueProcessorService;
    private readonly IDataQualityService _dataQualityService;
    private readonly IRiskScoreService _riskScoreService;
    private readonly IJobExecutionCapabilityAuthority _capability;
    private readonly IJobTargetResolver _targetResolver;
    private readonly IJobDependencyService _dependencies;

    public JobRunOrchestratorService(
        IRunnableJobLookup jobs,
        IJobRuntimeService jobRuntimeService,
        IImportBatchQueueProcessorService importBatchQueueProcessorService,
        IDataQualityService dataQualityService,
        IRiskScoreService riskScoreService,
        IJobExecutionCapabilityAuthority capability,
        IJobTargetResolver targetResolver,
        IJobDependencyService dependencies)
    {
        _jobs = jobs;
        _jobRuntimeService = jobRuntimeService;
        _importBatchQueueProcessorService = importBatchQueueProcessorService;
        _dataQualityService = dataQualityService;
        _riskScoreService = riskScoreService;
        _capability = capability;
        _targetResolver = targetResolver;
        _dependencies = dependencies;
    }

    public async Task<ApplicationResult<JobActionResponseDto>> RunNowAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        JobDefinition? job = await _jobs.FindAsync(jobDefinitionId, cancellationToken);

        if (job is null)
            return ApplicationResult<JobActionResponseDto>.Failure(ApplicationError.NotFound("Job definition was not found."));

        // ADMISSION BEFORE ANY RUN RECORD EXISTS. Paused state and executor
        // capability are both answered here, so an unsupported family never
        // opens a JobRunHistory row it can only close as failed.
        ApplicationResult admitted = JobRunAdmission.Admit(job, _capability);
        if (admitted.IsFailure)
            return ApplicationResult<JobActionResponseDto>.Failure(admitted.Error!);

        // The declared target is resolved through the canonical definition
        // authority BEFORE execution, so a job that cannot say which immutable
        // version it runs never runs at all.
        ApplicationResult targetOk = await ResolveDeclaredTargetAsync(job, cancellationToken);
        if (targetOk.IsFailure)
            return ApplicationResult<JobActionResponseDto>.Failure(targetOk.Error!);

        var run = await _jobRuntimeService.StartAsync(
            job.JobCode,
            triggerSource: "ManualRunNow",
            triggeredBy: requestedBy ?? "Admin",
            correlationId: correlationId ?? Guid.NewGuid().ToString("N"),
            cancellationToken);

        if (run.IsFailure || run.Value is null)
            return ApplicationResult<JobActionResponseDto>.Failure(run.Error!);

        try
        {
            var executionResult = await ExecuteJobAsync(job.JobType, cancellationToken);

            var finalStatus = executionResult.IsSuccess
                ? JobRunStatus.Ok
                : JobRunStatus.Failed;

            var completed = await _jobRuntimeService.CompleteAsync(
                run.Value.Id,
                finalStatus,
                executionResult.Message,
                executionResult.IsSuccess ? null : executionResult.Message,
                executionResult.ResultSummaryJson,
                cancellationToken);

            if (completed.IsFailure)
                return ApplicationResult<JobActionResponseDto>.Failure(completed.Error!);

            return ApplicationResult<JobActionResponseDto>.Success(
                new JobActionResponseDto(
                    job.Id,
                    job.JobCode,
                    job.JobName,
                    job.JobType,
                    finalStatus,
                    executionResult.Message,
                    run.Value.Id,
                    DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            await _jobRuntimeService.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Failed,
                ex.Message,
                ex.Message,
                null,
                CancellationToken.None);

            return ApplicationResult<JobActionResponseDto>.Failure(
                ApplicationError.Unexpected($"Run Now failed: {ex.Message}"));
        }
    }

    public async Task<ApplicationResult<IReadOnlyList<JobActionResponseDto>>> RunWithDependenciesAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ApplicationResult<IReadOnlyList<Guid>> order =
            await _dependencies.ResolveExecutionOrderAsync(jobDefinitionId, cancellationToken);

        if (order.IsFailure || order.Value is null)
            return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(order.Error!);

        string chainCorrelation = correlationId ?? Guid.NewGuid().ToString("N");
        var executed = new List<JobActionResponseDto>();

        foreach (Guid id in order.Value)
        {
            ApplicationResult<JobActionResponseDto> step =
                await RunNowAsync(id, requestedBy, chainCorrelation, cancellationToken);

            if (step.IsFailure || step.Value is null)
                return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(step.Error!);

            executed.Add(step.Value);

            // A predecessor that ran and failed still stops the chain. Run Now
            // reports a failed run as a successful REQUEST, which is the right
            // answer for one job and the wrong one for an ordered chain.
            if (step.Value.Status != JobRunStatus.Ok)
            {
                return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(
                    ApplicationError.BusinessRule(
                        "Job " + step.Value.JobCode + " did not complete successfully, so the jobs that depend on it "
                        + "were not started. " + step.Value.Message));
            }
        }

        return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Success(executed);
    }

    private async Task<ApplicationResult> ResolveDeclaredTargetAsync(
        JobDefinition job,
        CancellationToken cancellationToken)
    {
        if (!job.TargetDefinitionId.HasValue)
        {
            ApplicationResult<JobTargetResolution> none =
                await _targetResolver.ResolveAsync(job.JobType, null, cancellationToken);

            return none.IsFailure
                ? ApplicationResult.Failure(none.Error!)
                : ApplicationResult.Success();
        }

        if (string.IsNullOrWhiteSpace(job.TargetDefinitionKind) || !job.TargetVersionPolicy.HasValue)
        {
            return ApplicationResult.Failure(
                ApplicationError.Validation(
                    "Job " + job.JobCode + " declares a target identity without a kind or a version policy, "
                    + "so nothing can say which definition version it would run."));
        }

        DefinitionKind kind;
        if (!Enum.TryParse(job.TargetDefinitionKind, false, out kind)
            || !Enum.IsDefined(typeof(DefinitionKind), kind))
        {
            return ApplicationResult.Failure(
                ApplicationError.Validation(
                    "Job " + job.JobCode + " declares target kind '" + job.TargetDefinitionKind
                    + "', which is not a canonical definition kind."));
        }

        var reference = new JobTargetReference
        {
            Kind = kind,
            DefinitionId = job.TargetDefinitionId.Value,
            VersionPolicy = job.TargetVersionPolicy.Value,
            PinnedVersion = job.TargetDefinitionVersion,
            ParametersJson = job.TargetParametersJson
        };

        ApplicationResult<JobTargetResolution> resolved =
            await _targetResolver.ResolveAsync(job.JobType, reference, cancellationToken);

        return resolved.IsFailure
            ? ApplicationResult.Failure(resolved.Error!)
            : ApplicationResult.Success();
    }

    private async Task<RunNowExecutionResult> ExecuteJobAsync(
        JobDefinitionType jobType,
        CancellationToken cancellationToken)
    {
        switch (jobType)
        {
            case JobDefinitionType.DbLinkImport:
            case JobDefinitionType.CanonicalRefresh:
            {
                var result = await _importBatchQueueProcessorService.ProcessPendingBatchesAsync(
                    maxBatches: 10,
                    rowsPerBatch: 5000,
                    stopOnFirstError: false,
                    runDataQualityScan: false,
                    cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Import queue processing failed.");
                }

                return RunNowExecutionResult.Ok(
                    $"Import queue completed. Scanned={result.Value.BatchesScanned}, Completed={result.Value.BatchesCompleted}, Failed={result.Value.BatchesFailed}.",
                    JsonSerializer.Serialize(result.Value));
            }

            case JobDefinitionType.DataQualityScan:
            {
                var result = await _dataQualityService.RunFullScanAsync(
                    maxCandidatesPerRule: 500,
                    cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Data quality scan failed.");
                }

                return RunNowExecutionResult.Ok(
                    $"Data quality scan completed. Candidates={result.Value.CandidatesFound}, NewIssues={result.Value.NewIssuesPersisted}.",
                    JsonSerializer.Serialize(result.Value));
            }

            case JobDefinitionType.RiskScoring:
            {
                var result = await _riskScoreService.CalculateBatchAsync(
                    new CalculateRiskScoresBatchCommand(
                        SiteId: null,
                        RiskType: "QualityRisk",
                        MaxMaterials: 100,
                        StoreResult: true,
                        RequestedBy: "AdminRunNow",
                        CorrelationId: Guid.NewGuid().ToString("N")),
                    cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Risk scoring failed.");
                }

                return RunNowExecutionResult.Ok(
                    $"Risk scoring completed. Calculated={result.Value.ScoresCalculated}, Stored={result.Value.ScoresStored}.",
                    JsonSerializer.Serialize(result.Value));
            }

            default:
                // Unreachable by contract. Admission has already asked the one
                // capability authority, so arriving here means the authority
                // and this switch disagree about what the runtime can execute.
                // A loud contradiction is the point: the alternative is the
                // defect this task exists to close.
                throw new InvalidOperationException(
                    "Job family " + jobType + " passed capability admission but has no executor here. "
                    + "The capability authority and the executor disagree.");
        }
    }

    private sealed record RunNowExecutionResult(
        bool IsSuccess,
        string Message,
        string? ResultSummaryJson)
    {
        public static RunNowExecutionResult Ok(string message, string? resultSummaryJson)
        {
            return new RunNowExecutionResult(true, message, resultSummaryJson);
        }

        public static RunNowExecutionResult Failed(string message)
        {
            return new RunNowExecutionResult(false, message, null);
        }
    }
}