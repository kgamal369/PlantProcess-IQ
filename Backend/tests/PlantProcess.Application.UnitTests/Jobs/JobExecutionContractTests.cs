using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// T-106 corrective. The behaviours CENTRAL's review found the foundation had
/// never been asked to prove.
/// </summary>
public sealed class JobExecutionContractTests
{
    // ------------------------------------------------------------ enum ------
    [Fact]
    public void Existing_run_status_numbers_did_not_move_and_blocked_is_five()
    {
        Assert.Equal(0, (int)JobRunStatus.NeverRun);
        Assert.Equal(1, (int)JobRunStatus.Running);
        Assert.Equal(2, (int)JobRunStatus.Ok);
        Assert.Equal(3, (int)JobRunStatus.Failed);
        Assert.Equal(4, (int)JobRunStatus.Timeout);
        Assert.Equal(5, (int)JobRunStatus.Blocked);
    }

    [Fact]
    public void A_blocked_history_row_is_born_terminal_and_never_reports_running()
    {
        var history = new JobRunHistory(
            Guid.NewGuid(), "PROBE", "Probe", JobDefinitionType.DataQualityScan,
            "ManualRunNow", "tester", "chain", false, "test", "test");

        history.MarkBlocked("Required upstream never ran.");

        Assert.Equal(JobRunStatus.Blocked, history.Status);
        Assert.NotNull(history.CompletedAtUtc);
        Assert.Equal(0, history.DurationMs);
        Assert.Contains("upstream", history.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_blocked_attempt_becomes_the_jobs_last_run_truth()
    {
        var job = new JobDefinition("PROBE", "Probe", JobDefinitionType.DataQualityScan, "Manual", false);

        job.MarkBlocked("Required upstream failed.");

        Assert.Equal(JobRunStatus.Blocked, job.LastRunStatus);
        Assert.Contains("upstream", job.LastFailureReason, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------ capability ------
    private static readonly JobExecutionCapabilityAuthority Authority = new();

    [Fact]
    public void Every_family_is_classified()
    {
        foreach (JobDefinitionType jobType in Enum.GetValues<JobDefinitionType>())
        {
            JobExecutionCapability answer = Authority.Describe(jobType);
            Assert.Equal(jobType, answer.JobType);
            Assert.False(string.IsNullOrWhiteSpace(answer.Statement));
        }
    }

    [Fact]
    public void Canonical_refresh_is_not_advertised_as_executable_but_keeps_its_target_semantics()
    {
        JobExecutionCapability answer = Authority.Describe(JobDefinitionType.CanonicalRefresh);

        Assert.False(answer.IsExecutableByRuntime);
        Assert.Equal(JobTargetRequirement.Required, answer.TargetRequirement);
        Assert.Equal(DefinitionKind.Transformation, answer.CanonicalTargetKind);
        Assert.True(answer.VersionPolicyApplies);
    }

    [Theory]
    [InlineData(JobDefinitionType.MlParamsVsDefects)]
    [InlineData(JobDefinitionType.MlParamsVsDowntime)]
    [InlineData(JobDefinitionType.MlParamsVsKpis)]
    [InlineData(JobDefinitionType.MlWeeklyFull)]
    public void Learning_families_declare_a_model_target_and_no_executor(JobDefinitionType jobType)
    {
        JobExecutionCapability answer = Authority.Describe(jobType);

        Assert.False(answer.IsExecutableByRuntime);
        Assert.Equal(DefinitionKind.Model, answer.CanonicalTargetKind);
    }

    [Theory]
    [InlineData(JobDefinitionType.DbLinkImport)]
    [InlineData(JobDefinitionType.DataQualityScan)]
    [InlineData(JobDefinitionType.RiskScoring)]
    public void Commissioned_families_stay_executable_and_declare_no_canonical_target(JobDefinitionType jobType)
    {
        JobExecutionCapability answer = Authority.Describe(jobType);

        Assert.True(answer.IsExecutableByRuntime);
        Assert.Equal(JobTargetRequirement.NotUsed, answer.TargetRequirement);
        Assert.Null(answer.CanonicalTargetKind);
        Assert.False(answer.VersionPolicyApplies);
    }

    [Fact]
    public void The_class_policy_is_derived_from_the_capability_and_owns_no_table()
    {
        var policy = new CapabilityJobTargetClassPolicy(Authority);

        JobTargetClassRule refresh = policy.RuleFor(JobDefinitionType.CanonicalRefresh);
        Assert.True(refresh.RequiresTarget);
        Assert.Equal(new[] { DefinitionKind.Transformation }, refresh.PermittedKinds!.ToArray());

        JobTargetClassRule scan = policy.RuleFor(JobDefinitionType.DataQualityScan);
        Assert.False(scan.RequiresTarget);
        Assert.Null(scan.PermittedKinds);
    }

    // ----------------------------------------------- dependency semantics ---
    private static readonly Guid Upstream = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Required_upstream_that_never_ran_blocks_with_no_upstream_run_identity()
    {
        JobDependencyOutcome outcome = JobDependencyEvaluator.Evaluate(
            Upstream, isRequired: true, pinnedVersion: null,
            upstreamRunId: null, upstreamStatus: null, upstreamVersion: null);

        Assert.Equal(JobDependencyResolution.Blocked, outcome.Resolution);
        Assert.True(outcome.BlocksDownstream);
        Assert.Null(outcome.DependsOnRunId);
    }

    [Fact]
    public void Optional_upstream_that_never_ran_is_skipped_and_does_not_block()
    {
        JobDependencyOutcome outcome = JobDependencyEvaluator.Evaluate(
            Upstream, isRequired: false, pinnedVersion: null,
            upstreamRunId: null, upstreamStatus: null, upstreamVersion: null);

        Assert.Equal(JobDependencyResolution.SkippedOptional, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
    }

    [Fact]
    public void Required_upstream_that_failed_this_cycle_is_failed_upstream_and_blocks()
    {
        Guid upstreamRun = Guid.NewGuid();

        JobDependencyOutcome outcome = JobDependencyEvaluator.Evaluate(
            Upstream, isRequired: true, pinnedVersion: null,
            upstreamRunId: upstreamRun, upstreamStatus: JobRunStatus.Failed, upstreamVersion: null);

        Assert.Equal(JobDependencyResolution.FailedUpstream, outcome.Resolution);
        Assert.True(outcome.BlocksDownstream);
        Assert.Equal(upstreamRun, outcome.DependsOnRunId);
    }

    [Fact]
    public void A_pinned_version_mismatch_blocks_and_names_both_versions()
    {
        JobDependencyOutcome outcome = JobDependencyEvaluator.Evaluate(
            Upstream, isRequired: true, pinnedVersion: 7,
            upstreamRunId: Guid.NewGuid(), upstreamStatus: JobRunStatus.Ok, upstreamVersion: 4);

        Assert.Equal(JobDependencyResolution.Blocked, outcome.Resolution);
        Assert.True(outcome.BlocksDownstream);
        Assert.Equal(7, outcome.ExpectedVersion);
        Assert.Equal(4, outcome.ActualVersion);
        Assert.Contains("7", outcome.Reason, StringComparison.Ordinal);
        Assert.Contains("4", outcome.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_successful_upstream_is_satisfied_and_carries_its_run_identity()
    {
        Guid upstreamRun = Guid.NewGuid();

        JobDependencyOutcome outcome = JobDependencyEvaluator.Evaluate(
            Upstream, isRequired: true, pinnedVersion: null,
            upstreamRunId: upstreamRun, upstreamStatus: JobRunStatus.Ok, upstreamVersion: 3);

        Assert.Equal(JobDependencyResolution.Satisfied, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
        Assert.Equal(upstreamRun, outcome.DependsOnRunId);
    }

    [Fact]
    public void No_evaluation_path_produces_stale_accepted()
    {
        var produced = new List<JobDependencyResolution>();

        foreach (bool required in new[] { true, false })
        {
            foreach (int? pinned in new int?[] { null, 5 })
            {
                foreach (JobRunStatus? status in new JobRunStatus?[]
                    { null, JobRunStatus.Ok, JobRunStatus.Failed, JobRunStatus.Timeout, JobRunStatus.Blocked })
                {
                    Guid? runId = status is null ? null : Guid.NewGuid();

                    produced.Add(JobDependencyEvaluator.Evaluate(
                        Upstream, required, pinned, runId, status, 5).Resolution);
                }
            }
        }

        Assert.DoesNotContain(JobDependencyResolution.StaleAccepted, produced);
    }

    [Fact]
    public void The_evidence_row_refuses_to_claim_stale_accepted_or_a_missing_run()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobRunDependency(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Upstream,
            JobDependencyResolution.StaleAccepted, null, null, "no"));

        Assert.Throws<ArgumentException>(() => new JobRunDependency(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Upstream,
            JobDependencyResolution.Satisfied, null, null, "no"));
    }

    // ------------------------------------------------------ orchestration ---
    [Fact]
    public async Task Canonical_refresh_run_now_refuses_with_JX01_and_processes_no_batches()
    {
        var world = new World(JobDefinitionType.CanonicalRefresh);

        var result = await world.Orchestrator.RunNowAsync(
            world.Job.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionErrorCodes.NoExecutorForJobFamily, result.Error!.Code);
        Assert.Empty(world.Runtime.StartedJobCodes);
        Assert.Equal(0, world.Import.Calls);
        Assert.Empty(world.Runtime.BlockedJobIds);
    }

    [Fact]
    public async Task A_commissioned_family_still_executes_its_genuine_path()
    {
        var world = new World(JobDefinitionType.DataQualityScan);

        var result = await world.Orchestrator.RunNowAsync(
            world.Job.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(world.Runtime.StartedJobCodes);
        Assert.Equal(1, world.Quality.Calls);
    }

    // ----------------------------------------------------------- fixtures --
    private sealed class World
    {
        public World(JobDefinitionType jobType)
        {
            Job = new JobDefinition("PROBE_" + jobType, "Probe", jobType, "Manual", false);
            Runtime = new RecordingRuntime();
            Import = new CountingImport();
            Quality = new CountingQuality();

            Orchestrator = new JobRunOrchestratorService(
                new SingleJobLookup(Job), Runtime, Import, Quality, new UnusedRisk(),
                new JobExecutionCapabilityAuthority(), new NoTargetResolver(), new EmptyDependencies());
        }

        public JobDefinition Job { get; }
        public RecordingRuntime Runtime { get; }
        public CountingImport Import { get; }
        public CountingQuality Quality { get; }
        public JobRunOrchestratorService Orchestrator { get; }
    }

    private sealed class SingleJobLookup : IRunnableJobLookup
    {
        private readonly JobDefinition _job;
        public SingleJobLookup(JobDefinition job) { _job = job; }

        public Task<JobDefinition?> FindAsync(Guid jobDefinitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult<JobDefinition?>(_job.Id == jobDefinitionId ? _job : null);
        }
    }

    private sealed class RecordingRuntime : IJobRuntimeService
    {
        public List<string> StartedJobCodes { get; } = new();
        public List<Guid> BlockedJobIds { get; } = new();

        public Task<ApplicationResult<JobRunHistoryDto>> StartAsync(
            string jobCode, string triggerSource, string? triggeredBy, string? correlationId, CancellationToken ct)
        {
            StartedJobCodes.Add(jobCode);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(JobRunStatus.Running, correlationId)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> RecordBlockedAsync(
            Guid jobDefinitionId, string triggerSource, string? triggeredBy, string? correlationId,
            string reason, CancellationToken ct)
        {
            BlockedJobIds.Add(jobDefinitionId);
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(JobRunStatus.Blocked, correlationId)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> CompleteAsync(
            Guid jobRunHistoryId, JobRunStatus finalStatus, string? message, string? failureReason,
            string? resultSummaryJson, CancellationToken ct)
        {
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(Dto(finalStatus, null)));
        }

        public Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
            Guid jobDefinitionId, int take, CancellationToken ct)
        {
            throw new NotSupportedException("Not read by these tests.");
        }

        private static JobRunHistoryDto Dto(JobRunStatus status, string? correlationId)
        {
            return new JobRunHistoryDto(
                Guid.NewGuid(), Guid.NewGuid(), "PROBE", "PROBE", JobDefinitionType.DataQualityScan,
                status, DateTime.UtcNow, DateTime.UtcNow, 0, "ManualRunNow", "tester",
                correlationId, null, null, null);
        }
    }

    private sealed class CountingImport : IImportBatchQueueProcessorService
    {
        public int Calls { get; private set; }

        public Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
            int maxBatches, int rowsPerBatch, bool stopOnFirstError, bool runDataQualityScan, CancellationToken ct)
        {
            Calls = Calls + 1;
            throw new NotSupportedException("The import path must not be reached by these families.");
        }
    }

    private sealed class CountingQuality : IDataQualityService
    {
        public int Calls { get; private set; }

        public Task<ApplicationResult<Guid>> RaiseIssueAsync(RaiseDataQualityIssueCommand c, CancellationToken ct)
        {
            throw new NotSupportedException("Not raised by these tests.");
        }

        public Task<ApplicationResult<DataQualityScanSummary>> RunFullScanAsync(int max, CancellationToken ct)
        {
            Calls = Calls + 1;
            return Task.FromResult(ApplicationResult<DataQualityScanSummary>.Success(
                new DataQualityScanSummary(DateTime.UtcNow, 0, 0, 0, TimeSpan.Zero)));
        }
    }

    private sealed class UnusedRisk : IRiskScoreService
    {
        public Task<ApplicationResult<Guid>> StoreAsync(StoreRiskScoreCommand c, CancellationToken ct)
            => throw new NotSupportedException("Not reached.");

        public Task<ApplicationResult<CalculateRiskScoreResult>> CalculateAsync(CalculateRiskScoreCommand c, CancellationToken ct)
            => throw new NotSupportedException("Not reached.");

        public Task<ApplicationResult<CalculateRiskScoresBatchResult>> CalculateBatchAsync(
            CalculateRiskScoresBatchCommand c, CancellationToken ct)
            => throw new NotSupportedException("Not reached.");
    }

    private sealed class NoTargetResolver : IJobTargetResolver
    {
        public Task<ApplicationResult<JobTargetResolution>> ResolveAsync(
            JobDefinitionType jobClass, JobTargetReference? target, CancellationToken ct)
            => Task.FromResult(ApplicationResult<JobTargetResolution>.Success(JobTargetResolution.None()));

        public Task<ApplicationResult> AssertNotTargetedByJobsAsync(
            DefinitionKind kind, Guid definitionId, CancellationToken ct)
            => Task.FromResult(ApplicationResult.Success());
    }

    private sealed class EmptyDependencies : IJobDependencyService
    {
        public Task<ApplicationResult> AddDependencyAsync(
            Guid a, Guid b, JobDependencyKind k, bool r, int? v, int? s, CancellationToken ct)
            => throw new NotSupportedException("Not written by these tests.");

        public Task<ApplicationResult> RemoveDependencyAsync(Guid a, Guid b, CancellationToken ct)
            => throw new NotSupportedException("Not written by these tests.");

        public Task<ApplicationResult<IReadOnlyList<JobDependencyEdge>>> ListEdgesAsync(CancellationToken ct)
            => throw new NotSupportedException("Not read by these tests.");

        public Task<ApplicationResult<IReadOnlyList<Guid>>> ResolveExecutionOrderAsync(Guid id, CancellationToken ct)
            => Task.FromResult(ApplicationResult<IReadOnlyList<Guid>>.Success(new[] { id }));

        public Task<ApplicationResult<IReadOnlyList<JobDependencyOutcome>>> EvaluateAsync(
            Guid id, IReadOnlyDictionary<Guid, JobRunSnapshot> chainRuns, CancellationToken ct)
            => Task.FromResult(ApplicationResult<IReadOnlyList<JobDependencyOutcome>>.Success(
                Array.Empty<JobDependencyOutcome>()));

        public Task<ApplicationResult> RecordResolutionsAsync(
            Guid runId, Guid jobId, IReadOnlyList<JobDependencyOutcome> outcomes, CancellationToken ct)
            => Task.FromResult(ApplicationResult.Success());
    }
}