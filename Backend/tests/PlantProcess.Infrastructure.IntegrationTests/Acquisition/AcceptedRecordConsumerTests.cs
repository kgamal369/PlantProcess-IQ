using PlantProcess.Infrastructure.IntegrationTests.Acquisition;
using PlantProcess.Application.Jobs.Admission;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Jobs.Dependencies;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Execution.Transformations;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Application.Relationships;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Definitions.Canvas;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;
using PlantProcess.Infrastructure.Jobs;
using PlantProcess.Infrastructure.Jobs.Transformations;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Jobs;

/// <summary>Real accepted records, sealed batch, canonical publication and production job execution.</summary>
[Collection("CanonicalDefinitionStore")]
public sealed class AcceptedRecordConsumerTests : IAsyncLifetime
{
    private const string Schema = "ppiq_staging";
    private string SourceRelation = string.Empty;
    private AcceptedRecordFixture _accepted = null!;
    private const string SecondRelation = "txexec_second";
    private const string PlainRelation = "txexec_plain";
    private readonly string CodePrefix = "t090test_accepted_" + Guid.NewGuid().ToString("N") + "_";

    private readonly DefinitionStoreFixture _fixture;
    private string _system = string.Empty;
    private readonly List<Guid> _jobs = new();
    private Guid _siteId;
    private static readonly DateTime FixtureProductionStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public AcceptedRecordConsumerTests(DefinitionStoreFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------- fixture ----

    public async Task InitializeAsync()
    {
        await using var db = _fixture.NewContext();
        var site = new Site("AR-" + Guid.NewGuid().ToString("N")[..8], "Accepted consumer site", false);
        db.Set<Site>().Add(site); await db.SaveChangesAsync(); _siteId = site.Id;
        _accepted = new AcceptedRecordFixture(_fixture);
        await _accepted.InitializeAsync(new Dictionary<string,string> {
            ["code"]="string",["unit_type"]="string",["site_id"]="guid",["zone"]="string",
            ["offset_minutes"]="int32",["production_start_utc"]="datetime",["qty"]="int32" });
        _system = _accepted.SourceSystem;
        var a = await _accepted.OpenAsync();
        var b = await _accepted.OpenAsync(2,"end-a");
        foreach(var row in new[]{("UNIT-1","r1",a),("UNIT-2","r2",a),("EXCLUDED-B","r3",b)})
        {
            var accepted = await _accepted.AppendAsync(row.Item3,row.Item2,new Dictionary<string,object?> {
                ["code"]=row.Item1,["unit_type"]="Batch",["site_id"]=_siteId.ToString("D"),["zone"]="UTC",
                ["offset_minutes"]=0,["production_start_utc"]="2026-01-01T00:00:00Z",["qty"]=9 });
            Assert.True(accepted.IsAccepted,accepted.Refusal?.Detail);
        }
        SourceRelation=(await _accepted.SealAsync(a,"end-a")).RelationName!;
        await _accepted.SealAsync(b,"end-b");
    }

    public async Task DisposeAsync()
    {
        await using var db = _fixture.NewContext();
        foreach (var job in _jobs)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ppiq_meta.job_run_block_evidence WHERE job_run_history_id IN (SELECT id FROM ppiq_meta.job_run_histories WHERE job_definition_id={job})");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ppiq_meta.job_run_histories WHERE job_definition_id={job}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ppiq_meta.job_definitions WHERE id={job}");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ppiq_plant.material_units WHERE source_system={_system}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ppiq_plant.sites WHERE id={_siteId}");
        await _fixture.ResetAsync();
    }

    // ------------------------------------------------------- the definition ---

    private static string Board(string[] tables, bool withFilter) =>
        "{\"nodes\":[" + string.Join(",", tables.Select(t => "{\"id\":\"" + t + "\",\"kind\":\"dataset\"}"))
        + (withFilter ? ",{\"id\":\"only-large\",\"kind\":\"filter\"}" : string.Empty)
        + "],\"edges\":[" + (withFilter ? "{\"source\":\"" + tables[0] + "\",\"target\":\"only-large\"}" : string.Empty) + "]}";

    private string GraphJson(string[] tables, bool withFilter, string joins = "[]") =>
        "{\"name\":\"governed-execution\",\"targetEntity\":\"" + nameof(MaterialUnit) + "\",\"tables\":["
        + string.Join(",", tables.Select(t => "\"" + t + "\"")) + "],\"joins\":" + joins + ",\"filters\":"
        + (withFilter
            ? "[{\"table\":\"" + tables[0] + "\",\"column\":\"qty\",\"op\":\">\",\"value\":\"5\"}]"
            : "[]")
        + ",\"board\":" + Board(tables, withFilter) + "}";

    private static string Projection(string table, string target = "MaterialUnit") =>
        "{\"targetEntity\":\"" + target + "\",\"fieldBindings\":["
        + Binding("MaterialCode", table, "code") + ","
        + Binding("MaterialUnitType", table, "unit_type") + ","
        + Binding("SiteId", table, "site_id") + ","
        + Binding("ProductionStartUtc", table, "production_start_utc") + ","
        + Binding("PlantTimeZoneId", table, "zone") + ","
        + Binding("PlantUtcOffsetMinutes", table, "offset_minutes") + "]}";

    private static string Binding(string field, string table, string column) =>
        "{\"targetField\":\"" + field + "\",\"sourceKind\":\"column\",\"sourceTable\":\"" + table
        + "\",\"sourceField\":\"" + column + "\"}";

    private CanvasDefinitionLifecycleService Lifecycle(PlantProcessDbContext db) =>
        new(db, new CanonicalDefinitionWriter(db), new CanvasCompatibilityProjection(),
            new CanonicalEntityCatalog(db), new CanvasStagingSchema(Schema), new NoRelationships());

    private sealed class NoRelationships : IRelationshipPublicationService
    {
        public Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> PublishAsync(
            RelationshipPublicationRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Governed execution never publishes relationships.");

        public Task<ApplicationResult<int>> RetireByDefinitionAsync(
            Guid sourceDefinitionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Governed execution never retires relationships.");
    }

    private async Task<Guid> PublishDefinitionAsync(string suffix, string graphJson, string projectionJson)
    {
        await using var db = _fixture.NewContext();

        var saved = await Lifecycle(db).SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, CodePrefix + suffix, "Governed execution",
                graphJson, nameof(MaterialUnit), projectionJson),
            CancellationToken.None);

        Assert.True(saved.IsSuccess, saved.Error?.Message);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var published = await new CanonicalDefinitionWriter(db)
                .PublishAsync(saved.Value!.DefinitionId, saved.Value.VersionNumber, CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);
            await transaction.CommitAsync();
        }

        return saved.Value!.DefinitionId;
    }

    private async Task<Guid> JobForAsync(Guid definitionId, int? pinnedVersion)
    {
        await using var db = _fixture.NewContext();
        var job = new JobDefinition(
            "TXEXEC_" + Guid.NewGuid().ToString("N").Substring(0, 10),
            "Governed transformation probe", JobDefinitionType.CanonicalRefresh, "Manual", false);
        JobLaneAssignment.Initialize(job);

        job.AssignTargetDefinition(
            DefinitionKind.Transformation.ToString(), definitionId,
            pinnedVersion.HasValue ? JobTargetVersionPolicy.Pinned : JobTargetVersionPolicy.CurrentPublished,
            pinnedVersion);

        db.JobDefinitions.Add(job);
        await db.SaveChangesAsync();
        _jobs.Add(job.Id);
        return job.Id;
    }

    // ------------------------------------------------------- the composition --

    private sealed class CountingImport : IImportBatchQueueProcessorService
    {
        public int Calls { get; private set; }

        public Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
            int maxBatches, int rowsPerBatch, bool stopOnFirstError, bool runDataQualityScan, CancellationToken ct)
        {
            Calls = Calls + 1;
            return Task.FromResult(ApplicationResult<ImportQueueProcessingSummary>.Failure(
                ApplicationError.BusinessRule("The fixture import processor performs no work.")));
        }
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

    /// <summary>Inject one managed exception only after a separate DB context proves canonical durability.</summary>
    private sealed class FailingOnceEvidenceStore : IJobRunBlockEvidenceStore
    {
        private readonly IJobRunBlockEvidenceStore _inner;
        private readonly Func<Task<bool>> _canonicalRowIsDurable;
        public bool FaultInjected { get; private set; }
        public FailingOnceEvidenceStore(IJobRunBlockEvidenceStore inner, Func<Task<bool>> canonicalRowIsDurable)
        {
            _inner = inner;
            _canonicalRowIsDurable = canonicalRowIsDurable;
        }
        public Task AddAsync(IReadOnlyList<JobRunBlockEvidence> evidence, CancellationToken cancellationToken) =>
            _inner.AddAsync(evidence, cancellationToken);
        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            if (!FaultInjected && await _canonicalRowIsDurable())
            {
                FaultInjected = true;
                throw new InvalidOperationException("Injected evidence-save failure after independently verified canonical durability.");
            }
            await _inner.SaveAsync(cancellationToken);
        }
        public Task DiscardAsync(JobRunBlockEvidence evidence, CancellationToken cancellationToken) =>
            _inner.DiscardAsync(evidence, cancellationToken);
        public Task<IReadOnlyList<JobRunBlockEvidence>> ListAsync(Guid jobRunHistoryId, CancellationToken cancellationToken) =>
            _inner.ListAsync(jobRunHistoryId, cancellationToken);
    }

    private sealed class RequestingProbe : IJobRunCancellationProbe
    {
        private readonly IJobRunCancellationProbe _inner;
        private readonly Func<Guid, Task> _request;
        private bool _requested;

        public RequestingProbe(IJobRunCancellationProbe inner, Func<Guid, Task> request)
        {
            _inner = inner;
            _request = request;
        }

        public async Task<bool> IsRequestedAsync(Guid jobRunHistoryId, CancellationToken cancellationToken)
        {
            if (!_requested)
            {
                _requested = true;
                await _request(jobRunHistoryId);
            }

            return await _inner.IsRequestedAsync(jobRunHistoryId, cancellationToken);
        }
    }

    private JobRunOrchestratorService Orchestrator(
        PlantProcessDbContext db,
        CountingImport import,
        IJobRunBlockEvidenceStore? evidence = null,
        IJobRunCancellationProbe? probe = null)
    {
        var authority = new JobExecutionCapabilityAuthority();

        var executor = new TransformationProjectionJobExecutor(
            new CanonicalEntityCatalog(db),
            new MaterialUnitTransformationWriter(db),
            new CanonicalDefinitionWriter(db),
            new NpgsqlTransformationSourceReader(db),
            new CanvasStagingSchema(Schema),
            evidence ?? new JobRunBlockEvidenceStore(db),
            probe ?? new JobRunCancellationProbe(db));

        return new JobRunOrchestratorService(
            new RunnableJobLookup(db),
            new JobRuntimeService(db),
            import,
            new NoQuality(),
            new NoRisk(),
            authority,
            new JobTargetResolver(new DefinitionService(db), new CapabilityJobTargetClassPolicy(authority), new JobTargetLookup(db)),
            new JobDependencyService(db),
            new JobExecutorResolver(new IJobExecutor[] { executor }),
                new JobAdmissionController(new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<JobAdmissionController>.Instance));
    }

    private async Task<List<MaterialUnit>> UnitsAsync()
    {
        await using var db = _fixture.NewContext();
        return await db.MaterialUnits.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SourceSystem == _system)
            .OrderBy(x => x.MaterialCode)
            .ToListAsync();
    }

    private async Task<List<JobRunBlockEvidence>> EvidenceAsync(Guid runId)
    {
        await using var db = _fixture.NewContext();
        return await db.JobRunBlockEvidences.AsNoTracking()
            .Where(x => x.JobRunHistoryId == runId)
            .OrderBy(x => x.ExecutionOrdinal)
            .ToListAsync();
    }

    private async Task<JobRunHistory?> RunAsync(Guid runId)
    {
        await using var db = _fixture.NewContext();
        return await db.JobRunHistories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId);
    }

    private async Task<JobRunHistory> LatestRunAsync(Guid jobId)
    {
        await using var db = _fixture.NewContext();
        JobRunHistory? run = await db.JobRunHistories.AsNoTracking()
            .Where(x => x.JobDefinitionId == jobId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync();

        Assert.NotNull(run);
        return run!;
    }

    private async Task<int> RunCountAsync(Guid jobId)
    {
        await using var db = _fixture.NewContext();
        return await db.JobRunHistories.AsNoTracking().CountAsync(x => x.JobDefinitionId == jobId);
    }

    private async Task AssertRunOkAsync(ApplicationResult<JobActionResponseDto> result)
    {
        if (result.IsSuccess && result.Value?.Status == JobRunStatus.Ok) { return; }
        Guid? runId = result.Value?.JobRunHistoryId;
        JobRunHistory? run = runId.HasValue ? await RunAsync(runId.Value) : null;
        var blocks = runId.HasValue ? await EvidenceAsync(runId.Value) : new List<JobRunBlockEvidence>();
        string detail = JsonSerializer.Serialize(new
        {
            result.IsSuccess, result.Error, result.Value,
            PersistedRunStatus = run?.Status,
            PersistedVersion = run?.TargetDefinitionVersion,
            Blocks = blocks.Select(b => new { b.BlockId, b.Status, b.DiagnosticCode, b.DiagnosticDetail, b.InputRows, b.OutputRows })
        });
        Assert.True(false, "Expected a genuinely successful run. Actual persisted evidence: " + detail);
    }

    // ------------------------------------------------------------ the proofs --

    [Fact]
    public async Task Sealed_batch_executes_through_published_definition_with_exact_version_and_provenance()
    {
        Guid definitionId = await PublishDefinitionAsync("known", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using var db = _fixture.NewContext();
        var import = new CountingImport();
        var result = await Orchestrator(db, import).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        await AssertRunOkAsync(result);
        Assert.Equal(0, import.Calls);

        JobRunHistory? run = await RunAsync(result.Value.JobRunHistoryId!.Value);
        Assert.NotNull(run);
        Assert.Equal(definitionId, run!.TargetDefinitionId);
        Assert.Equal(1, run.TargetDefinitionVersion);
        Assert.Equal(DefinitionKind.Transformation.ToString(), run.TargetDefinitionKind);

        List<MaterialUnit> units = await UnitsAsync();
        Assert.Equal(new[] { "UNIT-1", "UNIT-2" }, units.Select(u => u.MaterialCode).ToArray());
        Assert.Equal(new[] { "r1", "r2" }, units.Select(u => u.SourceRecordId).ToArray());
        Assert.All(units, u => Assert.Equal(_siteId, u.SiteId));
        Assert.All(units, u => Assert.Equal(FixtureProductionStart, u.ProductionStartUtc));

        List<JobRunBlockEvidence> evidence = await EvidenceAsync(run.Id);
        Assert.Equal(new[] { SourceRelation, "only-large" }, evidence.Select(e => e.BlockId).ToArray());
        Assert.All(evidence, e => Assert.Equal(JobRunBlockStatus.Succeeded, e.Status));
        Assert.Equal(2, evidence[0].OutputRows);
        Assert.Equal(2, evidence[1].InputRows);
        Assert.Equal(2, evidence[1].OutputRows);
    }

}
