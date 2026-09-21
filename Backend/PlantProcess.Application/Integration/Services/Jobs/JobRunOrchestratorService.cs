using PlantProcess.Application.Jobs.Admission;
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
    private readonly IJobExecutorResolver _executors;
    private readonly IJobAdmissionController _admission;

    public JobRunOrchestratorService(
        IRunnableJobLookup jobs,
        IJobRuntimeService jobRuntimeService,
        IImportBatchQueueProcessorService importBatchQueueProcessorService,
        IDataQualityService dataQualityService,
        IRiskScoreService riskScoreService,
        IJobExecutionCapabilityAuthority capability,
        IJobTargetResolver targetResolver,
        IJobDependencyService dependencies,
        IJobExecutorResolver executors,
        IJobAdmissionController admission)
    {
        _jobs = jobs;
        _jobRuntimeService = jobRuntimeService;
        _importBatchQueueProcessorService = importBatchQueueProcessorService;
        _dataQualityService = dataQualityService;
        _riskScoreService = riskScoreService;
        _capability = capability;
        _targetResolver = targetResolver;
        _dependencies = dependencies;
        _executors = executors;
        _admission = admission;
    }

    public Task<ApplicationResult<JobActionResponseDto>> RunNowAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken)
        => RunCoreAsync(jobDefinitionId, null, null, "ManualRunNow", requestedBy, correlationId, cancellationToken);

    /// <summary>
    /// T-106 B2.3c. One governed occurrence, through the same authorities as Run Now. The
    /// occurrence identity reaches the run row, and the database decides a race.
    /// </summary>
    public Task<ApplicationResult<JobActionResponseDto>> RunScheduledAsync(
        Guid jobDefinitionId,
        string occurrenceKey,
        DateTime nominalAtUtc,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(occurrenceKey))
        {
            return Task.FromResult(ApplicationResult<JobActionResponseDto>.Failure(
                ApplicationError.Validation("A scheduled run requires a governed occurrence identity.")));
        }

        return RunCoreAsync(
            jobDefinitionId, occurrenceKey, nominalAtUtc, "GovernedSchedule", triggeredBy, correlationId, cancellationToken);
    }

    private async Task<ApplicationResult<JobActionResponseDto>> RunCoreAsync(
        Guid jobDefinitionId,
        string? occurrenceKey,
        DateTime? nominalAtUtc,
        string triggerSource,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        JobDefinition? job = await _jobs.FindAsync(jobDefinitionId, cancellationToken);

        if (job is null)
            return ApplicationResult<JobActionResponseDto>.Failure(ApplicationError.NotFound("Job definition was not found."));

        // CAPABILITY FIRST, AND NOTHING BEFORE IT. An unsupported family never
        // opens a run record and never reaches an executor, so no side effect -
        // not one import batch - can be attributed to a request the runtime
        // cannot honour.
        ApplicationResult admitted = JobRunAdmission.Admit(job, _capability);
        if (admitted.IsFailure)
            return ApplicationResult<JobActionResponseDto>.Failure(admitted.Error!);

        ApplicationResult<JobTargetResolution> targetOk = await ResolveDeclaredTargetAsync(job, cancellationToken);
        if (targetOk.IsFailure || targetOk.Value is null)
            return ApplicationResult<JobActionResponseDto>.Failure(targetOk.Error!);

        // T-261. THE GOVERNED PLAN IS PROVEN BEFORE A RUN EXISTS.
        //
        // A family that declares a canonical target executes a definition version, so
        // everything that can be known about that version is proven here: that an executor
        // exists, that the exact version reads and compiles, that its source identity and
        // canonical target are lawful. A request that fails any of those must never leave
        // a run record behind for an operator to interpret.
        JobExecutionCapability family = _capability.Describe(job.JobType);
        IJobExecutor? governed = null;
        JobExecutionPlan? plan = null;

        if (family.TargetRequirement == JobTargetRequirement.Required)
        {
            governed = _executors.Resolve(job.JobType);

            if (governed is null)
            {
                return ApplicationResult<JobActionResponseDto>.Failure(new ApplicationError(
                    JobExecutionDiagnosticCodes.ExecutorMissing,
                    "Job family " + job.JobType + " is commissioned but no executor is registered for it.",
                    ApplicationErrorType.BusinessRule));
            }

            ResolvedJobTarget? resolvedTarget = targetOk.Value.Target;

            if (resolvedTarget is null)
            {
                return ApplicationResult<JobActionResponseDto>.Failure(new ApplicationError(
                    JobExecutionDiagnosticCodes.ExactVersionRequired,
                    "Job " + job.JobCode + " declares no definition version this runtime could execute.",
                    ApplicationErrorType.BusinessRule));
            }

            ApplicationResult<JobExecutionPlan> admittedPlan =
                await governed.AdmitAsync(resolvedTarget, cancellationToken);

            if (admittedPlan.IsFailure || admittedPlan.Value is null)
                return ApplicationResult<JobActionResponseDto>.Failure(admittedPlan.Error!);

            plan = admittedPlan.Value;
        }

        // The reservation covers Start, execution, and terminal persistence, including
        // refused duplicate occurrences, failed starts, exceptions, and cancellation.
        await using JobAdmissionDecision resource = await _admission.AcquireAsync(
            JobAdmissionRequests.ForJob(job, correlationId), cancellationToken);
        if (!resource.IsAdmitted)
            return ApplicationResult<JobActionResponseDto>.Failure(JobAdmissionErrors.Refused(resource));
        cancellationToken.ThrowIfCancellationRequested();

        string runCorrelation = correlationId ?? Guid.NewGuid().ToString("N");

        // Admission, capability, target resolution and governed plan admission have already
        // spoken. Only now is a run created, and a scheduled request creates it with its
        // occurrence identity so the INSERT itself is the claim.
        var run = plan is not null
            ? await _jobRuntimeService.StartForTargetAsync(
                job.JobCode,
                occurrenceKey,
                occurrenceKey is null ? null : nominalAtUtc ?? DateTime.UtcNow,
                triggerSource: triggerSource,
                triggeredBy: requestedBy ?? (occurrenceKey is null ? "Admin" : "GovernedScheduleDispatcher"),
                correlationId: runCorrelation,
                target: plan.Target,
                cancellationToken)
            : occurrenceKey is null
            ? await _jobRuntimeService.StartAsync(
                job.JobCode,
                triggerSource: triggerSource,
                triggeredBy: requestedBy ?? "Admin",
                correlationId: runCorrelation,
                cancellationToken)
            : await _jobRuntimeService.StartScheduledAsync(
                job.JobCode,
                occurrenceKey,
                nominalAtUtc ?? DateTime.UtcNow,
                triggerSource: triggerSource,
                triggeredBy: requestedBy ?? "GovernedScheduleDispatcher",
                correlationId: runCorrelation,
                cancellationToken);

        if (run.IsFailure || run.Value is null)
            return ApplicationResult<JobActionResponseDto>.Failure(run.Error!);

        try
        {
            if (governed is not null && plan is not null)
            {
                return await RunGovernedAsync(
                    job, governed, plan, run.Value.Id, runCorrelation, cancellationToken);
            }

            var executionResult = await ExecuteJobAsync(job.JobType, cancellationToken);

            var finalStatus = executionResult.IsSuccess ? JobRunStatus.Ok : JobRunStatus.Failed;

            var completed = await _jobRuntimeService.CompleteAsync(
                run.Value.Id, finalStatus, executionResult.Message,
                executionResult.IsSuccess ? null : executionResult.Message,
                executionResult.ResultSummaryJson, cancellationToken);

            if (completed.IsFailure)
                return ApplicationResult<JobActionResponseDto>.Failure(completed.Error!);

            return ApplicationResult<JobActionResponseDto>.Success(
                new JobActionResponseDto(
                    job.Id, job.JobCode, job.JobName, job.JobType, finalStatus,
                    executionResult.Message, run.Value.Id, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            await _jobRuntimeService.CompleteAsync(
                run.Value.Id, JobRunStatus.Failed, ex.Message, ex.Message, null, CancellationToken.None);

            return ApplicationResult<JobActionResponseDto>.Failure(
                ApplicationError.Unexpected($"{triggerSource} failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// T-261. THE GOVERNED EXECUTION OF AN ALREADY ADMITTED PLAN.
    ///
    /// The plan is not re-resolved and the version is not looked up again: this run owns
    /// the exact version its own evidence names. A cooperative stop converges through
    /// acknowledgement rather than a completion, because a cancelled run is not a failed
    /// one and is certainly not a successful one.
    /// </summary>
    private async Task<ApplicationResult<JobActionResponseDto>> RunGovernedAsync(
        JobDefinition job,
        IJobExecutor executor,
        JobExecutionPlan plan,
        Guid runId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var context = new JobExecutionContext(
            job.Id, job.JobCode, job.JobType, runId, plan, correlationId);

        ApplicationResult<JobExecutionOutcome> executed =
            await executor.ExecuteAsync(context, cancellationToken);

        if (executed.IsFailure || executed.Value is null)
        {
            string refusal = (executed.Error?.Code ?? JobExecutionDiagnosticCodes.BlockFailed)
                + ": " + (executed.Error?.Message ?? "Governed execution produced no outcome.");

            var refused = await _jobRuntimeService.CompleteAsync(
                runId, JobRunStatus.Failed, refusal, refusal, null, cancellationToken);

            if (refused.IsFailure)
                return ApplicationResult<JobActionResponseDto>.Failure(refused.Error!);

            return ApplicationResult<JobActionResponseDto>.Success(new JobActionResponseDto(
                job.Id, job.JobCode, job.JobName, job.JobType, JobRunStatus.Failed,
                refusal, runId, DateTime.UtcNow));
        }

        JobExecutionOutcome outcome = executed.Value;
        string summary = JsonSerializer.Serialize(outcome);

        if (outcome.Cancelled)
        {
            var acknowledged = await _jobRuntimeService.AcknowledgeCancellationAsync(
                runId, outcome.Message, cancellationToken);

            if (acknowledged.IsFailure)
                return ApplicationResult<JobActionResponseDto>.Failure(acknowledged.Error!);

            return ApplicationResult<JobActionResponseDto>.Success(new JobActionResponseDto(
                job.Id, job.JobCode, job.JobName, job.JobType, JobRunStatus.Cancelled,
                outcome.Message, runId, DateTime.UtcNow));
        }

        JobRunStatus finalStatus = outcome.Succeeded ? JobRunStatus.Ok : JobRunStatus.Failed;

        string message = outcome.Succeeded
            ? outcome.Message
            : (outcome.DiagnosticCode ?? JobExecutionDiagnosticCodes.BlockFailed)
                + ": " + (outcome.DiagnosticDetail ?? outcome.Message);

        var completedRun = await _jobRuntimeService.CompleteAsync(
            runId, finalStatus, message, outcome.Succeeded ? null : message, summary, cancellationToken);

        if (completedRun.IsFailure)
            return ApplicationResult<JobActionResponseDto>.Failure(completedRun.Error!);

        return ApplicationResult<JobActionResponseDto>.Success(new JobActionResponseDto(
            job.Id, job.JobCode, job.JobName, job.JobType, finalStatus,
            message, runId, DateTime.UtcNow));
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
        var chainRuns = new Dictionary<Guid, JobRunSnapshot>();

        foreach (Guid id in order.Value)
        {
            ApplicationResult<IReadOnlyList<JobDependencyOutcome>> evaluation =
                await _dependencies.EvaluateAsync(id, chainRuns, cancellationToken);

            if (evaluation.IsFailure || evaluation.Value is null)
                return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(evaluation.Error!);

            IReadOnlyList<JobDependencyOutcome> outcomes = evaluation.Value;

            bool blocked = false;
            foreach (JobDependencyOutcome outcome in outcomes)
            {
                if (outcome.BlocksDownstream) { blocked = true; }
            }

            if (blocked)
            {
                // THE ATTEMPT IS REAL. A blocked child is recorded as a terminal
                // Blocked run with a genuine history identity, so the dependency
                // evidence below references a run that exists rather than an
                // invented id, and the monitor stops reporting a stale outcome.
                string reason = FirstBlockingReason(outcomes);

                var blockedRun = await _jobRuntimeService.RecordBlockedAsync(
                    id, "ManualRunNow", requestedBy ?? "Admin", chainCorrelation, reason, cancellationToken);

                if (blockedRun.IsFailure || blockedRun.Value is null)
                    return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(blockedRun.Error!);

                ApplicationResult recorded = await _dependencies.RecordResolutionsAsync(
                    blockedRun.Value.Id, id, outcomes, cancellationToken);

                if (recorded.IsFailure)
                    return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(recorded.Error!);

                return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(
                    ApplicationError.BusinessRule(reason));
            }

            ApplicationResult<JobActionResponseDto> step =
                await RunNowAsync(id, requestedBy, chainCorrelation, cancellationToken);

            if (step.IsFailure || step.Value is null)
                return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(step.Error!);

            executed.Add(step.Value);

            if (step.Value.JobRunHistoryId.HasValue && outcomes.Count > 0)
            {
                ApplicationResult recorded = await _dependencies.RecordResolutionsAsync(
                    step.Value.JobRunHistoryId.Value, id, outcomes, cancellationToken);

                if (recorded.IsFailure)
                    return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Failure(recorded.Error!);
            }

            if (step.Value.JobRunHistoryId.HasValue)
            {
                chainRuns[id] = new JobRunSnapshot(step.Value.JobRunHistoryId.Value, step.Value.Status, null);
            }

            // Do not return here. A failed predecessor must remain in chainRuns so
            // the next dependent can evaluate it, create its own REAL Blocked run
            // and persist failed_upstream evidence. Returning at this point was the
            // exact absence the corrective is intended to remove. Optional dependents
            // are also allowed to continue and record skipped_optional honestly.
        }

        return ApplicationResult<IReadOnlyList<JobActionResponseDto>>.Success(executed);
    }

    private static string FirstBlockingReason(IReadOnlyList<JobDependencyOutcome> outcomes)
    {
        foreach (JobDependencyOutcome outcome in outcomes)
        {
            if (outcome.BlocksDownstream) { return outcome.Reason; }
        }

        return "The run was blocked before compute started.";
    }

    private async Task<ApplicationResult<JobTargetResolution>> ResolveDeclaredTargetAsync(
        JobDefinition job, CancellationToken cancellationToken)
    {
        if (!job.TargetDefinitionId.HasValue)
        {
            ApplicationResult<JobTargetResolution> none =
                await _targetResolver.ResolveAsync(job.JobType, null, cancellationToken);

            return none;
        }

        if (string.IsNullOrWhiteSpace(job.TargetDefinitionKind) || !job.TargetVersionPolicy.HasValue)
        {
            return ApplicationResult<JobTargetResolution>.Failure(ApplicationError.Validation(
                "Job " + job.JobCode + " declares a target identity without a kind or a version policy, "
                + "so nothing can say which definition version it would run."));
        }

        DefinitionKind kind;
        if (!Enum.TryParse(job.TargetDefinitionKind, false, out kind)
            || !Enum.IsDefined(typeof(DefinitionKind), kind))
        {
            return ApplicationResult<JobTargetResolution>.Failure(ApplicationError.Validation(
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

        return resolved;
    }

    private async Task<RunNowExecutionResult> ExecuteJobAsync(
        JobDefinitionType jobType, CancellationToken cancellationToken)
    {
        switch (jobType)
        {
            // CanonicalRefresh is deliberately absent. Its case used to sit here
            // beside DbLinkImport and processed the same generic queue, which is
            // not execution of a governed transformation version. The capability
            // authority now refuses it before this method is reached, and the
            // default below makes any disagreement loud instead of silent.
            case JobDefinitionType.DbLinkImport:
            {
                var result = await _importBatchQueueProcessorService.ProcessPendingBatchesAsync(
                    maxBatches: 10, rowsPerBatch: 5000, stopOnFirstError: false,
                    runDataQualityScan: false, cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Import queue processing failed.");
                }

                // T-106 B2.5: the enclosing run reports what the batches actually did.
                var terminal = PlantProcess.Application.Jobs.Execution.JobRunStatusConvergence.Resolve(
                    new PlantProcess.Application.Jobs.Execution.JobUnitCounts(
                        result.Value.BatchesProcessed,
                        result.Value.BatchesCompleted,
                        result.Value.BatchesFailed,
                        result.Value.BatchesSkipped,
                        false));

                if (!terminal.IsSuccessfulTerminal)
                {
                    return RunNowExecutionResult.Failed(terminal.Message);
                }

                return RunNowExecutionResult.Ok(
                    terminal.Message,
                    JsonSerializer.Serialize(result.Value));
            }

            case JobDefinitionType.DataQualityScan:
            {
                var result = await _dataQualityService.RunFullScanAsync(
                    maxCandidatesPerRule: 500, cancellationToken);

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
                        SiteId: null, RiskType: "QualityRisk", MaxMaterials: 100,
                        StoreResult: true, RequestedBy: "AdminRunNow",
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
                throw new InvalidOperationException(
                    "Job family " + jobType + " passed capability admission but has no executor here. "
                    + "The capability authority and the executor disagree.");
        }
    }

    private sealed record RunNowExecutionResult(bool IsSuccess, string Message, string? ResultSummaryJson)
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