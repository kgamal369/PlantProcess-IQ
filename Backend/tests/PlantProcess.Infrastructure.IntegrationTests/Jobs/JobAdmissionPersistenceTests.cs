using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Jobs.Admission;
using PlantProcess.Application.Jobs.Dependencies;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Scheduling;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Jobs;

[Collection("CanonicalDefinitionStore")]
[Trait("Gate", "JobAdmissionDatabase")]
public sealed class JobAdmissionPersistenceTests(DefinitionStoreFixture fixture)
{
    private static readonly CancellationToken None = CancellationToken.None;
    private async Task<Guid> Create(JobDefinitionType kind, string schedule = "Manual")
    {
        await using var db = fixture.NewContext();
        var code = "ADMIT_" + Guid.NewGuid().ToString("N");
        var saved = await new JobDefinitionService(db).CreateJobAsync(new(
            code, code, kind, null, null, schedule, true, null, false, "admission-test", code), None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);
        return saved.Value!.Id;
    }

    [Theory]
    [InlineData(JobDefinitionType.DbLinkImport, "import")]
    [InlineData(JobDefinitionType.CanonicalRefresh, "projection")]
    [InlineData(JobDefinitionType.DataQualityScan, "analysis")]
    [InlineData(JobDefinitionType.RiskScoring, "analysis")]
    [InlineData(JobDefinitionType.MlWeeklyFull, null)]
    public async Task Jobs_created_after_migration_persist_only_lawful_defaults(JobDefinitionType kind, string? lane)
    {
        var id = await Create(kind);
        await using var db = fixture.NewContext();
        var persisted = await db.JobDefinitions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(lane, persisted.PoolCode); Assert.Equal(1, persisted.ComputeWeight);
    }

    [Fact]
    public async Task Updating_a_name_preserves_weight_and_incompatible_family_change_refuses()
    {
        var id = await Create(JobDefinitionType.DataQualityScan);
        await using var db = fixture.NewContext();
        var job = await db.JobDefinitions.SingleAsync(x => x.Id == id);
        job.AssignExecutionPool("analysis", 2.5); await db.SaveChangesAsync();
        var service = new JobDefinitionService(db);
        var same = await service.UpdateJobAsync(id, new("renamed", JobDefinitionType.RiskScoring, null, null, "Manual", true, null), None);
        Assert.True(same.IsSuccess); Assert.Equal(2.5, job.ComputeWeight);
        var refused = await service.UpdateJobAsync(id, new("wrong", JobDefinitionType.DbLinkImport, null, null, "Manual", true, null), None);
        Assert.True(refused.IsFailure); Assert.Equal(JobDefinitionType.RiskScoring, job.JobType);
        await using var proof = fixture.NewContext();
        var row = await proof.JobDefinitions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("analysis", row.PoolCode); Assert.Equal(2.5, row.ComputeWeight);
    }

    [Fact]
    public async Task Newly_created_job_runs_through_real_runtime_and_duplicate_occurrence_releases_capacity()
    {
        var id = await Create(JobDefinitionType.DataQualityScan);
        var admission = new JobAdmissionController(new JobAdmissionOptionsConfigurationProvider(new()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<JobAdmissionController>.Instance);
        async Task<ApplicationResult<JobActionResponseDto>> Execute(bool scheduled)
        {
            await using var db = fixture.NewContext();
            var quality = Substitute.For<IDataQualityService>();
            quality.RunFullScanAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
                Task.FromResult(ApplicationResult<DataQualityScanSummary>.Success(new(DateTime.UtcNow, 0, 0, 0, TimeSpan.Zero))));
            var authority = new JobExecutionCapabilityAuthority();
            var resolver = Substitute.For<IJobTargetResolver>();
            resolver.ResolveAsync(Arg.Any<JobDefinitionType>(), Arg.Any<JobTargetReference?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(ApplicationResult<JobTargetResolution>.Success(JobTargetResolution.None())));
            var orchestrator = new JobRunOrchestratorService(new RunnableJobLookup(db), new JobRuntimeService(db),
                Substitute.For<IImportBatchQueueProcessorService>(), quality, Substitute.For<IRiskScoreService>(),
                authority, resolver, Substitute.For<IJobDependencyService>(), new JobExecutorResolver(Array.Empty<IJobExecutor>()), admission);
            return scheduled ? await orchestrator.RunScheduledAsync(id, "admission-occurrence-" + id, DateTime.UtcNow, "test", null, None)
                : await orchestrator.RunNowAsync(id, "test", null, None);
        }
        Assert.True((await Execute(false)).IsSuccess);
        Assert.True((await Execute(true)).IsSuccess);
        var duplicate = await Execute(true);
        Assert.True(duplicate.IsFailure); Assert.Contains("OCCURRENCE ALREADY CLAIMED", duplicate.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(admission.Snapshot(), x => { Assert.Equal(0, x.RunningCount); Assert.Equal(0, x.ActiveWeight); });
        await using var verify = fixture.NewContext();
        Assert.Equal(2, await verify.JobRunHistories.CountAsync(x => x.JobDefinitionId == id));
    }

    [Fact]
    public async Task Registered_dispatcher_shares_one_bound_across_polls_and_isolates_execution_scopes()
    {
        await using var control = fixture.NewContext();
        var enabledBefore = await control.JobDefinitions.Where(x => x.IsEnabled).Select(x => x.Id).ToArrayAsync();
        await control.JobDefinitions.Where(x => x.IsEnabled).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsEnabled, false));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contexts = new System.Collections.Concurrent.ConcurrentBag<PlantProcessDbContext>();
        var disposed = new System.Collections.Concurrent.ConcurrentBag<ScopeProof>();
        int count = 0;
        try
        {
            for (int i = 0; i < 5; i++) await Create(JobDefinitionType.DataQualityScan, "Every 1 minute");
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Jobs:Admission:MaxOutstandingDispatches"] = "2" }).Build();
            var registered = new ServiceCollection(); PlantProcess.Application.DependencyInjection.AddApplication(registered);
            IServiceCollection services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(config);
            foreach (var type in new[] { typeof(JobAdmissionOptionsConfigurationProvider), typeof(IJobAdmissionConfigurationProvider),
                typeof(IJobAdmissionController), typeof(BoundedJobDispatcher), typeof(GovernedScheduleDispatcher) })
                services.Add(Assert.Single(registered, d => d.ServiceType == type));
            services.AddScoped<PlantProcessDbContext>(_ => fixture.NewContext());
            services.AddScoped<IPlantProcessDbContext>(p => p.GetRequiredService<PlantProcessDbContext>());
            services.AddScoped<ScopeProof>(_ => { var proof = new ScopeProof(); disposed.Add(proof); return proof; });
            services.AddScoped<IJobRunOrchestratorService>(p =>
            {
                var db = p.GetRequiredService<PlantProcessDbContext>();
                var proof = p.GetRequiredService<ScopeProof>();
                var run = Substitute.For<IJobRunOrchestratorService>();
                run.RunScheduledAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                    .Returns(async call =>
                    {
                        contexts.Add(db);
                        if (Interlocked.Increment(ref count) == 2) entered.TrySetResult();
                        await gate.Task.WaitAsync(call.Arg<CancellationToken>());
                        Assert.False(proof.Disposed);
                        return ApplicationResult<JobActionResponseDto>.Failure(ApplicationError.BusinessRule("controlled executor refusal"));
                    });
                return run;
            });
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await using var pollA = provider.CreateAsyncScope(); await using var pollB = provider.CreateAsyncScope();
            var pollAt = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
            var first = pollA.ServiceProvider.GetRequiredService<GovernedScheduleDispatcher>().DispatchDueAsync(pollAt, None);
            try
            {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = await pollB.ServiceProvider.GetRequiredService<GovernedScheduleDispatcher>().DispatchDueAsync(pollAt, None);
            Assert.Equal(2, count); Assert.Equal(0, second.Dispatched);
            Assert.Equal(second.OccurrencesDue, second.Refused);
            Assert.Equal(2, provider.GetRequiredService<BoundedJobDispatcher>().Outstanding);
            Assert.Equal(2, contexts.Distinct().Count()); Assert.All(disposed, x => Assert.False(x.Disposed));
            gate.SetResult(); var report = await first.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(report.OccurrencesDue, report.Refused); Assert.All(disposed, x => Assert.True(x.Disposed));
            Assert.Equal(0, provider.GetRequiredService<BoundedJobDispatcher>().Outstanding);
            Assert.Same(pollA.ServiceProvider.GetRequiredService<IJobAdmissionController>(), pollB.ServiceProvider.GetRequiredService<IJobAdmissionController>());
            }
            finally { gate.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10)); }
        }
        finally
        {
            gate.TrySetResult();
            await control.JobDefinitions.Where(x => x.JobCode.StartsWith("ADMIT_")).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsEnabled, false));
            await control.JobDefinitions.Where(x => enabledBefore.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsEnabled, true));
        }
    }
    private sealed class ScopeProof : IDisposable { public bool Disposed { get; private set; } public void Dispose() => Disposed = true; }
}
