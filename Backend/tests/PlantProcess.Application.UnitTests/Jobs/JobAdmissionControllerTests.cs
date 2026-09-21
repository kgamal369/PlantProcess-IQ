using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Application.Jobs.Admission;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// Admission behaviour. Every capacity in this file is TEST_CONFIGURATION: it proves the
/// law, never a site sizing. Nothing here touches a database, a scheduler or an executor.
/// </summary>
[Trait("Gate", "JobAdmission")]
public sealed class JobAdmissionControllerTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

    private static JobAdmissionController Controller(params JobLaneDefinition[] lanes)
        => new(new JobAdmissionTestConfigurationProvider(lanes), NullLogger<JobAdmissionController>.Instance);

    private static JobLaneDefinition Lane(
        string code,
        int maxConcurrency,
        double capacity,
        int queueDepth = 4,
        bool hardReserved = false)
        => new(code, maxConcurrency, capacity, queueDepth, JobCapacityProvenance.TestConfiguration, hardReserved);

    private static JobAdmissionRequest Request(
        string lane,
        double weight,
        string family = "DataQualityScan")
        => new(Guid.NewGuid(), family, lane, new JobResourceDemand(weight));

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        return condition();
    }

    [Fact]
    public async Task A_light_job_is_admitted_immediately()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 2, 4));

        await using JobAdmissionDecision decision = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);

        Assert.True(decision.IsAdmitted);
        Assert.NotNull(decision.Lease);
        Assert.Equal(TimeSpan.Zero, decision.QueueWait);
    }

    [Fact]
    public async Task A_candidate_waits_when_the_concurrency_bound_is_reached()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 1, 100));

        await using JobAdmissionDecision first = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);
        Assert.True(first.IsAdmitted);

        Task<JobAdmissionDecision> second = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        Assert.False(second.IsCompleted);

        await first.DisposeAsync();

        await using JobAdmissionDecision admitted = await second;
        Assert.True(admitted.IsAdmitted);
    }

    [Fact]
    public async Task A_candidate_waits_when_the_resource_bound_is_reached()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 10, 4));

        await using JobAdmissionDecision heavy = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 3, "DataQualityScan"),
            CancellationToken.None);
        Assert.True(heavy.IsAdmitted);

        Task<JobAdmissionDecision> second = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 3, "DataQualityScan"),
            CancellationToken.None);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        Assert.False(second.IsCompleted);

        await heavy.DisposeAsync();
        await using JobAdmissionDecision admitted = await second;
        Assert.True(admitted.IsAdmitted);
    }

    [Fact]
    public async Task Free_concurrency_with_insufficient_resource_admits_nothing()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 4, 4));

        await using JobAdmissionDecision held = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 3.5),
            CancellationToken.None);
        Assert.True(held.IsAdmitted);

        Task<JobAdmissionDecision> candidate = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        Assert.False(candidate.IsCompleted);
        Assert.Equal(1, admission.Snapshot()[0].RunningCount);

        await held.DisposeAsync();
        await using JobAdmissionDecision _ = await candidate;
    }

    [Fact]
    public async Task Free_resource_with_the_concurrency_bound_reached_admits_nothing()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 1, 100));

        await using JobAdmissionDecision held = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 0.5),
            CancellationToken.None);

        Task<JobAdmissionDecision> candidate = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 0.5),
            CancellationToken.None);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        Assert.False(candidate.IsCompleted);

        await held.DisposeAsync();
        await using JobAdmissionDecision _ = await candidate;
    }

    [Fact]
    public async Task A_candidate_heavier_than_its_lane_is_refused_by_name_and_never_queued()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 2, 4));

        await using JobAdmissionDecision decision = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 8),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedDemandExceedsLaneCapacity, decision.Outcome);
        Assert.Equal(PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionDemandExceedsLaneCapacity, decision.DiagnosticCode);
        Assert.Null(decision.Lease);
        Assert.Equal(0, admission.Snapshot()[0].QueueDepth);
    }

    [Fact]
    public async Task The_queue_bound_is_enforced_with_a_typed_backpressure_refusal()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Import, 1, 1, queueDepth: 1));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "DbLinkImport"),
            CancellationToken.None);

        using var queuedCancellation = new CancellationTokenSource();
        Task<JobAdmissionDecision> queued = admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "DbLinkImport"),
            queuedCancellation.Token);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));

        await using JobAdmissionDecision refused = await admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "DbLinkImport"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedQueueFull, refused.Outcome);
        Assert.Equal(PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionQueueFull, refused.DiagnosticCode);

        queuedCancellation.Cancel();
        await using JobAdmissionDecision cancelled = await queued;
        Assert.Equal(JobAdmissionOutcome.Cancelled, cancelled.Outcome);
    }

    [Fact]
    public async Task Cancelling_a_queued_candidate_consumes_no_capacity_and_creates_no_run()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 1, 4));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        Task<JobAdmissionDecision> queued = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            cancellation.Token);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        cancellation.Cancel();

        await using JobAdmissionDecision decision = await queued;

        Assert.Equal(JobAdmissionOutcome.Cancelled, decision.Outcome);
        Assert.Null(decision.Lease);

        JobLaneOccupancy lane = admission.Snapshot()[0];
        Assert.Equal(1, lane.RunningCount);
        Assert.Equal(1, lane.ActiveWeight);
        Assert.Equal(0, lane.QueueDepth);
    }

    [Fact]
    public async Task A_lease_releases_its_reservation_exactly_once()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 2, 4));

        JobAdmissionDecision decision = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 2),
            CancellationToken.None);

        await decision.DisposeAsync();
        await decision.DisposeAsync();
        await decision.Lease!.DisposeAsync();

        JobLaneOccupancy lane = admission.Snapshot()[0];
        Assert.Equal(0, lane.RunningCount);
        Assert.Equal(0, lane.ActiveWeight);
        Assert.True(decision.Lease!.IsReleased);
    }

    [Fact]
    public async Task An_executor_exception_still_releases_the_reservation_exactly_once()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Projection, 1, 4));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using JobAdmissionDecision decision = await admission.AcquireAsync(
                Request(JobLaneCodes.Projection, 2, "CanonicalRefresh"),
                CancellationToken.None);

            Assert.True(decision.IsAdmitted);
            throw new InvalidOperationException("The executor failed while holding capacity.");
        });

        JobLaneOccupancy lane = admission.Snapshot()[0];
        Assert.Equal(0, lane.RunningCount);
        Assert.Equal(0, lane.ActiveWeight);
    }

    [Fact]
    public async Task A_cancelled_run_releases_its_reservation_exactly_once()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Projection, 1, 4));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await using JobAdmissionDecision decision = await admission.AcquireAsync(
                Request(JobLaneCodes.Projection, 2, "CanonicalRefresh"),
                CancellationToken.None);

            Assert.True(decision.IsAdmitted);
            throw new OperationCanceledException("The run was cancelled while holding capacity.");
        });

        Assert.Equal(0, admission.Snapshot()[0].RunningCount);
    }

    [Fact]
    public async Task A_compute_weight_is_never_counted_as_a_concurrency_slot()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 4, 4));

        await using JobAdmissionDecision first = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 2),
            CancellationToken.None);
        await using JobAdmissionDecision second = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 2),
            CancellationToken.None);

        JobLaneOccupancy lane = admission.Snapshot()[0];

        Assert.Equal(2, lane.RunningCount);
        Assert.Equal(4, lane.ActiveWeight);
        Assert.True(lane.RunningCount < lane.MaxConcurrency, "Concurrency is still free while the resource pool is full.");

        Task<JobAdmissionDecision> third = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        Assert.False(third.IsCompleted);

        await second.DisposeAsync();
        await using JobAdmissionDecision _ = await third;
    }

    [Fact]
    public async Task Training_can_never_consume_the_online_scoring_reservation()
    {
        JobAdmissionController admission = Controller(
            Lane(JobLaneCodes.MlTraining, 1, 4),
            Lane(JobLaneCodes.MlOnlineScoring, 2, 4, hardReserved: true));

        await using JobAdmissionDecision decision = await admission.AcquireAsync(
            Request(JobLaneCodes.MlOnlineScoring, 1, "Training"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedLaneNotPermitted, decision.Outcome);
        Assert.Equal(PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionLaneNotPermitted, decision.DiagnosticCode);
    }

    [Fact]
    public async Task Batch_scoring_can_never_enter_the_online_scoring_lane()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.MlOnlineScoring, 2, 4, hardReserved: true));

        await using JobAdmissionDecision decision = await admission.AcquireAsync(
            Request(JobLaneCodes.MlOnlineScoring, 1, "BatchScoring"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedLaneNotPermitted, decision.Outcome);
    }

    [Fact]
    public async Task Projection_admits_while_import_and_analysis_are_saturated()
    {
        JobAdmissionController admission = Controller(
            Lane(JobLaneCodes.Import, 1, 2),
            Lane(JobLaneCodes.Analysis, 1, 2),
            Lane(JobLaneCodes.Projection, 2, 4, hardReserved: true));

        await using JobAdmissionDecision training = await admission.AcquireAsync(
            Request(JobLaneCodes.Import, 2, "DbLinkImport"),
            CancellationToken.None);
        await using JobAdmissionDecision batch = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 2, "DataQualityScan"),
            CancellationToken.None);

        Assert.True(training.IsAdmitted);
        Assert.True(batch.IsAdmitted);

        await using JobAdmissionDecision online = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 2, "CanonicalRefresh"),
            CancellationToken.None);

        Assert.True(online.IsAdmitted, "Each commissioned lane has independent capacity.");
    }

    [Fact]
    public async Task An_online_only_family_may_not_borrow_another_lane()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 2, 4));

        await using JobAdmissionDecision decision = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1, "OnlineScoring"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedLaneNotPermitted, decision.Outcome);
    }

    [Fact]
    public async Task Eligible_non_ml_jobs_run_concurrently_when_the_lane_allows_it()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Projection, 3, 9));

        await using JobAdmissionDecision first = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 1, "CanonicalRefresh"),
            CancellationToken.None);
        await using JobAdmissionDecision second = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 1, "CanonicalRefresh"),
            CancellationToken.None);
        await using JobAdmissionDecision third = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 1, "CanonicalRefresh"),
            CancellationToken.None);

        Assert.True(first.IsAdmitted);
        Assert.True(second.IsAdmitted);
        Assert.True(third.IsAdmitted);
        Assert.Equal(3, admission.Snapshot()[0].RunningCount);
    }

    [Fact]
    public async Task Neither_bound_is_ever_exceeded_under_a_saturating_burst()
    {
        JobLaneDefinition lane = Lane(JobLaneCodes.Analysis, 3, 6, queueDepth: 40);
        JobAdmissionController admission = Controller(lane);

        int peakRunning = 0;
        double peakWeight = 0;
        int peakQueue = 0;

        async Task RunOne()
        {
            await using JobAdmissionDecision decision = await admission.AcquireAsync(
                Request(JobLaneCodes.Analysis, 2),
                CancellationToken.None);

            if (!decision.IsAdmitted)
            {
                return;
            }

            JobLaneOccupancy occupancy = admission.Snapshot()[0];
            peakRunning = Math.Max(peakRunning, occupancy.RunningCount);
            peakWeight = Math.Max(peakWeight, occupancy.ActiveWeight);
            peakQueue = Math.Max(peakQueue, occupancy.QueueDepth);

            await Task.Delay(20).ConfigureAwait(false);
        }

        var runs = new List<Task>();
        for (int index = 0; index < 30; index++)
        {
            runs.Add(RunOne());
        }

        foreach (Task run in runs)
        {
            await run.ConfigureAwait(false);
        }

        Assert.True(peakRunning <= lane.MaxConcurrency, "Concurrency bound exceeded: " + peakRunning);
        Assert.True(peakWeight <= lane.ResourceCapacity, "Resource bound exceeded: " + peakWeight);
        Assert.True(peakQueue <= lane.MaxQueueDepth, "Queue bound exceeded: " + peakQueue);
        Assert.Equal(0, admission.Snapshot()[0].RunningCount);
        Assert.Equal(0, admission.Snapshot()[0].ActiveWeight);
    }

    [Fact]
    public async Task Waiting_candidates_are_admitted_first_in_first_out_inside_a_lane()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 1, 4));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);

        var order = new List<int>();
        var waiters = new List<Task>();

        for (int index = 0; index < 3; index++)
        {
            int position = index;
            waiters.Add(Task.Run(async () =>
            {
                await using JobAdmissionDecision decision = await admission.AcquireAsync(
                    Request(JobLaneCodes.Analysis, 1),
                    CancellationToken.None);

                lock (order)
                {
                    order.Add(position);
                }
            }));

            Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == position + 1, Settle));
        }

        await running.DisposeAsync();
        await Task.WhenAll(waiters).ConfigureAwait(false);

        Assert.Equal(new[] { 0, 1, 2 }, order);
    }

    [Fact]
    public async Task An_unknown_lane_and_an_invalid_demand_are_typed_refusals()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 2, 4));

        await using JobAdmissionDecision unknownLane = await admission.AcquireAsync(
            new JobAdmissionRequest(Guid.NewGuid(), "DataQualityScan", JobLaneCodes.Report, new JobResourceDemand(1)),
            CancellationToken.None);

        await using JobAdmissionDecision invalid = await admission.AcquireAsync(
            new JobAdmissionRequest(Guid.NewGuid(), "DataQualityScan", JobLaneCodes.Analysis, new JobResourceDemand(0)),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedUnknownLane, unknownLane.Outcome);
        Assert.Equal(PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionUnknownLane, unknownLane.DiagnosticCode);
        Assert.Equal(JobAdmissionOutcome.RefusedInvalidRequest, invalid.Outcome);
        Assert.Equal(PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionInvalidRequest, invalid.DiagnosticCode);
    }

    [Fact]
    public void Every_configured_lane_is_canonical_and_labelled_as_test_configuration()
    {
        foreach (JobLaneDefinition lane in JobAdmissionTestConfigurationProvider.Default())
        {
            Assert.Empty(lane.Validate());
            Assert.True(JobLaneCodes.IsBaseline(lane.LaneCode));
            Assert.Equal(JobCapacityProvenance.TestConfiguration, lane.Provenance);
        }

        Assert.Equal(JobLaneCodes.Baseline.Count, JobAdmissionTestConfigurationProvider.Default().Count);
    }
}
