using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Jobs.Canvas;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// T-245 against a real database: one job per definition however often it is bound, the
/// pinned version persisted as itself, and evidence reads that refuse a run belonging to
/// another job. The executor itself is T-261's and is not re-certified here.
/// </summary>
[Collection("CanonicalDefinitionStore")]
public sealed class CanvasJobBindingIntegrationTests : IAsyncLifetime
{
    private readonly DefinitionStoreFixture _fixture;
    private readonly List<Guid> _jobs = new();
    private readonly string _code = "t090test_t245_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private Guid _definitionId;

    public CanvasJobBindingIntegrationTests(DefinitionStoreFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = _fixture.NewContext();

        // A definition row is all this suite needs: it exercises binding and evidence
        // reads, not compilation, which T-261 already certified.
        var writer = new PlantProcess.Infrastructure.Definitions.CanonicalDefinitionWriter(db);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var written = await writer.WriteVersionAsync(
            new PlantProcess.Application.Definitions.CanonicalDefinitionWrite(
                PlantProcess.Application.Definitions.DefinitionKind.Transformation,
                _fixture.TenantId,
                _fixture.OwnerId,
                _code,
                "T-245 binding fixture",
                "{\"representation\":\"graph\"}",
                PlantProcess.Application.Definitions.CanonicalVersionStatus.Draft),
            CancellationToken.None);

        Assert.True(written.IsSuccess, written.Error?.Message);
        _definitionId = written.Value!.DefinitionId;

        var published = await writer.PublishAsync(_definitionId, written.Value.VersionNumber, CancellationToken.None);
        Assert.True(published.IsSuccess, published.Error?.Message);

        await transaction.CommitAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var db = _fixture.NewContext())
        {
            foreach (Guid jobId in _jobs)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM ppiq_meta.job_run_block_evidence WHERE job_run_history_id IN ("
                    + "SELECT id FROM ppiq_meta.job_run_histories WHERE job_definition_id = '" + jobId + "');"
                    + "DELETE FROM ppiq_meta.job_run_histories WHERE job_definition_id = '" + jobId + "';"
                    + "DELETE FROM ppiq_meta.job_definitions WHERE id = '" + jobId + "';");
            }
        }

        await _fixture.ResetAsync();
    }

    private CanvasJobBindingService Service(PlantProcessDbContext db) => new(
        new PlantProcess.Infrastructure.Definitions.CanonicalDefinitionWriter(db),
        new JobDefinitionService(db),
        new JobExecutionCapabilityAuthority(),
        new JobExecutorResolver(Array.Empty<IJobExecutor>()),
        new CanvasJobReadModel(db, new PlantProcess.Infrastructure.Jobs.JobRunBlockEvidenceStore(db)));

    private async Task RememberJobAsync(Guid jobId)
    {
        if (!_jobs.Contains(jobId)) { _jobs.Add(jobId); }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Binding_twice_keeps_one_job_and_persists_the_pinned_version()
    {
        await using var db = _fixture.NewContext();

        var first = await Service(db).BindAsync(_fixture.TenantId, new CanvasJobBindingRequest(_code, 1, null), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        await RememberJobAsync(first.Value!.JobDefinitionId);

        await using var second = _fixture.NewContext();
        var again = await Service(second).BindAsync(_fixture.TenantId, new CanvasJobBindingRequest(_code, 1, null), CancellationToken.None);

        Assert.True(again.IsSuccess, again.Error?.Message);
        Assert.Equal(first.Value.JobDefinitionId, again.Value!.JobDefinitionId);
        Assert.True(first.Value.Created);
        Assert.False(again.Value.Created);

        await using var read = _fixture.NewContext();
        List<JobDefinition> jobs = await read.JobDefinitions.AsNoTracking()
            .Where(x => !x.IsDeleted && x.TargetDefinitionId == _definitionId)
            .ToListAsync();

        JobDefinition job = Assert.Single(jobs);
        Assert.Equal(JobTargetVersionPolicy.Pinned, job.TargetVersionPolicy);
        Assert.Equal(1, job.TargetDefinitionVersion);
        Assert.Equal("Transformation", job.TargetDefinitionKind);
        Assert.Equal(JobDefinitionType.CanonicalRefresh, job.JobType);
    }

    [Fact]
    public async Task Rebinding_preserves_the_schedule_and_enabled_state_a_job_already_carries()
    {
        await using var db = _fixture.NewContext();
        var first = await Service(db).BindAsync(_fixture.TenantId, new CanvasJobBindingRequest(_code, 1, null), CancellationToken.None);
        await RememberJobAsync(first.Value!.JobDefinitionId);

        await using (var edit = _fixture.NewContext())
        {
            JobDefinition job = await edit.JobDefinitions.FirstAsync(x => x.Id == first.Value.JobDefinitionId);
            job.UpdateDefinition("Renamed by an operator", JobDefinitionType.CanonicalRefresh, "0 2 * * *", null, null, false, "operator note");
            await edit.SaveChangesAsync();
        }

        await using var rebind = _fixture.NewContext();
        await Service(rebind).BindAsync(_fixture.TenantId, new CanvasJobBindingRequest(_code, null, null), CancellationToken.None);

        await using var read = _fixture.NewContext();
        JobDefinition after = await read.JobDefinitions.AsNoTracking().FirstAsync(x => x.Id == first.Value.JobDefinitionId);

        Assert.Equal("0 2 * * *", after.ScheduleExpression);
        Assert.False(after.IsEnabled);
        Assert.Equal("Renamed by an operator", after.JobName);
        Assert.Equal(JobTargetVersionPolicy.CurrentPublished, after.TargetVersionPolicy);
        Assert.Null(after.TargetDefinitionVersion);
    }

    [Fact]
    public async Task A_run_of_another_job_is_refused_for_this_definition()
    {
        await using var db = _fixture.NewContext();
        var bound = await Service(db).BindAsync(_fixture.TenantId, new CanvasJobBindingRequest(_code, 1, null), CancellationToken.None);
        await RememberJobAsync(bound.Value!.JobDefinitionId);

        var stranger = new JobDefinition(
            "T245_STRANGER_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            "Unrelated job", JobDefinitionType.DbLinkImport, "Manual", false);

        await using (var other = _fixture.NewContext())
        {
            other.JobDefinitions.Add(stranger);
            await other.SaveChangesAsync();
        }

        await RememberJobAsync(stranger.Id);

        Guid foreignRunId;
        await using (var runtime = _fixture.NewContext())
        {
            var started = await new JobRuntimeService(runtime).StartAsync(stranger.JobCode, "tester", "tester", null, CancellationToken.None);
            Assert.True(started.IsSuccess, started.Error?.Message);
            foreignRunId = started.Value!.Id;
        }

        await using var read = _fixture.NewContext();
        var refused = await Service(read).ReadRunAsync(_fixture.TenantId, _code, foreignRunId, CancellationToken.None);

        Assert.True(refused.IsFailure);
        Assert.Equal(PlantProcess.Application.Common.Results.ApplicationErrorType.NotFound, refused.Error!.Type);
    }

    [Fact]
    public async Task A_definition_this_tenant_does_not_own_resolves_to_nothing()
    {
        await using var db = _fixture.NewContext();

        var result = await Service(db).DescribeExecutionAsync(Guid.NewGuid(), _code, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PlantProcess.Application.Common.Results.ApplicationErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task An_unregistered_executor_is_reported_before_any_launch()
    {
        await using var db = _fixture.NewContext();

        // This composition registers no executor on purpose: the answer must be a typed
        // refusal that a surface can explain, not an optimistic Run button.
        var capability = await Service(db).DescribeExecutionAsync(_fixture.TenantId, _code, 1, CancellationToken.None);

        Assert.True(capability.IsSuccess, capability.Error?.Message);
        Assert.False(capability.Value!.ExecutorRegistered);
        Assert.False(capability.Value.StaticallyEligible);
        Assert.Equal(JobExecutionDiagnosticCodes.ExecutorMissing, capability.Value.RefusalCode);
        Assert.Equal(1, capability.Value.ResolvedVersion);
    }
}
