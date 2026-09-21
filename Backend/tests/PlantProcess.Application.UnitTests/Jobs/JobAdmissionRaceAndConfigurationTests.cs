using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Application.Jobs.Admission;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// The parts of admission that only a race, a lock boundary or a configuration mistake can
/// expose. Capacities here are TEST_CONFIGURATION and certify behaviour, never sizing.
/// </summary>
[Trait("Gate", "JobAdmission")]
public sealed class JobAdmissionRaceAndConfigurationTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

    private static JobAdmissionController Controller(params JobLaneDefinition[] lanes)
        => new(new JobAdmissionTestConfigurationProvider(lanes), NullLogger<JobAdmissionController>.Instance);

    private static JobLaneDefinition Lane(string code, int maxConcurrency, double capacity, int queueDepth = 8)
        => new(code, maxConcurrency, capacity, queueDepth, JobCapacityProvenance.TestConfiguration);

    private static JobAdmissionRequest Request(string lane, double weight, string family = "DataQualityScan")
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
    public async Task A_heavy_queue_head_is_never_leapfrogged_by_a_lighter_candidate()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 4, 4));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 3),
            CancellationToken.None);

        Task<JobAdmissionDecision> heavyHead = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 4),
            CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));

        Task<JobAdmissionDecision> lightTail = admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 1),
            CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 2, Settle));

        Assert.False(lightTail.IsCompleted, "A lighter candidate must not overtake the queue head.");

        await running.DisposeAsync();

        await using JobAdmissionDecision headAdmitted = await heavyHead;
        Assert.True(headAdmitted.IsAdmitted);
        Assert.False(lightTail.IsCompleted, "The lane is full again; the tail still waits in order.");

        await headAdmitted.DisposeAsync();
        await using JobAdmissionDecision tailAdmitted = await lightTail;
        Assert.True(tailAdmitted.IsAdmitted);
    }

    [Fact]
    public async Task Admission_and_cancellation_have_exactly_one_winner()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 1, 2));

            JobAdmissionDecision running = await admission.AcquireAsync(
                Request(JobLaneCodes.Analysis, 2),
                CancellationToken.None);

            using var cancellation = new CancellationTokenSource();
            Task<JobAdmissionDecision> contested = admission.AcquireAsync(
                Request(JobLaneCodes.Analysis, 2),
                cancellation.Token);

            Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));

            Task release = running.DisposeAsync().AsTask();
            cancellation.Cancel();
            await release.ConfigureAwait(false);

            await using JobAdmissionDecision decision = await contested;

            JobLaneOccupancy lane = admission.Snapshot()[0];

            if (decision.IsAdmitted)
            {
                Assert.Equal(1, lane.RunningCount);
                Assert.Equal(2, lane.ActiveWeight);
            }
            else
            {
                Assert.Equal(JobAdmissionOutcome.Cancelled, decision.Outcome);
                Assert.Equal(0, lane.RunningCount);
                Assert.Equal(0, lane.ActiveWeight);
            }

            Assert.Equal(0, lane.QueueDepth);
        }
    }

    [Fact]
    public async Task A_newly_admitted_caller_may_re_enter_admission_immediately()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Analysis, 1, 2));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Analysis, 2),
            CancellationToken.None);

        Task<bool> queued = Task.Run(async () =>
        {
            await using JobAdmissionDecision admitted = await admission.AcquireAsync(
                Request(JobLaneCodes.Analysis, 2),
                CancellationToken.None);

            // Runs on the continuation of the grant. If the grant completed under the lane
            // lock, this call would deadlock instead of returning a refusal.
            await using JobAdmissionDecision reentrant = await admission.AcquireAsync(
                Request(JobLaneCodes.Analysis, 8),
                CancellationToken.None);

            return reentrant.Outcome == JobAdmissionOutcome.RefusedDemandExceedsLaneCapacity;
        });

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));
        await running.DisposeAsync();

        Task finished = await Task.WhenAny(queued, Task.Delay(Settle));
        Assert.True(ReferenceEquals(finished, queued), "A waiter continuation must never run under the lane lock.");
        Assert.True(await queued);
    }

    [Fact]
    public async Task A_waiting_or_refused_candidate_holds_no_capacity_at_any_moment()
    {
        JobAdmissionController admission = Controller(Lane(JobLaneCodes.Import, 1, 1, queueDepth: 1));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "DbLinkImport"),
            CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        Task<JobAdmissionDecision> queued = admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "DbLinkImport"),
            cancellation.Token);

        Assert.True(await WaitUntilAsync(() => admission.Snapshot()[0].QueueDepth == 1, Settle));

        JobLaneOccupancy whileQueued = admission.Snapshot()[0];
        Assert.Equal(1, whileQueued.RunningCount);
        Assert.Equal(1, whileQueued.ActiveWeight);

        await using JobAdmissionDecision refused = await admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "DbLinkImport"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedQueueFull, refused.Outcome);
        Assert.Null(refused.Lease);

        JobLaneOccupancy afterRefusal = admission.Snapshot()[0];
        Assert.Equal(1, afterRefusal.RunningCount);
        Assert.Equal(1, afterRefusal.ActiveWeight);

        cancellation.Cancel();
        await using JobAdmissionDecision cancelled = await queued;
        Assert.Equal(JobAdmissionOutcome.Cancelled, cancelled.Outcome);
    }

    [Fact]
    public async Task A_family_may_not_run_in_a_lane_other_than_the_one_it_declares()
    {
        JobAdmissionController admission = Controller(
            Lane(JobLaneCodes.Import, 2, 4),
            Lane(JobLaneCodes.Projection, 2, 4));

        await using JobAdmissionDecision wrongLane = await admission.AcquireAsync(
            Request(JobLaneCodes.Import, 1, "CanonicalRefresh"),
            CancellationToken.None);

        await using JobAdmissionDecision rightLane = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 1, "CanonicalRefresh"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedLaneNotPermitted, wrongLane.Outcome);
        Assert.True(rightLane.IsAdmitted);
    }

    [Fact]
    public void The_shipped_configuration_is_complete_canonical_and_truthfully_unqualified()
    {
        var provider = new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions());

        IReadOnlyList<JobLaneDefinition> lanes = provider.GetLanes();

        Assert.Equal(JobLaneCodes.Baseline.Count, lanes.Count);
        Assert.All(lanes, lane => Assert.Empty(lane.Validate()));
        Assert.All(lanes, lane => Assert.Equal(JobCapacityProvenance.UnqualifiedDefault, lane.Provenance));
        Assert.Contains(lanes, lane => lane.LaneCode == JobLaneCodes.MlOnlineScoring && lane.IsHardReserved);
        Assert.DoesNotContain(lanes, lane => lane.Provenance == JobCapacityProvenance.SiteCalibrated);
    }

    [Fact]
    public void An_invalid_lane_configuration_is_refused_at_startup()
    {
        List<JobLaneOptions> broken = JobAdmissionOptionsConfigurationProvider.ShippedDefaults();
        broken[0].MaxConcurrency = 0;

        Assert.Throws<ArgumentException>(
            () => new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions { Lanes = broken }));
    }

    [Fact]
    public void A_workload_that_can_never_fit_its_lane_is_refused_at_startup()
    {
        List<JobLaneOptions> declared = JobAdmissionOptionsConfigurationProvider.ShippedDefaults();
        JobLaneOptions training = declared.Single(lane => lane.LaneCode == JobLaneCodes.MlTraining);
        training.MaxDeclaredComputeWeight = training.ResourceCapacity + 1;

        Assert.Throws<ArgumentException>(
            () => new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions { Lanes = declared }));
    }

    [Fact]
    public void A_missing_canonical_lane_is_refused_at_startup()
    {
        List<JobLaneOptions> declared = JobAdmissionOptionsConfigurationProvider.ShippedDefaults();
        declared.RemoveAll(lane => lane.LaneCode == JobLaneCodes.Report);

        Assert.Throws<ArgumentException>(
            () => new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions { Lanes = declared }));
    }

    [Fact]
    public void The_admission_scope_names_one_runtime_instance_and_never_a_site()
    {
        var provider = new JobAdmissionOptionsConfigurationProvider(
            new JobAdmissionOptions { AdmissionScope = "worker-1" });

        var admission = new JobAdmissionController(provider, NullLogger<JobAdmissionController>.Instance);

        Assert.Equal("worker-1", admission.AdmissionScope);
    }


    [Fact]
    public async Task A_lane_that_allows_no_waiting_admits_now_or_refuses_now()
    {
        JobAdmissionController admission = Controller(
            new JobLaneDefinition(
                JobLaneCodes.Projection,
                1,
                2,
                0,
                JobCapacityProvenance.TestConfiguration,
                IsHardReserved: true));

        await using JobAdmissionDecision running = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 2, "CanonicalRefresh"),
            CancellationToken.None);
        Assert.True(running.IsAdmitted);

        await using JobAdmissionDecision refused = await admission.AcquireAsync(
            Request(JobLaneCodes.Projection, 2, "CanonicalRefresh"),
            CancellationToken.None);

        Assert.Equal(JobAdmissionOutcome.RefusedQueueFull, refused.Outcome);
        Assert.Equal(0, admission.Snapshot()[0].QueueDepth);
        Assert.False(admission.Snapshot()[0].MaxQueueDepth > 0);
    }

    [Fact]
    public void The_lane_catalogue_accepts_a_later_governed_lane_without_reopening_admission()
    {
        var catalogue = new JobLaneCatalogue();
        const string acquisitionLane = "industrial.acquisition";

        Assert.False(catalogue.IsRegistered(acquisitionLane));

        JobLaneRegistration registration = catalogue.Register(acquisitionLane, "acquisition-session-owner");

        Assert.True(catalogue.IsRegistered(acquisitionLane));
        Assert.Equal("acquisition-session-owner", registration.Owner);
        Assert.Contains(catalogue.RequiredLanes(), lane => lane.LaneCode == acquisitionLane);

        // The same owner may re-register; a different owner may never take the lane over.
        Assert.Equal(registration, catalogue.Register(acquisitionLane, "acquisition-session-owner"));
        Assert.Throws<InvalidOperationException>(() => catalogue.Register(acquisitionLane, "someone-else"));

        // Baseline lanes stay exactly as the current design declares them.
        Assert.All(JobLaneCodes.Baseline, lane => Assert.True(catalogue.IsRegistered(lane)));
    }

    [Fact]
    public void A_registered_lane_without_configuration_is_refused_at_startup()
    {
        var catalogue = new JobLaneCatalogue();
        catalogue.Register("industrial.acquisition", "acquisition-session-owner");

        ArgumentException failure = Assert.Throws<ArgumentException>(
            () => new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions(), catalogue));

        Assert.Contains("industrial.acquisition", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unregistered_lane_in_configuration_is_refused_at_startup()
    {
        List<JobLaneOptions> declared = JobAdmissionOptionsConfigurationProvider.ShippedDefaults();
        declared.Add(new JobLaneOptions
        {
            LaneCode = "gpu2",
            MaxConcurrency = 1,
            ResourceCapacity = 1,
            QueueCapacity = 1
        });

        Assert.Throws<ArgumentException>(
            () => new JobAdmissionOptionsConfigurationProvider(
                new JobAdmissionOptions { Lanes = declared },
                new JobLaneCatalogue()));
    }

    [Fact]
    public void Preemptible_is_lane_metadata_and_admission_claims_no_checkpointing()
    {
        var provider = new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions());

        JobLaneDefinition training = provider.GetLanes().Single(lane => lane.LaneCode == JobLaneCodes.MlTraining);
        Assert.True(training.IsPreemptible);

        // The flags IsPreemptible and Preemptible are lane contract metadata and are allowed.
        // A member that claims to perform the act is not.
        string[] forbiddenActions = { "Checkpoint", "Suspend", "Resume", "Yield" };
        string[] forbiddenNames = { "Preempt", "PreemptAsync", "PreemptJob" };

        string[] offenders = typeof(JobAdmissionController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(JobAdmissionController).Namespace)
            .SelectMany(type => type.GetMembers().Select(member => new { Type = type.Name, member.Name }))
            .Where(member =>
                forbiddenActions.Any(token => member.Name.Contains(token, StringComparison.Ordinal)) ||
                forbiddenNames.Contains(member.Name, StringComparer.Ordinal))
            .Select(member => member.Type + "." + member.Name)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Admission carries the lane contract only. Checkpoint, suspend and resume belong to a commissioned " +
            "runtime that can actually do them. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public async Task The_dispatcher_suppresses_a_duplicate_occurrence_within_one_runtime()
    {
        await using var dispatcher = new BoundedJobDispatcher(4, NullLogger<BoundedJobDispatcher>.Instance);
        using var gate = new SemaphoreSlim(0);
        const string occurrence = "job:7f1c/occurrence:2026-09-19T10:00:00Z";

        Assert.True(dispatcher.TryDispatch(occurrence, async _ => await gate.WaitAsync().ConfigureAwait(false)));
        Assert.True(await WaitUntilAsync(() => dispatcher.Outstanding == 1, Settle));

        Assert.False(dispatcher.TryDispatch(occurrence, _ => Task.CompletedTask));
        Assert.Equal(1, dispatcher.SuppressedDuplicates);
        Assert.Equal(1, dispatcher.Outstanding);

        gate.Release();
        Assert.True(await WaitUntilAsync(() => dispatcher.Outstanding == 0, Settle));

        Assert.True(dispatcher.TryDispatch(occurrence, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task A_shutdown_grace_requests_cancellation_and_keeps_supervising_work()
    {
        var dispatcher = new BoundedJobDispatcher(4, NullLogger<BoundedJobDispatcher>.Instance);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(dispatcher.TryDispatch("job:stubborn/occurrence:1", async token =>
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }));

        await started.Task.ConfigureAwait(false);
        await dispatcher.DrainAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);

        Assert.Equal(1, dispatcher.OutstandingAtShutdown);
        Assert.False(dispatcher.AcceptingWork);
        Assert.True(cancelled.Task.IsCompletedSuccessfully, "Outstanding work is cancelled, never abandoned unobserved.");
        Assert.False(dispatcher.TryDispatch("job:stubborn/occurrence:2", _ => Task.CompletedTask));

        await dispatcher.DisposeAsync();
    }

    [Fact]
    public async Task The_dispatcher_bounds_its_outstanding_work_and_observes_every_fault()
    {
        await using var dispatcher = new BoundedJobDispatcher(2, NullLogger<BoundedJobDispatcher>.Instance);
        using var gate = new SemaphoreSlim(0);

        Assert.True(dispatcher.TryDispatch("job:a/occurrence:" + Guid.NewGuid(), async _ => await gate.WaitAsync().ConfigureAwait(false)));
        Assert.True(dispatcher.TryDispatch("job:a/occurrence:" + Guid.NewGuid(), async _ => await gate.WaitAsync().ConfigureAwait(false)));

        Assert.True(await WaitUntilAsync(() => dispatcher.Outstanding == 2, Settle));
        Assert.False(dispatcher.TryDispatch("job:a/occurrence:" + Guid.NewGuid(), _ => Task.CompletedTask), "The outstanding bound must refuse the third dispatch.");
        Assert.Equal(1, dispatcher.RejectedForBound);

        gate.Release(2);
        Assert.True(await WaitUntilAsync(() => dispatcher.Outstanding == 0, Settle));

        Assert.True(dispatcher.TryDispatch("job:a/occurrence:fault", _ => throw new InvalidOperationException("executor fault")));
        Assert.True(await WaitUntilAsync(() => dispatcher.ObservedFaults == 1, Settle));
        Assert.Equal(0, dispatcher.Outstanding);
    }

    [Fact]
    public async Task The_dispatcher_drains_in_flight_work_on_shutdown()
    {
        var dispatcher = new BoundedJobDispatcher(4, NullLogger<BoundedJobDispatcher>.Instance);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(dispatcher.TryDispatch("job:drain/occurrence:1", async token =>
        {
            await Task.Delay(100, token).ConfigureAwait(false);
            finished.TrySetResult();
        }));

        await dispatcher.DisposeAsync();

        Assert.True(finished.Task.IsCompletedSuccessfully, "Shutdown must drain in-flight dispatches.");
        Assert.Equal(0, dispatcher.Outstanding);
    }
    [Fact]
    public async Task An_already_cancelled_candidate_never_reserves_available_capacity()
    {
        var admission = Controller(Lane(JobLaneCodes.Analysis, 1, 4));
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await using var result = await admission.AcquireAsync(Request(JobLaneCodes.Analysis, 1), stop.Token);
        Assert.Equal(JobAdmissionOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, admission.Snapshot()[0].RunningCount);
        Assert.Equal(0, admission.Snapshot()[0].ActiveWeight);
    }

    [Fact]
    public async Task Cancelling_a_heavy_head_grants_the_light_tail_without_an_unrelated_release()
    {
        var admission = Controller(Lane(JobLaneCodes.Analysis, 4, 4));
        await using var active = await admission.AcquireAsync(Request(JobLaneCodes.Analysis, 3), CancellationToken.None);
        using var stop = new CancellationTokenSource();
        var head = admission.AcquireAsync(Request(JobLaneCodes.Analysis, 4), stop.Token);
        var tail = admission.AcquireAsync(Request(JobLaneCodes.Analysis, 1), CancellationToken.None);
        Assert.Equal(2, admission.Snapshot()[0].QueueDepth);
        Assert.False(tail.IsCompleted);
        stop.Cancel();
        await using var cancelled = await head.WaitAsync(Settle);
        await using var granted = await tail.WaitAsync(Settle);
        Assert.Equal(JobAdmissionOutcome.Cancelled, cancelled.Outcome);
        Assert.True(granted.IsAdmitted);
        Assert.Equal(2, admission.Snapshot()[0].RunningCount);
        Assert.Equal(4, admission.Snapshot()[0].ActiveWeight);
    }

    [Fact]
    public async Task Configured_wait_deadline_refuses_without_a_run_or_leaked_queue_entry()
    {
        var opts = new JobAdmissionOptions { AdmissionWaitSeconds = 1 };
        var admission = new JobAdmissionController(new JobAdmissionOptionsConfigurationProvider(opts), NullLogger<JobAdmissionController>.Instance);
        await using var first = await admission.AcquireAsync(Request(JobLaneCodes.Analysis, 4), CancellationToken.None);
        await using var expired = await admission.AcquireAsync(Request(JobLaneCodes.Analysis, 1), CancellationToken.None).WaitAsync(Settle);
        Assert.Equal(JobAdmissionOutcome.WaitExpired, expired.Outcome);
        var lane = admission.Snapshot().Single(x => x.LaneCode == JobLaneCodes.Analysis);
        Assert.Equal(1, lane.RunningCount);
        Assert.Equal(0, lane.QueueDepth);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_finite_or_non_positive_capacity_is_refused(double capacity)
        => Assert.Throws<ArgumentException>(() => Controller(Lane(JobLaneCodes.Analysis, 1, capacity)));

    [Theory]
    [InlineData("MlWeeklyFull")]
    [InlineData("MlParamsVsDefects")]
    [InlineData("MlParamsVsDowntime")]
    [InlineData("MlParamsVsKpis")]
    [InlineData("Custom")]
    public async Task Uncommissioned_families_gain_no_lane_assignment(string family)
    {
        var admission = Controller(Lane(JobLaneCodes.MlTraining, 1, 4));
        await using var refused = await admission.AcquireAsync(Request(JobLaneCodes.MlTraining, 1, family), CancellationToken.None);
        Assert.Equal(JobAdmissionOutcome.RefusedLaneNotPermitted, refused.Outcome);
    }

    [Fact]
    public async Task Cooperative_shutdown_keeps_ownership_until_ignoring_work_actually_exits()
    {
        var dispatcher = new BoundedJobDispatcher(1, NullLogger<BoundedJobDispatcher>.Instance);
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(dispatcher.TryDispatch("stubborn", _ => exit.Task));
        var drain = dispatcher.DrainAsync(TimeSpan.Zero);
        Assert.False(drain.IsCompleted);
        Assert.Equal(1, dispatcher.Outstanding);
        Assert.Equal(1, dispatcher.OutstandingAtShutdown);
        exit.SetResult();
        await drain.WaitAsync(Settle);
        Assert.Equal(0, dispatcher.Outstanding);
        await dispatcher.DisposeAsync();
    }

}
