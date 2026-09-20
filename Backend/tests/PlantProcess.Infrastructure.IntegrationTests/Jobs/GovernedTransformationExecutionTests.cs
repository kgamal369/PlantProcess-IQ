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

/// <summary>
/// GOVERNED TRANSFORMATION EXECUTION, END TO END, IN A DISPOSABLE DATABASE.
///
/// The source relation here is a contract fixture: a source-shaped staged relation that
/// carries the governed provenance columns beside its business columns, which is the
/// shape the accepted-record path will produce. It is created and dropped by this suite
/// and exists only inside the runner-owned disposable database.
/// </summary>
[Collection("CanonicalDefinitionStore")]
public sealed class GovernedTransformationExecutionTests : IAsyncLifetime
{
    private const string Schema = "ppiq_staging";
    private const string SourceRelation = "txexec_source";
    private const string SecondRelation = "txexec_second";
    private const string PlainRelation = "txexec_plain";
    private const string CodePrefix = "t090test_txexec_";

    private readonly DefinitionStoreFixture _fixture;
    private readonly string _system = "txexec-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private readonly List<Guid> _jobs = new();
    private Guid _siteId;
    private static readonly DateTime FixtureProductionStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GovernedTransformationExecutionTests(DefinitionStoreFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------- fixture ----

    public async Task InitializeAsync()
    {
        await using var db = _fixture.NewContext();

        await db.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS " + Schema + "." + SourceRelation + ";"
            + "DROP TABLE IF EXISTS " + Schema + "." + SecondRelation + ";"
            + "DROP TABLE IF EXISTS " + Schema + "." + PlainRelation + ";");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE " + Schema + "." + SourceRelation + " ("
            + "code text, unit_type text, site_id uuid, zone text, offset_minutes integer, production_start_utc timestamptz, qty integer, "
            + "source_system varchar(100), source_record_id varchar(200));"
            + "CREATE TABLE " + Schema + "." + SecondRelation + " ("
            + "code text, unit_type text, site_id uuid, zone text, offset_minutes integer, production_start_utc timestamptz, qty integer, "
            + "source_system varchar(100), source_record_id varchar(200));"
            + "CREATE TABLE " + Schema + "." + PlainRelation + " ("
            + "code text, unit_type text, site_id uuid, zone text, offset_minutes integer, production_start_utc timestamptz, qty integer);");

        var site = new Site("TXEXEC-" + Guid.NewGuid().ToString("N").Substring(0, 8), "Transformation execution fixture site", false);
        db.Set<Site>().Add(site);
        await db.SaveChangesAsync();
        _siteId = site.Id;
    }

    public async Task DisposeAsync()
    {
        await using (var db = _fixture.NewContext())
        {
            foreach (Guid jobId in _jobs)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM ppiq_meta.job_run_block_evidence WHERE job_run_history_id IN "
                    + "(SELECT id FROM ppiq_meta.job_run_histories WHERE job_definition_id = '" + jobId + "');"
                    + "DELETE FROM ppiq_meta.job_run_histories WHERE job_definition_id = '" + jobId + "';"
                    + "DELETE FROM ppiq_meta.job_definitions WHERE id = '" + jobId + "';");
            }

            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ppiq_plant.material_units WHERE source_system = '" + _system + "';"
                + "DELETE FROM ppiq_plant.sites WHERE id = '" + _siteId + "';"
                + "DROP TABLE IF EXISTS " + Schema + "." + SourceRelation + ";"
                + "DROP TABLE IF EXISTS " + Schema + "." + SecondRelation + ";"
                + "DROP TABLE IF EXISTS " + Schema + "." + PlainRelation + ";");
        }

        await _fixture.ResetAsync();
    }

    private async Task SeedAsync(string relation, params (string Code, string Record, int Qty)[] rows)
    {
        await using var db = _fixture.NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE " + Schema + "." + relation + ";");

        foreach (var row in rows)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO " + Schema + "." + relation
                + " (code, unit_type, site_id, zone, offset_minutes, production_start_utc, qty, source_system, source_record_id) VALUES ('"
                + row.Code + "', 'FixtureUnit', '" + _siteId + "', 'UTC', 0, TIMESTAMPTZ '2026-01-01 00:00:00+00', "
                + row.Qty.ToString(CultureInfo.InvariantCulture) + ", '"
                + _system + "', " + (row.Record.Length == 0 ? "NULL" : "'" + row.Record + "'") + ");");
        }
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

    [Fact]
    public void Projection_catalog_preserves_underlying_types_and_requiredness()
    {
        using var db = _fixture.NewContext();
        var catalog = new CanonicalEntityCatalog(db);
        var fields = catalog.ProjectionFieldsOf(nameof(MaterialUnit));
        Assert.NotEmpty(fields);
        var model = db.Model.FindEntityType(typeof(MaterialUnit));
        Assert.NotNull(model);

        // Explicit regression oracle: the nullable timestamp remains a timestamp,
        // and its optionality is independent of the type's display name.
        var productionStart = Assert.Single(fields,
            field => field.Name == nameof(MaterialUnit.ProductionStartUtc));
        Assert.Equal("DateTime", productionStart.ClrTypeName);
        Assert.False(productionStart.IsRequired);
        Assert.True(ProjectionTypeCompatibility.IsCompatible(
            "timestamp with time zone", productionStart.ClrTypeName));
        Assert.False(ProjectionTypeCompatibility.IsCompatible("text", productionStart.ClrTypeName));
        Assert.False(ProjectionTypeCompatibility.IsCompatible("uuid", productionStart.ClrTypeName));
        Assert.False(ProjectionTypeCompatibility.IsCompatible("timestamp with time zone", "Nullable`1"));

        foreach (var field in fields)
        {
            var property = model!.FindProperty(field.Name);
            Assert.NotNull(property);
            Assert.Equal(!property!.IsNullable, field.IsRequired);
            Assert.Equal((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType).Name,
                field.ClrTypeName);
        }
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
            new JobExecutorResolver(new IJobExecutor[] { executor }));
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
    public async Task A_governed_run_projects_the_exact_version_and_records_block_evidence()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9), ("UNIT-2", "r2", 9));
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

    [Fact]
    public async Task The_same_source_identity_twice_changes_nothing_and_duplicates_nothing()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        Guid definitionId = await PublishDefinitionAsync("replay", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using var db = _fixture.NewContext();
        var first = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);
        await AssertRunOkAsync(first);
        Guid canonicalId = (await UnitsAsync()).Single().Id;

        await using var second = _fixture.NewContext();
        var again = await Orchestrator(second, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        await AssertRunOkAsync(again);
        MaterialUnit only = Assert.Single(await UnitsAsync());
        Assert.Equal(canonicalId, only.Id);
        Assert.Contains("0 inserted", again.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_conflicting_effect_for_a_known_identity_refuses_and_leaves_canonical_state_unchanged()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        Guid definitionId = await PublishDefinitionAsync("conflict", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using (var db = _fixture.NewContext())
        {
            var first = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);
            await AssertRunOkAsync(first);
        }

        await SeedAsync(SourceRelation, ("UNIT-1-CHANGED", "r1", 9));

        await using var conflicted = _fixture.NewContext();
        var result = await Orchestrator(conflicted, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.Equal(JobRunStatus.Failed, result.Value!.Status);
        Assert.Contains(JobExecutionDiagnosticCodes.CanonicalIdentityConflict, result.Value.Message, StringComparison.Ordinal);

        MaterialUnit only = Assert.Single(await UnitsAsync());
        Assert.Equal("UNIT-1", only.MaterialCode);

        List<JobRunBlockEvidence> evidence = await EvidenceAsync(result.Value.JobRunHistoryId!.Value);
        Assert.Equal(JobRunBlockStatus.Failed, evidence.Last().Status);
        Assert.Equal(JobExecutionDiagnosticCodes.CanonicalIdentityConflict, evidence.Last().DiagnosticCode);
    }

    [Fact]
    public async Task A_row_without_governed_identity_refuses_before_anything_is_written()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9), ("UNIT-2", string.Empty, 9));
        Guid definitionId = await PublishDefinitionAsync("invalid", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using var db = _fixture.NewContext();
        var result = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.Equal(JobRunStatus.Failed, result.Value!.Status);
        Assert.Contains(JobExecutionDiagnosticCodes.SourceIdentityInvalid, result.Value.Message, StringComparison.Ordinal);
        Assert.Empty(await UnitsAsync());
    }

    [Fact]
    public async Task Two_provenance_capable_relations_refuse_before_a_run_exists()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        await SeedAsync(SecondRelation, ("UNIT-1", "r1", 9));

        Guid definitionId = await PublishDefinitionAsync(
            "ambiguous",
            GraphJson(new[] { SourceRelation, SecondRelation }, false,
                "[{\"leftTable\":\"" + SourceRelation + "\",\"leftColumn\":\"code\",\"rightTable\":\"" + SecondRelation + "\",\"rightColumn\":\"code\"}]"),
            Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using var db = _fixture.NewContext();
        var result = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityAmbiguous, result.Error!.Code);
        Assert.Equal(0, await RunCountAsync(jobId));
    }

    [Fact]
    public async Task A_relation_without_provenance_refuses_before_a_run_exists()
    {
        await using (var seed = _fixture.NewContext())
        {
            await seed.Database.ExecuteSqlRawAsync(
                "TRUNCATE " + Schema + "." + PlainRelation + ";"
                + "INSERT INTO " + Schema + "." + PlainRelation
                + " (code, unit_type, site_id, zone, offset_minutes, qty) VALUES ('UNIT-1', 'FixtureUnit', '"
                + _siteId + "', 'UTC', 0, 9);");
        }

        Guid definitionId = await PublishDefinitionAsync(
            "unavailable", GraphJson(new[] { PlainRelation }, false), Projection(PlainRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using var db = _fixture.NewContext();
        var result = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityUnavailable, result.Error!.Code);
        Assert.Equal(0, await RunCountAsync(jobId));
        Assert.Empty(await UnitsAsync());
    }

    [Fact]
    public async Task An_authored_statement_version_refuses_before_a_run_exists()
    {
        await using var db = _fixture.NewContext();

        var saved = await Lifecycle(db).SaveSqlAsync(
            new CanvasSqlSave(_fixture.TenantId, _fixture.OwnerId, CodePrefix + "sqlmode", "Governed execution",
                null,
                "SELECT code, unit_type, site_id, production_start_utc, zone, offset_minutes FROM " + Schema + "." + SourceRelation,
                null, nameof(MaterialUnit), SqlProjection()),
            CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var published = await new CanonicalDefinitionWriter(db)
                .PublishAsync(saved.Value!.DefinitionId, saved.Value.VersionNumber, CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);
            await transaction.CommitAsync();
        }

        Guid jobId = await JobForAsync(saved.Value!.DefinitionId, 1);

        await using var runner = _fixture.NewContext();
        var result = await Orchestrator(runner, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityUnavailable, result.Error!.Code);
        Assert.Equal(0, await RunCountAsync(jobId));
    }

    private static string SqlProjection() =>
        "{\"targetEntity\":\"MaterialUnit\",\"fieldBindings\":["
        + "{\"targetField\":\"MaterialCode\",\"sourceKind\":\"sql\",\"sourceField\":\"code\"},"
        + "{\"targetField\":\"MaterialUnitType\",\"sourceKind\":\"sql\",\"sourceField\":\"unit_type\"},"
        + "{\"targetField\":\"SiteId\",\"sourceKind\":\"sql\",\"sourceField\":\"site_id\"},"
        + "{\"targetField\":\"ProductionStartUtc\",\"sourceKind\":\"sql\",\"sourceField\":\"production_start_utc\"},"
        + "{\"targetField\":\"PlantTimeZoneId\",\"sourceKind\":\"sql\",\"sourceField\":\"zone\"},"
        + "{\"targetField\":\"PlantUtcOffsetMinutes\",\"sourceKind\":\"sql\",\"sourceField\":\"offset_minutes\"}]}";

    [Fact]
    public async Task The_current_published_version_is_what_a_following_job_executes()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9), ("UNIT-2", "r2", 1));
        Guid definitionId = await PublishDefinitionAsync(
            "version", GraphJson(new[] { SourceRelation }, false), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, null);

        await using (var first = _fixture.NewContext())
        {
            var run = await Orchestrator(first, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);
            await AssertRunOkAsync(run);
            Assert.Equal(1, (await RunAsync(run.Value.JobRunHistoryId!.Value))!.TargetDefinitionVersion);
            Assert.Equal(2, (await UnitsAsync()).Count);
        }

        await using (var db = _fixture.NewContext())
        {
            var second = await Lifecycle(db).SaveGraphAsync(
                new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, CodePrefix + "version", "Governed execution",
                    GraphJson(new[] { SourceRelation }, true), nameof(MaterialUnit), Projection(SourceRelation)),
                CancellationToken.None);
            Assert.True(second.IsSuccess, second.Error?.Message);

            await using var transaction = await db.Database.BeginTransactionAsync();
            var published = await new CanonicalDefinitionWriter(db)
                .PublishAsync(definitionId, second.Value!.VersionNumber, CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);
            await transaction.CommitAsync();
        }

        await using var runner = _fixture.NewContext();
        var filtered = await Orchestrator(runner, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        await AssertRunOkAsync(filtered);
        JobRunHistory run2 = (await RunAsync(filtered.Value.JobRunHistoryId!.Value))!;
        Assert.Equal(2, run2.TargetDefinitionVersion);
        Assert.Equal(1, (await EvidenceAsync(run2.Id)).Last().InputRows);
    }

    [Fact]
    public async Task A_failure_after_the_canonical_write_converges_on_retry_without_duplicating()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        Guid definitionId = await PublishDefinitionAsync("recovery", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        Guid crashedRunId;
        await using (var db = _fixture.NewContext())
        {
            // A controlled exception, not a claim of an actual killed process. The trigger
            // observes the canonical row through another context, not an incidental save count.
            var store = new FailingOnceEvidenceStore(new JobRunBlockEvidenceStore(db),
                async () => (await UnitsAsync()).Count == 1);
            var result = await Orchestrator(db, new CountingImport(), store).RunNowAsync(jobId, "tester", null, CancellationToken.None);

            // An unexpected failure is not a governed refusal: the run exists and converges
            // to Failed, and the caller is told the attempt failed rather than succeeded.
            Assert.True(store.FaultInjected, "The intended post-durability fault was never reached: " + JsonSerializer.Serialize(result));
            Assert.True(result.IsFailure, "The injected exception must not become a successful run.");
        }

        JobRunHistory crashedRun = await LatestRunAsync(jobId);
        Assert.Equal(JobRunStatus.Failed, crashedRun.Status);
        crashedRunId = crashedRun.Id;

        MaterialUnit written = Assert.Single(await UnitsAsync());
        List<JobRunBlockEvidence> crashed = await EvidenceAsync(crashedRunId);
        Assert.DoesNotContain(crashed, e => e.Status == JobRunBlockStatus.Pending);
        Assert.Equal(JobRunBlockStatus.Failed, crashed.Last().Status);
        Assert.Equal(JobExecutionDiagnosticCodes.BlockFailed, crashed.Last().DiagnosticCode);

        await using var retry = _fixture.NewContext();
        var second = await Orchestrator(retry, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        await AssertRunOkAsync(second);
        MaterialUnit after = Assert.Single(await UnitsAsync());
        Assert.Equal(written.Id, after.Id);
        Assert.All(await EvidenceAsync(second.Value.JobRunHistoryId!.Value),
            e => Assert.Equal(JobRunBlockStatus.Succeeded, e.Status));
    }

    [Fact]
    public async Task An_operator_request_is_observed_acknowledged_and_writes_nothing()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        Guid definitionId = await PublishDefinitionAsync("cancel", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);

        await using var db = _fixture.NewContext();

        // The request is written by a DIFFERENT context, exactly as an operator's request
        // reaches a running executor, so a stale tracked copy cannot answer it.
        var probe = new RequestingProbe(new JobRunCancellationProbe(db), async runId =>
        {
            await using var requester = _fixture.NewContext();
            var requested = await new JobRuntimeService(requester)
                .RequestCancellationAsync(jobId, runId, "tester", "stop", CancellationToken.None);
            Assert.True(requested.IsSuccess, requested.Error?.Message);
        });

        var result = await Orchestrator(db, new CountingImport(), null, probe).RunNowAsync(jobId, "tester", null, CancellationToken.None);

        Assert.Equal(JobRunStatus.Cancelled, result.Value!.Status);
        JobRunHistory run = (await RunAsync(result.Value.JobRunHistoryId!.Value))!;
        Assert.Equal(JobRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CancellationAcknowledgedAtUtc);
        Assert.Empty(await UnitsAsync());
        Assert.All(await EvidenceAsync(run.Id), e => Assert.Equal(JobRunBlockStatus.Cancelled, e.Status));
    }

    [Fact]
    public async Task The_background_import_queue_path_is_untouched_by_the_commissioning()
    {
        await using var db = _fixture.NewContext();

        var importJob = new JobDefinition(
            "TXEXEC_IMPORT_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            "Import queue probe", JobDefinitionType.DbLinkImport, "Manual", false);

        db.JobDefinitions.Add(importJob);
        await db.SaveChangesAsync();
        _jobs.Add(importJob.Id);

        var import = new CountingImport();
        var result = await Orchestrator(db, import).RunNowAsync(importJob.Id, "tester", null, CancellationToken.None);

        // The generic queue is still the DbLinkImport path and still reached exactly once.
        // Commissioning the Transformation executor did not reroute it.
        Assert.Equal(1, import.Calls);
        Assert.True(result.IsSuccess);
        Assert.Equal(JobRunStatus.Failed, result.Value!.Status);
    }

    [Fact]
    public async Task A_targetless_canonical_refresh_job_still_refuses_and_never_reaches_the_import_queue()
    {
        await using var db = _fixture.NewContext();

        var targetless = new JobDefinition(
            "TXEXEC_TARGETLESS_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            "Targetless refresh probe", JobDefinitionType.CanonicalRefresh, "Manual", false);

        db.JobDefinitions.Add(targetless);
        await db.SaveChangesAsync();
        _jobs.Add(targetless.Id);

        var import = new CountingImport();
        var result = await Orchestrator(db, import).RunNowAsync(targetless.Id, "tester", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, import.Calls);
        Assert.Equal(0, await RunCountAsync(targetless.Id));
    }

    [Fact]
    public void The_executor_reuses_the_frozen_projection_vocabulary_rather_than_restating_it()
    {
        var frozen = typeof(CanvasDefinitionLifecycleService)
            .GetField("ProjectionRequiredCode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(frozen);
        Assert.Equal(
            TransformationProjectionJobExecutor.ProjectionRequiredCode,
            (string)frozen!.GetRawConstantValue()!);
    }
    [Fact]
    public async Task Timezone_or_offset_without_a_production_start_refuses_without_writing()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        await using (var seed = _fixture.NewContext())
        {
            await seed.Database.ExecuteSqlRawAsync("UPDATE " + Schema + "." + SourceRelation + " SET production_start_utc = NULL;");
        }
        Guid definitionId = await PublishDefinitionAsync("invalid-window", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);
        await using var db = _fixture.NewContext();
        var result = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(JobRunStatus.Failed, result.Value!.Status);
        Assert.Contains(JobExecutionDiagnosticCodes.CanonicalWriteFailed, result.Value.Message, StringComparison.Ordinal);
        Assert.Contains("without a production start", result.Value.Message, StringComparison.Ordinal);
        Assert.Empty(await UnitsAsync());
    }

    [Fact]
    public async Task Runtime_role_can_write_evidence_but_cannot_delete_it_and_parent_history_is_restricted()
    {
        await SeedAsync(SourceRelation, ("UNIT-1", "r1", 9));
        Guid definitionId = await PublishDefinitionAsync("role-evidence", GraphJson(new[] { SourceRelation }, true), Projection(SourceRelation));
        Guid jobId = await JobForAsync(definitionId, 1);
        await using var db = _fixture.NewContext();
        var result = await Orchestrator(db, new CountingImport()).RunNowAsync(jobId, "tester", null, CancellationToken.None);
        await AssertRunOkAsync(result);
        Guid runId = result.Value!.JobRunHistoryId!.Value;
        Guid probeId = Guid.NewGuid();
        string connectionString = db.Database.GetConnectionString()!;
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var role = new NpgsqlCommand("SET LOCAL ROLE plantprocess_app", connection, transaction))
                await role.ExecuteNonQueryAsync();
            await using (var insert = new NpgsqlCommand(
                "INSERT INTO ppiq_meta.job_run_block_evidence (id,job_run_history_id,block_id,execution_ordinal,status) " +
                "VALUES (@id,@run,'runtime-privilege-probe',1000000,'Pending')", connection, transaction))
            {
                insert.Parameters.AddWithValue("id", probeId); insert.Parameters.AddWithValue("run", runId);
                Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            }
            await using (var update = new NpgsqlCommand(
                "UPDATE ppiq_meta.job_run_block_evidence SET status='Running',started_at_utc=now() WHERE id=@id", connection, transaction))
            {
                update.Parameters.AddWithValue("id", probeId);
                Assert.Equal(1, await update.ExecuteNonQueryAsync());
            }
            await using (var read = new NpgsqlCommand("SELECT status FROM ppiq_meta.job_run_block_evidence WHERE id=@id", connection, transaction))
            {
                read.Parameters.AddWithValue("id", probeId);
                Assert.Equal("Running", await read.ExecuteScalarAsync());
            }
            // A real existing row and the intended SQLSTATE, not an empty DELETE result.
            await using (var delete = new NpgsqlCommand("DELETE FROM ppiq_meta.job_run_block_evidence WHERE id=@id", connection, transaction))
            {
                delete.Parameters.AddWithValue("id", probeId);
                var denied = await Assert.ThrowsAsync<PostgresException>(async () => { await delete.ExecuteNonQueryAsync(); });
                Assert.Equal("42501", denied.SqlState);
                Assert.Contains("job_run_block_evidence", denied.MessageText, StringComparison.Ordinal);
            }
            await transaction.RollbackAsync();
        }
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var deleteParent = new NpgsqlCommand("DELETE FROM ppiq_meta.job_run_histories WHERE id=@id", connection, transaction);
            deleteParent.Parameters.AddWithValue("id", runId);
            var restricted = await Assert.ThrowsAsync<PostgresException>(async () => { await deleteParent.ExecuteNonQueryAsync(); });
            Assert.Equal("23503", restricted.SqlState);
            Assert.Equal("fk_job_run_block_evidence_run", restricted.ConstraintName);
            await transaction.RollbackAsync();
        }
        Assert.DoesNotContain(await EvidenceAsync(runId), e => e.Id == probeId);
        Assert.NotEmpty(await EvidenceAsync(runId));
    }

}
