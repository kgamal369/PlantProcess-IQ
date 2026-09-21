using PlantProcess.Application.Jobs.Admission;
using System;
using System.Collections.Generic;
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
/// T-106. THE ACCEPTANCE THE GRAPH TESTS CANNOT GIVE.
///
/// A deterministic topological order proves the algebra. It does not prove the
/// runtime honours it. These exercise the real RunWithDependenciesAsync and
/// observe the order in which runs are actually STARTED, the correlation they
/// share, and what happens to a successor when its predecessor fails.
/// </summary>
public sealed class JobChainExecutionTests
{
    [Fact]
    public async Task Declaring_C_after_B_after_A_executes_A_then_B_then_C()
    {
        var world = new ChainWorld();

        var result = await world.Orchestrator.RunWithDependenciesAsync(
            world.JobC.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "PROBE_A", "PROBE_B", "PROBE_C" }, world.Runtime.StartedJobCodes.ToArray());
        Assert.Equal(3, result.Value!.Count);
        Assert.Equal(2, world.Dependencies.RecordedOutcomes.Count);
        Assert.Equal(JobDependencyResolution.Satisfied, world.Dependencies.RecordedOutcomes[0].Resolution);
        Assert.Equal(JobDependencyResolution.Satisfied, world.Dependencies.RecordedOutcomes[1].Resolution);
        Assert.True(world.Dependencies.RecordedOutcomes[0].DependsOnRunId.HasValue);
        Assert.True(world.Dependencies.RecordedOutcomes[1].DependsOnRunId.HasValue);
    }

    [Fact]
    public async Task Every_job_in_one_chain_shares_one_correlation()
    {
        var world = new ChainWorld();

        await world.Orchestrator.RunWithDependenciesAsync(
            world.JobC.Id, "tester", "chain-correlation", CancellationToken.None);

        Assert.Equal(3, world.Runtime.Correlations.Count);
        foreach (string correlation in world.Runtime.Correlations)
        {
            Assert.Equal("chain-correlation", correlation);
        }
    }

    [Fact]
    public async Task A_predecessor_that_fails_stops_the_chain_before_its_dependent_starts()
    {
        var world = new ChainWorld();
        world.Quality.FailOnCallNumber = 2;

        var result = await world.Orchestrator.RunWithDependenciesAsync(
            world.JobC.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(new[] { "PROBE_A", "PROBE_B" }, world.Runtime.StartedJobCodes.ToArray());
        Assert.DoesNotContain("PROBE_C", world.Runtime.StartedJobCodes);
        Assert.Contains(world.JobC.Id, world.Runtime.BlockedJobIds);
        Assert.True(world.Dependencies.RecordedOutcomes.Count >= 2);
        JobDependencyOutcome finalOutcome = world.Dependencies.RecordedOutcomes[world.Dependencies.RecordedOutcomes.Count - 1];
        Assert.Equal(world.JobB.Id, finalOutcome.DependsOnJobDefinitionId);
        Assert.Equal(JobDependencyResolution.FailedUpstream, finalOutcome.Resolution);
        Assert.True(finalOutcome.BlocksDownstream);
        Assert.True(finalOutcome.DependsOnRunId.HasValue);
    }

    [Fact]
    public async Task An_unsupported_family_is_refused_before_any_run_record_is_opened()
    {
        var world = new ChainWorld();
        world.Lookup.Replace(new JobDefinition(
            "PROBE_ML", "Probe ML", JobDefinitionType.MlWeeklyFull, "Manual", false));

        var target = world.Lookup.Only();

        var result = await world.Orchestrator.RunNowAsync(
            target.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionErrorCodes.NoExecutorForJobFamily, result.Error!.Code);
        Assert.Empty(world.Runtime.StartedJobCodes);
    }

    // ----------------------------------------------------------------- world --
    private sealed class ChainWorld
    {
        public ChainWorld()
        {
            JobA = new JobDefinition("PROBE_A", "Probe A", JobDefinitionType.DataQualityScan, "Manual", false);
            JobB = new JobDefinition("PROBE_B", "Probe B", JobDefinitionType.DataQualityScan, "Manual", false);
            JobC = new JobDefinition("PROBE_C", "Probe C", JobDefinitionType.DataQualityScan, "Manual", false);

            foreach (var job in new[] { JobA, JobB, JobC }) JobLaneAssignment.Initialize(job);
            Lookup = new FakeRunnableJobLookup(JobA, JobB, JobC);
            Runtime = new RecordingJobRuntimeService();
            Quality = new RecordingDataQualityService();

            Dependencies = new FixedOrderDependencyService(new[] { JobA.Id, JobB.Id, JobC.Id });

            Orchestrator = new JobRunOrchestratorService(
                Lookup,
                Runtime,
                new NeverCalledImportProcessor(),
                Quality,
                new NeverCalledRiskScoreService(),
                new JobExecutionCapabilityAuthority(),
                new NoTargetResolver(),
                Dependencies,
                new JobExecutorResolver(Array.Empty<IJobExecutor>()),
                new JobAdmissionController(new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<JobAdmissionController>.Instance));
        }

        public JobDefinition JobA { get; }
        public JobDefinition JobB { get; }
        public JobDefinition JobC { get; }
        public FakeRunnableJobLookup Lookup { get; }
        public RecordingJobRuntimeService Runtime { get; }
        public RecordingDataQualityService Quality { get; }
        public FixedOrderDependencyService Dependencies { get; }
        public JobRunOrchestratorService Orchestrator { get; }
    }

    private sealed class FakeRunnableJobLookup : IRunnableJobLookup
    {
        private readonly List<JobDefinition> _jobs;

        public FakeRunnableJobLookup(params JobDefinition[] jobs)
        {
            _jobs = new List<JobDefinition>(jobs);
        }

        public void Replace(JobDefinition only)
        {
            _jobs.Clear();
            _jobs.Add(only);
        }

        public JobDefinition Only()
        {
            return _jobs[0];
        }

        public Task<JobDefinition?> FindAsync(Guid jobDefinitionId, CancellationToken cancellationToken)
        {
            foreach (JobDefinition job in _jobs)
            {
                if (job.Id == jobDefinitionId)
                {
                    return Task.FromResult<JobDefinition?>(job);
                }
            }

            return Task.FromResult<JobDefinition?>(null);
        }
    }

    private sealed class RecordingJobRuntimeService : IJobRuntimeService
    {
        public List<string> StartedJobCodes { get; } = new();
        public List<string> Correlations { get; } = new();
        public List<Guid> BlockedJobIds { get; } = new();

        public Task<ApplicationResult<JobRunHistoryDto>> StartAsync(
            string jobCode, string triggerSource, string? triggeredBy, string? correlationId, CancellationToken cancellationToken)
        {
            StartedJobCodes.Add(jobCode);
            Correlations.Add(correlationId ?? string.Empty);

            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(
                new JobRunHistoryDto(
                    Guid.NewGuid(), Guid.NewGuid(), jobCode, jobCode, JobDefinitionType.DataQualityScan,
                    JobRunStatus.Running, DateTime.UtcNow, null, null, triggerSource, triggeredBy,
                    correlationId, null, null, null)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> StartForTargetAsync(
            string jobCode, string? occurrenceKey, DateTime? nominalAtUtc, string triggerSource,
            string? triggeredBy, string? correlationId,
            PlantProcess.Application.Jobs.Targeting.ResolvedJobTarget target, CancellationToken cancellationToken)
        {
            // T-261. These chains register no executor, so a governed start is never taken.
            // It is implemented rather than defaulted so the semantic stays compile-time visible.
            StartedJobCodes.Add(jobCode);
            Correlations.Add(correlationId ?? string.Empty);

            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(
                new JobRunHistoryDto(
                    Guid.NewGuid(), Guid.NewGuid(), jobCode, jobCode, JobDefinitionType.CanonicalRefresh,
                    JobRunStatus.Running, DateTime.UtcNow, null, null, triggerSource, triggeredBy,
                    correlationId, null, null, null)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> RecordBlockedAsync(
            Guid jobDefinitionId,
            string triggerSource,
            string? triggeredBy,
            string? correlationId,
            string reason,
            CancellationToken cancellationToken)
        {
            BlockedJobIds.Add(jobDefinitionId);

            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(
                new JobRunHistoryDto(
                    Guid.NewGuid(), jobDefinitionId, "BLOCKED", "BLOCKED", JobDefinitionType.DataQualityScan,
                    JobRunStatus.Blocked, DateTime.UtcNow, DateTime.UtcNow, 0, triggerSource, triggeredBy,
                    correlationId, reason, reason, null)));
        }

        public Task<ApplicationResult<JobRunHistoryDto>> CompleteAsync(
            Guid jobRunHistoryId, JobRunStatus finalStatus, string? message, string? failureReason,
            string? resultSummaryJson, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Success(
                new JobRunHistoryDto(
                    jobRunHistoryId, Guid.NewGuid(), "PROBE", "PROBE", JobDefinitionType.DataQualityScan,
                    finalStatus, DateTime.UtcNow, DateTime.UtcNow, 0, "ManualRunNow", null, null,
                    failureReason, message, resultSummaryJson)));
        }

        public Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
            Guid jobDefinitionId, int take, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The chain tests never read run history.");
        }
    }

    private sealed class RecordingDataQualityService : IDataQualityService
    {
        private int _calls;

        public int FailOnCallNumber { get; set; }

        public Task<ApplicationResult<Guid>> RaiseIssueAsync(
            RaiseDataQualityIssueCommand command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The chain tests never raise issues.");
        }

        public Task<ApplicationResult<DataQualityScanSummary>> RunFullScanAsync(
            int maxCandidatesPerRule, CancellationToken cancellationToken)
        {
            _calls = _calls + 1;

            if (FailOnCallNumber == _calls)
            {
                return Task.FromResult(ApplicationResult<DataQualityScanSummary>.Failure(
                    ApplicationError.BusinessRule("Probe failure on call " + _calls + ".")));
            }

            return Task.FromResult(ApplicationResult<DataQualityScanSummary>.Success(
                new DataQualityScanSummary(DateTime.UtcNow, 0, 0, 0, TimeSpan.Zero)));
        }
    }

    private sealed class NeverCalledImportProcessor : IImportBatchQueueProcessorService
    {
        public Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
            int maxBatches, int rowsPerBatch, bool stopOnFirstError, bool runDataQualityScan, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The import executor must not be reached by a data-quality chain.");
        }
    }

    private sealed class NeverCalledRiskScoreService : IRiskScoreService
    {
        public Task<ApplicationResult<Guid>> StoreAsync(StoreRiskScoreCommand command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The risk executor must not be reached by a data-quality chain.");
        }

        public Task<ApplicationResult<CalculateRiskScoreResult>> CalculateAsync(
            CalculateRiskScoreCommand command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The risk executor must not be reached by a data-quality chain.");
        }

        public Task<ApplicationResult<CalculateRiskScoresBatchResult>> CalculateBatchAsync(
            CalculateRiskScoresBatchCommand command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The risk executor must not be reached by a data-quality chain.");
        }
    }

    private sealed class NoTargetResolver : IJobTargetResolver
    {
        public Task<ApplicationResult<JobTargetResolution>> ResolveAsync(
            JobDefinitionType jobClass, JobTargetReference? target, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApplicationResult<JobTargetResolution>.Success(JobTargetResolution.None()));
        }

        public Task<ApplicationResult> AssertNotTargetedByJobsAsync(
            DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApplicationResult.Success());
        }
    }

    private sealed class FixedOrderDependencyService : IJobDependencyService
    {
        private readonly IReadOnlyList<Guid> _order;

        public FixedOrderDependencyService(IReadOnlyList<Guid> order)
        {
            _order = order;
        }

        public List<JobDependencyOutcome> RecordedOutcomes { get; } = new();
        public List<Guid> RecordedRunIds { get; } = new();

        public Task<ApplicationResult> AddDependencyAsync(
            Guid jobDefinitionId,
            Guid dependsOnJobDefinitionId,
            JobDependencyKind dependencyKind,
            bool isRequired,
            int? dependsOnVersion,
            int? stalenessToleranceMinutes,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The chain tests do not write edges.");
        }

        public Task<ApplicationResult> RemoveDependencyAsync(Guid jobDefinitionId, Guid dependsOnJobDefinitionId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The chain tests do not write edges.");
        }

        public Task<ApplicationResult<IReadOnlyList<JobDependencyEdge>>> ListEdgesAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The chain tests do not read edges.");
        }

        public Task<ApplicationResult<IReadOnlyList<Guid>>> ResolveExecutionOrderAsync(
            Guid jobDefinitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApplicationResult<IReadOnlyList<Guid>>.Success(_order));
        }

        public Task<ApplicationResult<IReadOnlyList<JobDependencyOutcome>>> EvaluateAsync(
            Guid jobDefinitionId,
            IReadOnlyDictionary<Guid, JobRunSnapshot> chainRuns,
            CancellationToken cancellationToken)
        {
            int index = -1;
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i] == jobDefinitionId)
                {
                    index = i;
                    break;
                }
            }

            if (index <= 0)
            {
                return Task.FromResult(
                    ApplicationResult<IReadOnlyList<JobDependencyOutcome>>.Success(
                        Array.Empty<JobDependencyOutcome>()));
            }

            Guid predecessorId = _order[index - 1];
            chainRuns.TryGetValue(predecessorId, out JobRunSnapshot? predecessor);

            JobDependencyOutcome outcome = JobDependencyEvaluator.Evaluate(
                predecessorId,
                isRequired: true,
                pinnedVersion: null,
                upstreamRunId: predecessor?.RunId,
                upstreamStatus: predecessor?.Status,
                upstreamVersion: predecessor?.TargetDefinitionVersion);

            return Task.FromResult(
                ApplicationResult<IReadOnlyList<JobDependencyOutcome>>.Success(
                    new[] { outcome }));
        }

        public Task<ApplicationResult> RecordResolutionsAsync(
            Guid runId,
            Guid jobDefinitionId,
            IReadOnlyList<JobDependencyOutcome> outcomes,
            CancellationToken cancellationToken)
        {
            RecordedRunIds.Add(runId);
            foreach (JobDependencyOutcome outcome in outcomes)
            {
                RecordedOutcomes.Add(outcome);
            }

            return Task.FromResult(ApplicationResult.Success());
        }
    }
}