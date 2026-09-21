using PlantProcess.Application.Jobs.Admission;
using Microsoft.Extensions.Logging.Abstractions;
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
[Trait("Gate", "JobAdmissionProduction")]
public sealed class JobAdmissionProductionTests
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

        public Func<CancellationToken, Task>? OnExecute { get; set; }
        public async Task<ApplicationResult<JobExecutionOutcome>> ExecuteAsync(
            JobExecutionContext context, CancellationToken cancellationToken)
        {
            Executed.Add(context);
            if (OnExecute is not null) await OnExecute(cancellationToken);
            return ApplicationResult<JobExecutionOutcome>.Success(Outcome);
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
        public Action? OnStart { get; set; }
        public Func<Task>? OnComplete { get; set; }
        public bool FailStart { get; set; }
        public bool FailComplete { get; set; }
        public bool Duplicate { get; set; }
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
            OnStart?.Invoke();
            if (FailStart || Duplicate) return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Failure(
                ApplicationError.BusinessRule(Duplicate ? "OCCURRENCE ALREADY CLAIMED" : "start refused")));
            TargetStarts.Add(target);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(JobRunStatus.Running)));
        }

        public async Task<ApplicationResult<JobRunHistoryDto>> CompleteAsync(
            Guid jobRunHistoryId, JobRunStatus finalStatus, string? message, string? failureReason,
            string? resultSummaryJson, CancellationToken ct)
        {
            Completions.Add(finalStatus);
            if (OnComplete is not null) await OnComplete();
            if (FailComplete) return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.BusinessRule("complete refused"));
            return ApplicationResult<JobRunHistoryDto>.Success(Dto(finalStatus));
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
        public World(JobAdmissionController admission)
        {
            Job = new JobDefinition("PROBE", "Probe", JobDefinitionType.CanonicalRefresh, "Manual", false);
            Job.AssignExecutionPool(JobLaneCodes.Projection, 1);
            if (true)
            {
                Job.AssignTargetDefinition(
                    "Transformation",
                    DefinitionId, JobTargetVersionPolicy.Pinned, 3);
            }

            Orchestrator = new JobRunOrchestratorService(
                new Lookup(Job), Runtime, new NoImport(), new NoQuality(), new NoRisk(),
                new JobExecutionCapabilityAuthority(), new Resolver(), new NoDependencies(),
                new JobExecutorResolver(new IJobExecutor[] { Executor }), admission);
        }

        public JobDefinition Job { get; }
        public Runtime Runtime { get; } = new();
        public StubExecutor Executor { get; } = new();
        public JobRunOrchestratorService Orchestrator { get; }
    }


    private static JobAdmissionController Controller() => new(
        new JobAdmissionTestConfigurationProvider(new JobLaneDefinition(JobLaneCodes.Projection, 1, 2, 4, JobCapacityProvenance.TestConfiguration)),
        NullLogger<JobAdmissionController>.Instance);
    private static void Empty(JobAdmissionController admission)
    {
        var lane = Assert.Single(admission.Snapshot());
        Assert.Equal(0, lane.RunningCount); Assert.Equal(0, lane.ActiveWeight); Assert.Equal(0, lane.QueueDepth);
    }
    private static Task<ApplicationResult<JobActionResponseDto>> Run(World world, bool scheduled = false, CancellationToken ct = default)
        => scheduled ? world.Orchestrator.RunScheduledAsync(world.Job.Id, "occurrence", DateTime.UtcNow, "test", null, ct)
            : world.Orchestrator.RunNowAsync(world.Job.Id, "test", null, ct);

    [Theory]
    [InlineData("start-refused")]
    [InlineData("start-throws")]
    [InlineData("duplicate")]
    [InlineData("complete-refused")]
    [InlineData("complete-throws")]
    [InlineData("executor-throws")]
    [InlineData("cancelled")]
    [InlineData("success")]
    public async Task Real_orchestrator_holds_capacity_through_terminal_paths_and_releases_once(string path)
    {
        var admission = Controller(); var world = new World(admission);
        Action occupied = () => Assert.Equal(1, admission.Snapshot()[0].RunningCount);
        world.Runtime.OnStart = () => { occupied(); if (path == "start-throws") throw new InvalidOperationException("start probe"); };
        world.Runtime.OnComplete = () => { occupied(); if (path == "complete-throws") throw new InvalidOperationException("complete probe"); return Task.CompletedTask; };
        world.Runtime.FailStart = path == "start-refused";
        world.Runtime.Duplicate = path == "duplicate";
        world.Runtime.FailComplete = path == "complete-refused";
        world.Executor.OnExecute = _ => { occupied(); if (path == "executor-throws") throw new InvalidOperationException("execution probe"); return Task.CompletedTask; };
        if (path == "cancelled") world.Executor.Outcome = new(false, true, "cancelled", 0, Array.Empty<JobExecutionBlockResult>(), JobExecutionDiagnosticCodes.Cancelled, null);
        if (path is "start-throws" or "complete-throws")
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(world));
        else
        {
            var result = await Run(world, path == "duplicate");
            Assert.Equal(path is "success" or "cancelled", result.IsSuccess);
        }
        if (path.StartsWith("start", StringComparison.Ordinal) || path == "duplicate") Assert.Empty(world.Executor.Executed);
        if (path == "cancelled") Assert.Single(world.Runtime.Acknowledged);
        Empty(admission);
        await using var next = await admission.AcquireAsync(new(Guid.NewGuid(), "CanonicalRefresh", JobLaneCodes.Projection, new(2)), CancellationToken.None);
        Assert.True(next.IsAdmitted);
    }

    [Fact]
    public async Task Manual_and_scheduled_calls_share_capacity_and_wait_through_completion()
    {
        var admission = Controller(); var manual = new World(admission); var scheduled = new World(admission);
        var completing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manual.Runtime.OnComplete = async () => { completing.SetResult(); await finish.Task; };
        var first = Run(manual); await completing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = Run(scheduled, true);
        try
        {
            Assert.False(second.IsCompleted);
            Assert.Empty(scheduled.Runtime.TargetStarts);
            Assert.Equal(1, admission.Snapshot()[0].QueueDepth);
        }
        finally { finish.TrySetResult(); }
        Assert.True((await first).IsSuccess); Assert.True((await second).IsSuccess); Empty(admission);
    }

    [Fact]
    public async Task Cancelled_waiting_run_never_calls_runtime_Start()
    {
        var admission = Controller(); var world = new World(admission);
        await using var blocker = await admission.AcquireAsync(new(Guid.NewGuid(), "CanonicalRefresh", JobLaneCodes.Projection, new(2)), CancellationToken.None);
        using var stop = new CancellationTokenSource();
        var run = Run(world, false, stop.Token); stop.Cancel();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(JobExecutionDiagnosticCodes.AdmissionCancelled, result.Error!.Code);
        Assert.Empty(world.Runtime.TargetStarts); Assert.Empty(world.Executor.Executed);
        await blocker.DisposeAsync(); Empty(admission);
    }

}
