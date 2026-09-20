using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Jobs.Dependencies;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// The orchestrator's governed path: the plan is admitted before any run exists, the run
/// is created with the exact resolved target, the executor receives that frozen plan,
/// and cancellation converges through acknowledgement rather than completion.
/// </summary>
public sealed class GovernedJobDispatchTests
{
    private static readonly Guid DefinitionId = Guid.Parse("7a1f0000-0000-0000-0000-0000000000aa");

    private sealed record StubPlan(ResolvedJobTarget Resolved) : JobExecutionPlan(Resolved);

    private sealed class StubExecutor : IJobExecutor
    {
        public ApplicationResult<JobExecutionPlan>? Admission { get; set; }
        public JobExecutionOutcome Outcome { get; set; } =
            new(true, false, "done", 1, Array.Empty<JobExecutionBlockResult>(), null, null);
        public List<JobExecutionContext> Executed { get; } = new();
        public List<ResolvedJobTarget> Admitted { get; } = new();

        public JobDefinitionType Executes => JobDefinitionType.CanonicalRefresh;

        public Task<ApplicationResult<JobExecutionPlan>> AdmitAsync(
            ResolvedJobTarget target, CancellationToken cancellationToken)
        {
            Admitted.Add(target);
            return Task.FromResult(Admission ?? ApplicationResult<JobExecutionPlan>.Success(new StubPlan(target)));
        }

        public Task<ApplicationResult<JobExecutionOutcome>> ExecuteAsync(
            JobExecutionContext context, CancellationToken cancellationToken)
        {
            Executed.Add(context);
            return Task.FromResult(ApplicationResult<JobExecutionOutcome>.Success(Outcome));
        }
    }

    private sealed class Lookup : IRunnableJobLookup
    {
        private readonly JobDefinition _job;
        public Lookup(JobDefinition job) { _job = job; }

        public Task<JobDefinition?> FindAsync(Guid jobDefinitionId, CancellationToken cancellationToken) =>
            Task.FromResult<JobDefinition?>(_job.Id == jobDefinitionId ? _job : null);
    }

    private sealed class Runtime : IJobRuntimeService
    {
        public List<string> PlainStarts { get; } = new();
        public List<ResolvedJobTarget> TargetStarts { get; } = new();
        public List<JobRunStatus> Completions { get; } = new();
        public List<Guid> Acknowledged { get; } = new();
        public Guid RunId { get; } = Guid.NewGuid();

        public Task<ApplicationResult<JobRunHistoryDto>> StartAsync(
            string jobCode, string triggerSource, string? triggeredBy, string? correlationId, CancellationToken ct)
        {
            PlainStarts.Add(jobCode);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(JobRunStatus.Running)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> StartForTargetAsync(
            string jobCode, string? occurrenceKey, DateTime? nominalAtUtc, string triggerSource,
            string? triggeredBy, string? correlationId, ResolvedJobTarget target, CancellationToken cancellationToken)
        {
            TargetStarts.Add(target);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(JobRunStatus.Running)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> CompleteAsync(
            Guid jobRunHistoryId, JobRunStatus finalStatus, string? message, string? failureReason,
            string? resultSummaryJson, CancellationToken ct)
        {
            Completions.Add(finalStatus);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(finalStatus)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> AcknowledgeCancellationAsync(
            Guid jobRunHistoryId, string? message, CancellationToken cancellationToken)
        {
            Acknowledged.Add(jobRunHistoryId);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(JobRunStatus.Cancelled)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> RecordBlockedAsync(
            Guid jobDefinitionId, string triggerSource, string? triggeredBy, string? correlationId,
            string reason, CancellationToken ct) =>
            throw new NotSupportedException("Not reached.");

        public Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
            Guid jobDefinitionId, int take, CancellationToken ct) =>
            throw new NotSupportedException("Not reached.");

        private JobRunHistoryDto Dto(JobRunStatus status) =>
            new(RunId, Guid.NewGuid(), "PROBE", "PROBE", JobDefinitionType.CanonicalRefresh,
                status, DateTime.UtcNow, null, null, "ManualRunNow", "tester", null, null, null, null);
    }

    private sealed class NoImport : IImportBatchQueueProcessorService
    {
        public Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
            int maxBatches, int rowsPerBatch, bool stopOnFirstError, bool runDataQualityScan, CancellationToken ct) =>
            throw new InvalidOperationException("The generic import queue is never a Transformation execution path.");
    }

    private sealed class NoQuality : IDataQualityService
    {
        public Task<ApplicationResult<Guid>> RaiseIssueAsync(RaiseDataQualityIssueCommand c, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<DataQualityScanSummary>> RunFullScanAsync(int max, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class NoRisk : IRiskScoreService
    {
        public Task<ApplicationResult<Guid>> StoreAsync(StoreRiskScoreCommand c, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<CalculateRiskScoreResult>> CalculateAsync(CalculateRiskScoreCommand c, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<CalculateRiskScoresBatchResult>> CalculateBatchAsync(
            CalculateRiskScoresBatchCommand c, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class Resolver : IJobTargetResolver
    {
        public Task<ApplicationResult<JobTargetResolution>> ResolveAsync(
            JobDefinitionType jobClass, JobTargetReference? target, CancellationToken ct)
        {
            if (target is null)
            {
                return Task.FromResult(ApplicationResult<JobTargetResolution>.Success(JobTargetResolution.None()));
            }

            return Task.FromResult(ApplicationResult<JobTargetResolution>.Success(JobTargetResolution.Of(
                new ResolvedJobTarget
                {
                    Kind = target.Kind,
                    DefinitionId = target.DefinitionId,
                    ResolvedVersion = target.PinnedVersion ?? 4,
                    PolicyApplied = target.VersionPolicy,
                })));
        }

        public Task<ApplicationResult> AssertNotTargetedByJobsAsync(DefinitionKind kind, Guid definitionId, CancellationToken ct) =>
            Task.FromResult(ApplicationResult.Success());
    }

    private sealed class NoDependencies : IJobDependencyService
    {
        public Task<ApplicationResult> AddDependencyAsync(
            Guid a, Guid b, JobDependencyKind k, bool r, int? v, int? s, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ApplicationResult> RemoveDependencyAsync(Guid a, Guid b, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<IReadOnlyList<JobDependencyEdge>>> ListEdgesAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<IReadOnlyList<Guid>>> ResolveExecutionOrderAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<Guid>>.Success(new[] { id }));

        public Task<ApplicationResult<IReadOnlyList<JobDependencyOutcome>>> EvaluateAsync(
            Guid id, IReadOnlyDictionary<Guid, JobRunSnapshot> chainRuns, CancellationToken ct) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<JobDependencyOutcome>>.Success(
                Array.Empty<JobDependencyOutcome>()));

        public Task<ApplicationResult> RecordResolutionsAsync(
            Guid runId, Guid jobId, IReadOnlyList<JobDependencyOutcome> outcomes, CancellationToken ct) =>
            Task.FromResult(ApplicationResult.Success());
    }

    private sealed class World
    {
        public World(JobDefinitionType jobType, bool withTarget = true)
        {
            Job = new JobDefinition("PROBE_" + jobType, "Probe", jobType, "Manual", false);
            if (withTarget)
            {
                Job.AssignTargetDefinition(
                    jobType == JobDefinitionType.CanonicalRefresh ? "Transformation" : "Model",
                    DefinitionId, JobTargetVersionPolicy.Pinned, 3);
            }

            Orchestrator = new JobRunOrchestratorService(
                new Lookup(Job), Runtime, new NoImport(), new NoQuality(), new NoRisk(),
                new JobExecutionCapabilityAuthority(), new Resolver(), new NoDependencies(),
                new JobExecutorResolver(new IJobExecutor[] { Executor }));
        }

        public JobDefinition Job { get; }
        public Runtime Runtime { get; } = new();
        public StubExecutor Executor { get; } = new();
        public JobRunOrchestratorService Orchestrator { get; }
    }

    [Fact]
    public async Task A_refused_plan_creates_no_run()
    {
        var world = new World(JobDefinitionType.CanonicalRefresh);
        world.Executor.Admission = ApplicationResult<JobExecutionPlan>.Failure(new ApplicationError(
            JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned, "not yet", ApplicationErrorType.BusinessRule));

        var result = await world.Orchestrator.RunNowAsync(world.Job.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned, result.Error!.Code);
        Assert.Empty(world.Runtime.TargetStarts);
        Assert.Empty(world.Runtime.PlainStarts);
        Assert.Empty(world.Executor.Executed);
    }

    [Fact]
    public async Task The_run_is_created_with_the_exact_resolution_and_executes_that_frozen_plan()
    {
        var world = new World(JobDefinitionType.CanonicalRefresh);

        var result = await world.Orchestrator.RunNowAsync(world.Job.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(JobRunStatus.Ok, result.Value!.Status);
        ResolvedJobTarget started = Assert.Single(world.Runtime.TargetStarts);
        Assert.Equal(DefinitionId, started.DefinitionId);
        Assert.Equal(3, started.ResolvedVersion);
        Assert.Empty(world.Runtime.PlainStarts);

        JobExecutionContext context = Assert.Single(world.Executor.Executed);
        Assert.Same(world.Executor.Admitted.Single(), context.Plan.Target);
        Assert.Equal(world.Runtime.RunId, context.JobRunHistoryId);
        Assert.Equal(new[] { JobRunStatus.Ok }, world.Runtime.Completions.ToArray());
    }

    [Fact]
    public async Task A_typed_block_failure_completes_the_run_as_failed_with_its_code()
    {
        var world = new World(JobDefinitionType.CanonicalRefresh);
        world.Executor.Outcome = new JobExecutionOutcome(
            false, false, "block failed", 0, Array.Empty<JobExecutionBlockResult>(),
            JobExecutionDiagnosticCodes.SourceIdentityInvalid, "row 2");

        var result = await world.Orchestrator.RunNowAsync(world.Job.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(JobRunStatus.Failed, result.Value!.Status);
        Assert.Contains(JobExecutionDiagnosticCodes.SourceIdentityInvalid, result.Value.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { JobRunStatus.Failed }, world.Runtime.Completions.ToArray());
    }

    [Fact]
    public async Task An_observed_cancellation_is_acknowledged_and_never_completed_as_success()
    {
        var world = new World(JobDefinitionType.CanonicalRefresh);
        world.Executor.Outcome = new JobExecutionOutcome(
            false, true, "stopped", 0, Array.Empty<JobExecutionBlockResult>(),
            JobExecutionDiagnosticCodes.Cancelled, null);

        var result = await world.Orchestrator.RunNowAsync(world.Job.Id, "tester", null, CancellationToken.None);

        Assert.Equal(JobRunStatus.Cancelled, result.Value!.Status);
        Assert.Equal(new[] { world.Runtime.RunId }, world.Runtime.Acknowledged.ToArray());
        Assert.Empty(world.Runtime.Completions);
    }

    [Fact]
    public async Task An_unsupported_family_still_refuses_before_admission()
    {
        var world = new World(JobDefinitionType.MlWeeklyFull);

        var result = await world.Orchestrator.RunNowAsync(world.Job.Id, "tester", null, CancellationToken.None);

        Assert.Equal(JobExecutionErrorCodes.NoExecutorForJobFamily, result.Error!.Code);
        Assert.Empty(world.Executor.Admitted);
        Assert.Empty(world.Runtime.TargetStarts);
    }
}
