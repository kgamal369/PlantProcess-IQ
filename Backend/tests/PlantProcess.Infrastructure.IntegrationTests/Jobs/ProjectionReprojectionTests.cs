using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
using PlantProcess.Application.Jobs.Admission;
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
using PlantProcess.Infrastructure.IntegrationTests.Acquisition;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;
using PlantProcess.Infrastructure.Jobs;
using PlantProcess.Infrastructure.Jobs.Transformations;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Canonical projection provenance and reprojection through real accepted records, a
/// sealed batch, published Transformation versions and the production job path:
/// exact-version lineage per row, idempotent replay, authorised supersession on the same
/// canonical identity, rollback to the earlier effect, ordinary conflict refusal, stale
/// generation refusal, a genuine uniqueness race, failure atomicity between the canonical
/// write and its lineage, and tenant-scoped denial.
/// </summary>
[Collection("CanonicalDefinitionStore")]
public sealed class ProjectionReprojectionTests : IAsyncLifetime
{
    private const string Schema = "ppiq_staging";
    private const string Reproject = "{\"projection\":\"reproject\"}";

    private readonly DefinitionStoreFixture _fixture;
    private readonly string _codePrefix = "provenance_" + Guid.NewGuid().ToString("N") + "_";
    private readonly List<Guid> _jobs = new();
    private AcceptedRecordFixture _accepted = null!;
    private string _system = string.Empty;
    private string _relation = string.Empty;
    private Guid _batch;
    private Guid _siteId;
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ProjectionReprojectionTests(DefinitionStoreFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------- fixture ----

    public async Task InitializeAsync()
    {
        await using var db = _fixture.NewContext();
        var site = new Site("PR-" + Guid.NewGuid().ToString("N")[..8], "Projection provenance site", false);
        db.Set<Site>().Add(site);
        await db.SaveChangesAsync();
        _siteId = site.Id;

        _accepted = new AcceptedRecordFixture(_fixture);
        await _accepted.InitializeAsync(new Dictionary<string, string>
        {
            ["code"] = "string", ["unit_type"] = "string", ["site_id"] = "guid", ["zone"] = "string",
            ["offset_minutes"] = "int32", ["production_start_utc"] = "datetime", ["qty"] = "int32",
            ["family_a"] = "string", ["family_b"] = "string",
        });
        _system = _accepted.SourceSystem;

        var batch = await _accepted.OpenAsync();
        _batch = batch.BatchId;
        foreach (var row in new[] { ("UNIT-1", "r1"), ("UNIT-2", "r2") })
        {
            var accepted = await _accepted.AppendAsync(batch, row.Item2, new Dictionary<string, object?>
            {
                ["code"] = row.Item1, ["unit_type"] = "Batch", ["site_id"] = _siteId.ToString("D"), ["zone"] = "UTC",
                ["offset_minutes"] = 0, ["production_start_utc"] = "2026-01-01T00:00:00Z", ["qty"] = 9,
                ["family_a"] = "Alpha", ["family_b"] = "Beta",
            });
            Assert.True(accepted.IsAccepted, accepted.Refusal?.Detail);
        }

        _relation = (await _accepted.SealAsync(batch, "end-a")).RelationName!;
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

    private string Board(bool withFilter) =>
        "{\"nodes\":[{\"id\":\"" + _relation + "\",\"kind\":\"dataset\"}"
        + (withFilter ? ",{\"id\":\"only-large\",\"kind\":\"filter\"}" : string.Empty)
        + "],\"edges\":[" + (withFilter ? "{\"source\":\"" + _relation + "\",\"target\":\"only-large\"}" : string.Empty) + "]}";

    private string GraphJson(bool withFilter) =>
        "{\"name\":\"projection-provenance\",\"targetEntity\":\"" + nameof(MaterialUnit) + "\",\"tables\":[\"" + _relation
        + "\"],\"joins\":[],\"filters\":"
        + (withFilter ? "[{\"table\":\"" + _relation + "\",\"column\":\"qty\",\"op\":\">\",\"value\":\"5\"}]" : "[]")
        + ",\"board\":" + Board(withFilter) + "}";

    private string Projection(string familyColumn) =>
        "{\"targetEntity\":\"MaterialUnit\",\"fieldBindings\":["
        + Binding("MaterialCode", "code") + ","
        + Binding("MaterialUnitType", "unit_type") + ","
        + Binding("SiteId", "site_id") + ","
        + Binding("ProductFamily", familyColumn) + ","
        + Binding("ProductionStartUtc", "production_start_utc") + ","
        + Binding("PlantTimeZoneId", "zone") + ","
        + Binding("PlantUtcOffsetMinutes", "offset_minutes") + "]}";

    private string Binding(string field, string column) =>
        "{\"targetField\":\"" + field + "\",\"sourceKind\":\"column\",\"sourceTable\":\"" + _relation
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

    /// <summary>Saves a new version under one definition code and publishes it.</summary>
    private async Task<(Guid DefinitionId, int Version)> PublishVersionAsync(string code, bool withFilter, string familyColumn)
    {
        await using var db = _fixture.NewContext();
        var saved = await Lifecycle(db).SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, _codePrefix + code, "Projection provenance",
                GraphJson(withFilter), nameof(MaterialUnit), Projection(familyColumn)),
            CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var published = await new CanonicalDefinitionWriter(db)
                .PublishAsync(saved.Value!.DefinitionId, saved.Value.VersionNumber, CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);
            await transaction.CommitAsync();
        }

        return (saved.Value!.DefinitionId, saved.Value.VersionNumber);
    }

    private async Task<string> HashOfAsync(Guid definitionId, int version)
    {
        await using var db = _fixture.NewContext();
        await db.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT definition_hash FROM ppiq_meta.definition_versions WHERE definition_id = $1 AND version_number = $2",
            (NpgsqlConnection)db.Database.GetDbConnection());
        command.Parameters.AddWithValue(definitionId);
        command.Parameters.AddWithValue(version);
        string? hash = await command.ExecuteScalarAsync() as string;
        Assert.False(string.IsNullOrWhiteSpace(hash));
        return hash!;
    }

    private async Task<Guid> JobForAsync(Guid definitionId, int version, string? parameters)
    {
        await using var db = _fixture.NewContext();
        var job = new JobDefinition(
            "PROJPROV_" + Guid.NewGuid().ToString("N").Substring(0, 10),
            "Projection provenance probe", JobDefinitionType.CanonicalRefresh, "Manual", false);
        JobLaneAssignment.Initialize(job);
        job.AssignTargetDefinition(
            DefinitionKind.Transformation.ToString(), definitionId, JobTargetVersionPolicy.Pinned, version, parameters);
        db.JobDefinitions.Add(job);
        await db.SaveChangesAsync();
        _jobs.Add(job.Id);
        return job.Id;
    }

    // ------------------------------------------------------- the composition --

    private sealed class CountingImport : IImportBatchQueueProcessorService
    {
        public Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
            int maxBatches, int rowsPerBatch, bool stopOnFirstError, bool runDataQualityScan, CancellationToken ct) =>
            Task.FromResult(ApplicationResult<ImportQueueProcessingSummary>.Failure(
                ApplicationError.BusinessRule("The fixture import processor performs no work.")));
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

    private static JobRunOrchestratorService Orchestrator(PlantProcessDbContext db)
    {
        var authority = new JobExecutionCapabilityAuthority();
        var executor = new TransformationProjectionJobExecutor(
            new CanonicalEntityCatalog(db),
            new MaterialUnitTransformationWriter(db),
            new CanonicalDefinitionWriter(db),
            new NpgsqlTransformationSourceReader(db),
            new CanvasStagingSchema(Schema),
            new JobRunBlockEvidenceStore(db),
            new JobRunCancellationProbe(db));

        return new JobRunOrchestratorService(
            new RunnableJobLookup(db),
            new JobRuntimeService(db),
            new CountingImport(),
            new NoQuality(),
            new NoRisk(),
            authority,
            new JobTargetResolver(new DefinitionService(db), new CapabilityJobTargetClassPolicy(authority), new JobTargetLookup(db)),
            new JobDependencyService(db),
            new JobExecutorResolver(new IJobExecutor[] { executor }),
            new JobAdmissionController(new JobAdmissionOptionsConfigurationProvider(new JobAdmissionOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<JobAdmissionController>.Instance));
    }

    private async Task<JobActionResponseDto> RunAsync(Guid jobId)
    {
        await using var db = _fixture.NewContext();
        var result = await Orchestrator(db).RunNowAsync(jobId, "tester", null, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        return result.Value!;
    }

    private async Task<JobActionResponseDto> RunOkAsync(Guid jobId)
    {
        JobActionResponseDto run = await RunAsync(jobId);
        Assert.True(run.Status == JobRunStatus.Ok, "Expected a successful run: " + JsonSerializer.Serialize(run));
        return run;
    }

    // ------------------------------------------------------------ observation --

    private sealed record Effect(
        Guid EffectId, Guid UnitId, string RecordId, Guid DefinitionId, int Version, string Hash, string Kind, bool IsCurrent,
        Guid? Supersedes, Guid? Batch, Guid? Receipt, string? ContentHash, Guid? Dataset, string EffectHash,
        Guid RunId, long Generation);

    private async Task<List<Effect>> LedgerAsync(Guid tenant)
    {
        await using var db = _fixture.NewContext();
        await db.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        // The fixture connection may be a superuser. FORCE RLS does not constrain
        // superusers: observe through the actual runtime role, with no tenant WHERE filter.
        await using (var role = new NpgsqlCommand("SET LOCAL ROLE plantprocess_app", connection, transaction))
            await role.ExecuteNonQueryAsync();
        await using (var roleCheck = new NpgsqlCommand(
            "SELECT current_user = 'plantprocess_app' AND NOT rolsuper AND NOT rolbypassrls "
            + "FROM pg_roles WHERE rolname = current_user", connection, transaction))
            Assert.Equal(true, (bool)(await roleCheck.ExecuteScalarAsync())!);
        await using (var scope = new NpgsqlCommand("SELECT set_config('app.current_tenant', $1, true)", connection, transaction))
        {
            scope.Parameters.AddWithValue(tenant.ToString("D"));
            await scope.ExecuteScalarAsync();
        }

        await using var query = new NpgsqlCommand(
            "SELECT effect_id, material_unit_id, source_record_id, definition_id, definition_version, definition_hash, "
            + "effect_kind, is_current, supersedes_effect_id, source_batch_id, source_receipt_id, source_content_hash, "
            + "source_dataset_governance_id, effect_hash, job_run_history_id, projection_generation "
            + "FROM ppiq_plant.canonical_projection_effects WHERE source_system = $1 ORDER BY projection_generation, source_record_id",
            connection, transaction);
        query.Parameters.AddWithValue(_system);

        var rows = new List<Effect>();
        await using (var reader = await query.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add(new Effect(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetInt32(4),
                    reader.GetString(5), reader.GetString(6), reader.GetBoolean(7),
                    reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    reader.IsDBNull(9) ? null : reader.GetGuid(9),
                    reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetGuid(12),
                    reader.GetString(13).Trim(), reader.GetGuid(14), reader.GetInt64(15)));
            }
        }

        await transaction.CommitAsync();
        return rows;
    }

    private async Task<List<MaterialUnit>> UnitsAsync()
    {
        await using var db = _fixture.NewContext();
        return await db.MaterialUnits.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SourceSystem == _system)
            .OrderBy(x => x.SourceRecordId)
            .ToListAsync();
    }

    /// <summary>The deterministic business output. Clock and audit fields are compared separately.</summary>
    private static string Business(IEnumerable<MaterialUnit> units) =>
        string.Join("|", units.OrderBy(u => u.SourceRecordId, StringComparer.Ordinal).Select(u =>
            u.Id + ";" + u.SourceSystem + ";" + u.SourceRecordId + ";" + u.MaterialCode + ";" + u.MaterialUnitType + ";"
            + u.SiteId + ";" + u.ProductFamily + ";" + u.GradeOrRecipe + ";"
            + u.ProductionStartUtc?.Ticks + ";" + u.ProductionEndUtc?.Ticks + ";"
            + u.ProductionStartLocal?.Ticks + ";" + u.ProductionEndLocal?.Ticks + ";"
            + u.PlantTimeZoneId + ";" + u.PlantUtcOffsetMinutes));

    private static void AssertOneCurrentEach(List<Effect> ledger, List<MaterialUnit> units)
    {
        foreach (MaterialUnit unit in units)
        {
            Effect current = Assert.Single(ledger, e => e.UnitId == unit.Id && e.IsCurrent);
            Assert.True(current.Version > 0);
            Assert.False(string.IsNullOrWhiteSpace(current.Hash));
            Assert.Equal(MaterialUnitTransformationWriter.EffectHashOf(unit), current.EffectHash);
        }
    }

    // ------------------------------------------------------------ the proofs --

    [Fact]
    public async Task Published_v1_replay_v2_reprojection_and_explicit_rollback_keep_one_identity_with_exact_lineage()
    {
        var v1 = await PublishVersionAsync("main", false, "family_a");
        string hash1 = await HashOfAsync(v1.DefinitionId, v1.Version);
        Guid tenant = _fixture.TenantId;

        // First projection under the published v1.
        JobActionResponseDto first = await RunOkAsync(await JobForAsync(v1.DefinitionId, v1.Version, null));
        List<MaterialUnit> afterV1 = await UnitsAsync();
        Assert.Equal(new[] { "r1", "r2" }, afterV1.Select(u => u.SourceRecordId).ToArray());
        Assert.All(afterV1, u => Assert.Equal("Alpha", u.ProductFamily));
        string snapshotV1 = Business(afterV1);

        List<Effect> ledger = await LedgerAsync(tenant);
        Assert.Equal(2, ledger.Count);
        Assert.All(ledger, e =>
        {
            Assert.True(e.IsCurrent);
            Assert.Equal("Inserted", e.Kind);
            Assert.Equal(v1.DefinitionId, e.DefinitionId);
            Assert.Equal(v1.Version, e.Version);
            Assert.Equal(hash1, e.Hash);
            Assert.Equal(first.JobRunHistoryId, e.RunId);
            Assert.Equal(_batch, e.Batch);
            Assert.NotNull(e.Receipt);
            Assert.False(string.IsNullOrWhiteSpace(e.ContentHash));
            Assert.Equal(_accepted.Dataset, e.Dataset);
        });
        AssertOneCurrentEach(ledger, afterV1);
        var v1Effects = ledger.ToDictionary(e => e.UnitId, e => e.EffectHash);

        // Replay the same batch and version three times: nothing new, anywhere.
        Guid replayJob = await JobForAsync(v1.DefinitionId, v1.Version, null);
        for (int i = 0; i < 3; i++)
        {
            JobActionResponseDto replay = await RunOkAsync(replayJob);
            Assert.Contains("0 inserted, 2 unchanged, 0 superseded, 0 reattributed", replay.Message, StringComparison.Ordinal);
        }

        Assert.Equal(snapshotV1, Business(await UnitsAsync()));
        Assert.Equal(2, (await LedgerAsync(tenant)).Count);

        // A changed mapping without reprojection authority is an ordinary conflicting write.
        var v2 = await PublishVersionAsync("main", false, "family_b");
        Assert.Equal(v1.DefinitionId, v2.DefinitionId);
        string hash2 = await HashOfAsync(v2.DefinitionId, v2.Version);
        Assert.NotEqual(hash1, hash2);

        JobActionResponseDto refused = await RunAsync(await JobForAsync(v2.DefinitionId, v2.Version, null));
        Assert.Equal(JobRunStatus.Failed, refused.Status);
        Assert.Contains(JobExecutionDiagnosticCodes.CanonicalIdentityConflict, refused.Message, StringComparison.Ordinal);
        Assert.Equal(snapshotV1, Business(await UnitsAsync()));
        Assert.Equal(2, (await LedgerAsync(tenant)).Count);

        // Authorised reprojection under v2: same identities, new effect, exact v2 lineage.
        JobActionResponseDto reprojected = await RunOkAsync(await JobForAsync(v2.DefinitionId, v2.Version, Reproject));
        Assert.Contains("0 inserted, 0 unchanged, 2 superseded", reprojected.Message, StringComparison.Ordinal);
        List<MaterialUnit> afterV2 = await UnitsAsync();
        Assert.Equal(afterV1.Select(u => u.Id).ToArray(), afterV2.Select(u => u.Id).ToArray());
        Assert.All(afterV2, u => Assert.Equal("Beta", u.ProductFamily));

        ledger = await LedgerAsync(tenant);
        Assert.Equal(4, ledger.Count);
        AssertOneCurrentEach(ledger, afterV2);
        Assert.All(ledger.Where(e => e.IsCurrent), e =>
        {
            Assert.Equal("Superseded", e.Kind);
            Assert.Equal(v2.Version, e.Version);
            Assert.Equal(hash2, e.Hash);
            Assert.Equal(reprojected.JobRunHistoryId, e.RunId);
            Assert.NotNull(e.Supersedes);
            Assert.Contains(ledger, p => p.EffectId == e.Supersedes && !p.IsCurrent && p.Version == v1.Version);
        });

        // Explicit rollback to v1: the earlier deterministic business output, exactly.
        JobActionResponseDto rolledBack = await RunOkAsync(await JobForAsync(v1.DefinitionId, v1.Version, Reproject));
        Assert.Contains("2 superseded", rolledBack.Message, StringComparison.Ordinal);
        List<MaterialUnit> afterRollback = await UnitsAsync();
        Assert.Equal(snapshotV1, Business(afterRollback));
        Assert.All(afterRollback, u => Assert.NotNull(u.UpdatedAtUtc));

        ledger = await LedgerAsync(tenant);
        Assert.Equal(6, ledger.Count);
        AssertOneCurrentEach(ledger, afterRollback);
        Assert.All(ledger.Where(e => e.IsCurrent), e =>
        {
            Assert.Equal(v1.Version, e.Version);
            Assert.Equal(hash1, e.Hash);
            Assert.Equal(v1Effects[e.UnitId], e.EffectHash);
            Assert.Equal(rolledBack.JobRunHistoryId, e.RunId);
        });
        Assert.Equal(6, ledger.Select(e => e.EffectId).Distinct().Count());
        Assert.True(ledger.Where(e => e.IsCurrent).All(e => e.Generation > ledger.Where(p => !p.IsCurrent).Max(p => p.Generation)));
    }

    [Fact]
    public async Task The_same_effect_under_a_new_version_moves_current_lineage_and_leaves_the_row_untouched()
    {
        var v1 = await PublishVersionAsync("same", false, "family_a");
        await RunOkAsync(await JobForAsync(v1.DefinitionId, v1.Version, null));
        string before = Business(await UnitsAsync());
        List<DateTime?> touchedBefore = (await UnitsAsync()).Select(u => u.UpdatedAtUtc).ToList();

        var v2 = await PublishVersionAsync("same", true, "family_a");
        JobActionResponseDto run = await RunOkAsync(await JobForAsync(v2.DefinitionId, v2.Version, null));
        Assert.Contains("0 inserted, 0 unchanged, 0 superseded, 2 reattributed", run.Message, StringComparison.Ordinal);

        List<MaterialUnit> after = await UnitsAsync();
        Assert.Equal(before, Business(after));
        Assert.Equal(touchedBefore, after.Select(u => u.UpdatedAtUtc).ToList());

        List<Effect> ledger = await LedgerAsync(_fixture.TenantId);
        Assert.Equal(4, ledger.Count);
        Assert.All(ledger.Where(e => e.IsCurrent), e =>
        {
            Assert.Equal("Reattributed", e.Kind);
            Assert.Equal(v2.Version, e.Version);
            Assert.Equal(run.JobRunHistoryId, e.RunId);
        });
        Assert.Equal(2, ledger.Count(e => !e.IsCurrent && e.Version == v1.Version));
    }

    [Fact]
    public async Task A_delayed_reprojection_from_an_older_generation_is_refused_as_stale()
    {
        var v1 = await PublishVersionAsync("stale", false, "family_a");
        string hash1 = await HashOfAsync(v1.DefinitionId, v1.Version);
        await RunOkAsync(await JobForAsync(v1.DefinitionId, v1.Version, null));

        long delayedGeneration;
        await using (var reserve = _fixture.NewContext())
        {
            delayedGeneration = await new MaterialUnitTransformationWriter(reserve)
                .ReserveProjectionGenerationAsync(CancellationToken.None);
        }

        var v2 = await PublishVersionAsync("stale", false, "family_b");
        await RunOkAsync(await JobForAsync(v2.DefinitionId, v2.Version, Reproject));
        string afterV2 = Business(await UnitsAsync());
        int ledgerCount = (await LedgerAsync(_fixture.TenantId)).Count;

        await using var db = _fixture.NewContext();
        await using (await new NpgsqlTransformationSourceReader(db)
            .BindDefinitionAsync(v1.DefinitionId, v1.Version, CancellationToken.None))
        {
            var result = await new MaterialUnitTransformationWriter(db).WriteAsync(
                new CanonicalWriteRequest(nameof(MaterialUnit), v1.DefinitionId, v1.Version, hash1, Guid.NewGuid(),
                    CanonicalProjectionMode.Reproject, delayedGeneration,
                    new[] { Row("r1", "UNIT-1", "Alpha") }),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(JobExecutionDiagnosticCodes.ProjectionEffectStale, result.Error!.Code);
        }

        Assert.Equal(afterV2, Business(await UnitsAsync()));
        Assert.Equal(ledgerCount, (await LedgerAsync(_fixture.TenantId)).Count);
    }

    [Fact]
    public async Task A_uniqueness_race_for_one_new_identity_leaves_exactly_one_effect()
    {
        var v1 = await PublishVersionAsync("race", false, "family_a");
        string hash1 = await HashOfAsync(v1.DefinitionId, v1.Version);

        await using var first = _fixture.NewContext();
        await using var second = _fixture.NewContext();
        await using var firstScope = await new NpgsqlTransformationSourceReader(first)
            .BindDefinitionAsync(v1.DefinitionId, v1.Version, CancellationToken.None);
        await using var secondScope = await new NpgsqlTransformationSourceReader(second)
            .BindDefinitionAsync(v1.DefinitionId, v1.Version, CancellationToken.None);

        var firstWriter = new MaterialUnitTransformationWriter(first);
        var secondWriter = new MaterialUnitTransformationWriter(second);
        long g1 = await firstWriter.ReserveProjectionGenerationAsync(CancellationToken.None);
        long g2 = await secondWriter.ReserveProjectionGenerationAsync(CancellationToken.None);

        // The first write holds its uncommitted row inside the caller's transaction.
        await using var holding = await first.Database.BeginTransactionAsync();
        var held = await firstWriter.WriteAsync(
            new CanonicalWriteRequest(nameof(MaterialUnit), v1.DefinitionId, v1.Version, hash1, Guid.NewGuid(),
                CanonicalProjectionMode.Ordinary, g1, new[] { Row("race-1", "RACE-1", "Alpha") }),
            CancellationToken.None);
        Assert.True(held.IsSuccess, held.Error?.Message);

        // The second genuinely waits on the database's unique key, proven by a lock wait.
        Task<ApplicationResult<CanonicalWriteResult>> racing = Task.Run(() => secondWriter.WriteAsync(
            new CanonicalWriteRequest(nameof(MaterialUnit), v1.DefinitionId, v1.Version, hash1, Guid.NewGuid(),
                CanonicalProjectionMode.Ordinary, g2, new[] { Row("race-1", "RACE-1", "Alpha") }),
            CancellationToken.None));

        bool waiting = false;
        for (int i = 0; i < 150 && !waiting && !racing.IsCompleted; i++)
        {
            await Task.Delay(100);
            await using var probe = _fixture.NewContext();
            waiting = await probe.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'")
                .SingleAsync() > 0;
        }

        Assert.True(waiting, "The second write never waited on the first; the race was not exercised.");
        await holding.CommitAsync();

        ApplicationResult<CanonicalWriteResult> lost = await racing;
        Assert.True(lost.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.ProjectionEffectStale, lost.Error!.Code);

        MaterialUnit unit = Assert.Single(await UnitsAsync(), u => u.SourceRecordId == "race-1");
        Effect current = Assert.Single(await LedgerAsync(_fixture.TenantId), e => e.RecordId == "race-1");
        Assert.True(current.IsCurrent);
        Assert.Equal(unit.Id, current.UnitId);
        Assert.Equal(g1, current.Generation);
    }

    [Fact]
    public async Task A_failure_between_the_canonical_write_and_its_lineage_leaves_neither()
    {
        var v1 = await PublishVersionAsync("atomic", false, "family_a");
        Guid jobId = await JobForAsync(v1.DefinitionId, v1.Version, null);
        string trigger = "trg_lineage_fault_" + Guid.NewGuid().ToString("N")[..12];
        string function = "ppiq_plant." + trigger + "_fn";

        await using (var db = _fixture.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE FUNCTION " + function + "() RETURNS trigger LANGUAGE plpgsql AS $f$ BEGIN "
                + "IF NEW.source_system = '" + _system + "' AND NEW.source_record_id = 'r2' THEN "
                + "RAISE EXCEPTION 'injected lineage fault'; END IF; RETURN NEW; END $f$;"
                + "CREATE TRIGGER " + trigger + " BEFORE INSERT ON ppiq_plant.canonical_projection_effects "
                + "FOR EACH ROW EXECUTE FUNCTION " + function + "();");
        }

        try
        {
            JobActionResponseDto run = await RunAsync(jobId);
            Assert.Equal(JobRunStatus.Failed, run.Status);
            Assert.Contains(JobExecutionDiagnosticCodes.CanonicalWriteFailed, run.Message, StringComparison.Ordinal);
            Assert.Empty(await UnitsAsync());
            Assert.Empty(await LedgerAsync(_fixture.TenantId));
        }
        finally
        {
            await using var db = _fixture.NewContext();
            await db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER IF EXISTS " + trigger + " ON ppiq_plant.canonical_projection_effects;"
                + "DROP FUNCTION IF EXISTS " + function + "();");
        }

        JobActionResponseDto retry = await RunOkAsync(jobId);
        Assert.Contains("2 inserted", retry.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await LedgerAsync(_fixture.TenantId)).Count);
    }

    [Fact]
    public async Task An_identity_attributed_under_another_tenant_is_denied_and_invisible()
    {
        var v1 = await PublishVersionAsync("tenant", false, "family_a");
        Guid foreign = Guid.NewGuid();

        await using (var db = _fixture.NewContext())
        {
            var unit = new MaterialUnit("UNIT-1", "Batch", _siteId, "Alpha", null, false, _system, "r1");
            unit.SetProductionWindow(Start, null, TimeSpan.Zero, "UTC");
            db.MaterialUnits.Add(unit);
            await db.SaveChangesAsync();

            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.current_tenant', {foreign.ToString("D")}, true)");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ppiq_plant.canonical_projection_effects (tenant_id, target_entity, material_unit_id,
                   source_system, source_record_id, definition_id, definition_version, definition_hash, job_run_history_id,
                   projection_generation, projection_mode, effect_kind, effect_hash, is_current)
                   VALUES ({foreign}, 'MaterialUnit', {unit.Id}, {_system}, 'r1', {Guid.NewGuid()}, 1, 'foreign',
                   {Guid.NewGuid()}, 1, 'Ordinary', 'Inserted', {MaterialUnitTransformationWriter.EffectHashOf(unit)}, true)");
            await transaction.CommitAsync();
        }

        string before = Business(await UnitsAsync());

        JobActionResponseDto run = await RunAsync(await JobForAsync(v1.DefinitionId, v1.Version, null));
        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Contains(JobExecutionDiagnosticCodes.CanonicalIdentityConflict, run.Message, StringComparison.Ordinal);

        Assert.Equal(before, Business(await UnitsAsync()));
        Assert.Empty(await LedgerAsync(_fixture.TenantId));
        Effect foreignRow = Assert.Single(await LedgerAsync(foreign));
        Assert.Equal("foreign", foreignRow.Hash);
    }

    private CanonicalWriteRow Row(string record, string code, string family) =>
        new(_system, record, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(MaterialUnit.MaterialCode)] = code,
            [nameof(MaterialUnit.MaterialUnitType)] = "Batch",
            [nameof(MaterialUnit.SiteId)] = _siteId,
            [nameof(MaterialUnit.ProductFamily)] = family,
            [nameof(MaterialUnit.ProductionStartUtc)] = Start,
            [nameof(MaterialUnit.PlantTimeZoneId)] = "UTC",
            [nameof(MaterialUnit.PlantUtcOffsetMinutes)] = 0,
        });
}
