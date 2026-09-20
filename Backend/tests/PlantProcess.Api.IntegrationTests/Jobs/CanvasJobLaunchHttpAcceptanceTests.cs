using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Relationships;
using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Definitions.Canvas;
using PlantProcess.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PlantProcess.Api.IntegrationTests.Jobs;

// Real HTTP host and database. No executor, reader, writer or evidence replacement.
[Trait("Task", "T-245")]
public sealed class CanvasJobLaunchHttpAcceptanceTests : IAsyncLifetime
{
    private const string Schema = "ppiq_staging";
    private readonly string Relation = "canvas_source_" + Guid.NewGuid().ToString("N");
    private readonly string SlowRelation = "canvas_gate_" + Guid.NewGuid().ToString("N");
    private readonly long _gate = Random.Shared.NextInt64(1, long.MaxValue);

    private readonly string _code = "t090test_t245acc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private readonly string _slowCode = "t090test_t245slow_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private readonly string _system = "t245acc-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private readonly List<Guid> _jobs = new();

    private readonly ITestOutputHelper _output;
    public CanvasJobLaunchHttpAcceptanceTests(ITestOutputHelper output) => _output = output;

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _ownerId;
    private Guid _siteId;
    private Guid _definitionId;


    // ----------------------------------------------------------------- fixture --

    public async Task InitializeAsync()
    {
        _client = await CanvasLaunchAcceptanceSupport.AuthenticatedClientAsync();
        _tenantId = CanvasLaunchAcceptanceSupport.TenantOf(_client);

        await using var db = CanvasLaunchAcceptanceSupport.NewContext();

        _ownerId = await db.Database
            .SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM ppiq_meta.app_users WHERE tenant_id = {0} ORDER BY created_at_utc LIMIT 1", _tenantId)
            .FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            "DROP VIEW IF EXISTS " + Schema + "." + SlowRelation + ";"
            + "DROP TABLE IF EXISTS " + Schema + "." + Relation + ";"
            + "CREATE TABLE " + Schema + "." + Relation + " ("
            + "code text, unit_type text, site_id uuid, zone text, offset_minutes integer, production_start_utc timestamptz, "
            + "source_system varchar(100), source_record_id varchar(200));");

        // Transaction advisory lock: the fixture owns the same key until it observes
        // the real reader waiting. No elapsed-time assumption decides disconnection.
        await db.Database.ExecuteSqlRawAsync(
            "CREATE VIEW " + Schema + "." + SlowRelation + " AS SELECT "
            + "CASE WHEN pg_advisory_xact_lock(" + _gate + "::bigint) IS NULL THEN s.code ELSE s.code END AS code, "
            + "s.unit_type, s.site_id, s.zone, s.offset_minutes, s.production_start_utc, s.source_system, s.source_record_id "
            + "FROM " + Schema + "." + Relation + " s;");

        var site = new Site("T245ACC-" + Guid.NewGuid().ToString("N").Substring(0, 8), "T-245 acceptance site", false);
        db.Set<Site>().Add(site);
        await db.SaveChangesAsync();
        _siteId = site.Id;

        await SeedAsync(("ACC-1", "r1"), ("ACC-2", "r2"));
        _definitionId = await PublishDefinitionAsync(_code, Relation);
    }

    // Fixtures have unique relations and identities. The runner stops its host and
    // drops its entire disposable database, including partially initialized fixtures.
    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    private async Task SeedAsync(params (string Code, string Record)[] rows)
    {
        await using var db = CanvasLaunchAcceptanceSupport.NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE " + Schema + "." + Relation + ";");

        foreach (var row in rows)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO " + Schema + "." + Relation
                + " (code, unit_type, site_id, zone, offset_minutes, production_start_utc, source_system, source_record_id) VALUES ('"
                + row.Code + "', 'AcceptanceUnit', '" + _siteId + "', 'UTC', 0, TIMESTAMPTZ '2026-01-01 00:00:00+00', '" + _system + "', "
                + (row.Record.Length == 0 ? "NULL" : "'" + row.Record + "'") + ");");
        }
    }

    private async Task<Guid> PublishDefinitionAsync(string code, string relation)
    {
        await using var db = CanvasLaunchAcceptanceSupport.NewContext();

        var lifecycle = new CanvasDefinitionLifecycleService(
            db,
            new CanonicalDefinitionWriter(db),
            new CanvasCompatibilityProjection(),
            new CanonicalEntityCatalog(db),
            new CanvasStagingSchema(Schema),
            new NoRelationships());

        string board = "{\"nodes\":[{\"id\":\"" + relation + "\",\"kind\":\"dataset\"}],\"edges\":[]}";
        string graph = "{\"name\":\"t245-acceptance\",\"targetEntity\":\"" + nameof(MaterialUnit) + "\",\"tables\":[\""
            + relation + "\"],\"joins\":[],\"filters\":[],\"board\":" + board + "}";

        string projection = "{\"targetEntity\":\"" + nameof(MaterialUnit) + "\",\"fieldBindings\":["
            + Binding(relation, "MaterialCode", "code") + "," + Binding(relation, "MaterialUnitType", "unit_type") + ","
            + Binding(relation, "SiteId", "site_id") + "," + Binding(relation, "ProductionStartUtc", "production_start_utc") + ","
            + Binding(relation, "PlantTimeZoneId", "zone") + ","
            + Binding(relation, "PlantUtcOffsetMinutes", "offset_minutes") + "]}";

        var saved = await lifecycle.SaveGraphAsync(
            new CanvasGraphSave(_tenantId, _ownerId, code, "T-245 acceptance", graph, nameof(MaterialUnit), projection),
            CancellationToken.None);

        Assert.True(saved.IsSuccess, saved.Error?.Message);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var published = await new CanonicalDefinitionWriter(db)
            .PublishAsync(saved.Value!.DefinitionId, saved.Value.VersionNumber, CancellationToken.None);
        Assert.True(published.IsSuccess, published.Error?.Message);
        await transaction.CommitAsync();

        return saved.Value!.DefinitionId;
    }

    private static string Binding(string relation, string field, string column) =>
        "{\"targetField\":\"" + field + "\",\"sourceKind\":\"column\",\"sourceTable\":\"" + relation
        + "\",\"sourceField\":\"" + column + "\"}";

    private sealed class NoRelationships : IRelationshipPublicationService
    {
        public Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> PublishAsync(
            RelationshipPublicationRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The acceptance never publishes relationships.");

        public Task<ApplicationResult<int>> RetireByDefinitionAsync(
            Guid sourceDefinitionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The acceptance never retires relationships.");
    }

    // ------------------------------------------------------------------ helpers --

    private string BaseFor(string code) => "/api/prep/definitions/" + Uri.EscapeDataString(code);

    private async Task<JsonElement> BindAsync(string code)
    {
        var response = await _client.PostAsJsonAsync(BaseFor(code) + "/job-binding", new { pinnedVersion = 1, jobName = (string?)null });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        JsonElement binding = await response.Content.ReadFromJsonAsync<JsonElement>();
        _jobs.Add(binding.GetProperty("jobDefinitionId").GetGuid());
        return binding;
    }

    private async Task<JsonElement?> FindRunAsync(string code, string correlation)
    {
        var response = await _client.GetAsync(BaseFor(code) + "/runs?correlationId=" + Uri.EscapeDataString(correlation));

        if (response.StatusCode == HttpStatusCode.NoContent) { return null; }

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> AwaitAsync(string code, string correlation, Func<JsonElement, bool> until, TimeSpan timeout, string what)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            JsonElement? run = await FindRunAsync(code, correlation);

            if (run.HasValue && until(run.Value)) { return run.Value; }

            await Task.Delay(200);
        }

        throw new TimeoutException("The run for " + correlation + " never reached: " + what);
    }

    private async Task<int> MaterialUnitCountAsync()
    {
        await using var db = CanvasLaunchAcceptanceSupport.NewContext();

        return await db.MaterialUnits.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => x.SourceSystem == _system);
    }

    // ------------------------------------------------------------------- proofs --

    [Fact]
    public async Task A_bound_canvas_launches_through_the_endpoint_and_returns_its_own_run_evidence()
    {
        JsonElement binding = await BindAsync(_code);
        Assert.Equal(1, binding.GetProperty("pinnedVersion").GetInt32());

        var capability = await _client.GetFromJsonAsync<JsonElement>(BaseFor(_code) + "/execution-capability?version=1");
        Assert.True(capability.GetProperty("staticallyEligible").GetBoolean(), capability.GetProperty("refusalDetail").ToString());

        string correlation = "acc-" + Guid.NewGuid().ToString("N");
        var launch = await _client.PostAsJsonAsync(BaseFor(_code) + "/runs", new { correlationId = correlation, requestedBy = "acceptance" });
        Assert.True(launch.IsSuccessStatusCode, await launch.Content.ReadAsStringAsync());

        JsonElement run = await launch.Content.ReadFromJsonAsync<JsonElement>();
        await AssertSuccessfulRunAsync(_code, run);
        Assert.Equal(1, run.GetProperty("targetDefinitionVersion").GetInt32());
        Assert.Equal(_definitionId, run.GetProperty("targetDefinitionId").GetGuid());
        Assert.Equal(correlation, run.GetProperty("correlationId").GetString());

        await AssertMaterialValuesAsync();

        JsonElement? attached = await FindRunAsync(_code, correlation);
        Assert.True(attached.HasValue);
        Guid runId = attached.Value.GetProperty("runId").GetGuid();
        Assert.Equal(run.GetProperty("runId").GetGuid(), runId);

        JsonElement read = await _client.GetFromJsonAsync<JsonElement>(BaseFor(_code) + "/runs/" + runId);
        JsonElement blocks = read.GetProperty("blocks");
        Assert.True(blocks.GetArrayLength() >= 1);
        Assert.Equal(Relation, blocks[0].GetProperty("blockId").GetString());
        Assert.Equal("Succeeded", blocks[0].GetProperty("status").GetString());
        Assert.Equal(2, blocks[blocks.GetArrayLength() - 1].GetProperty("outputRows").GetInt32());
    }

    [Fact]
    public async Task A_run_belonging_to_another_job_is_refused_by_this_definition()
    {
        await BindAsync(_code);

        Guid foreignRunId;

        await using (var db = CanvasLaunchAcceptanceSupport.NewContext())
        {
            var stranger = new JobDefinition(
                "T245ACC_OTHER_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                "Unrelated job", JobDefinitionType.DbLinkImport, "Manual", false);

            db.JobDefinitions.Add(stranger);
            await db.SaveChangesAsync();
            _jobs.Add(stranger.Id);

            var started = await new JobRuntimeService(db)
                .StartAsync(stranger.JobCode, "acceptance", "acceptance", null, CancellationToken.None);

            Assert.True(started.IsSuccess, started.Error?.Message);
            foreignRunId = started.Value!.Id;
        }

        var response = await _client.GetAsync(BaseFor(_code) + "/runs/" + foreignRunId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_typed_block_failure_is_returned_through_the_evidence_endpoint()
    {
        await SeedAsync(("ACC-1", "r1"), ("ACC-BAD", string.Empty));
        await BindAsync(_code);

        string correlation = "acc-" + Guid.NewGuid().ToString("N");
        var launch = await _client.PostAsJsonAsync(BaseFor(_code) + "/runs", new { correlationId = correlation, requestedBy = "acceptance" });

        JsonElement run = launch.IsSuccessStatusCode
            ? await launch.Content.ReadFromJsonAsync<JsonElement>()
            : (await FindRunAsync(_code, correlation))!.Value;

        Assert.Equal("Failed", run.GetProperty("status").GetString());
        Assert.Equal(0, await MaterialUnitCountAsync());

        JsonElement read = await _client.GetFromJsonAsync<JsonElement>(BaseFor(_code) + "/runs/" + run.GetProperty("runId").GetGuid());
        JsonElement blocks = read.GetProperty("blocks");
        JsonElement last = blocks[blocks.GetArrayLength() - 1];

        Assert.Equal("Failed", last.GetProperty("status").GetString());
        Assert.Equal("JOB_EXEC_SOURCE_IDENTITY_INVALID", last.GetProperty("diagnosticCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(read.GetProperty("failureReason").GetString()));
    }

    [Fact]
    public async Task Anonymous_callers_cannot_bind_launch_or_read_runs()
    {
        using var anonymous = CanvasLaunchAcceptanceSupport.AnonymousClient();
        using var bind = await anonymous.PostAsJsonAsync(BaseFor(_code) + "/job-binding", new { pinnedVersion = 1 });
        using var launch = await anonymous.PostAsJsonAsync(BaseFor(_code) + "/runs", new { correlationId = Guid.NewGuid().ToString("N") });
        using var read = await anonymous.GetAsync(BaseFor(_code) + "/runs?correlationId=unowned");
        foreach (var response in new[] { bind, launch, read })
            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                "Anonymous request returned " + response.StatusCode);
    }

    private async Task AssertSuccessfulRunAsync(string code, JsonElement run)
    {
        _output.WriteLine("Run response: " + run.GetRawText());
        if (run.GetProperty("status").GetString() == "Ok") return;

        // A completed HTTP request may legitimately return a failed governed run.
        // Persist the complete governed evidence in TRX before the disposable DB is removed.
        using var response = await _client.GetAsync(BaseFor(code) + "/runs/" + run.GetProperty("runId").GetGuid());
        string evidence = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Persisted run evidence: " + evidence);
        Assert.True(false, "Expected a successful governed run. Response: " + run.GetRawText()
            + "; evidence HTTP " + (int)response.StatusCode + ": " + evidence);
    }

    [Fact]
    public async Task A_zone_without_a_production_start_is_refused_without_canonical_writes()
    {
        // The former happy-path fixture was invalid in exactly this way. Keep it as
        // an intentional negative control through the real endpoint and writer.
        await using (var db = CanvasLaunchAcceptanceSupport.NewContext())
            await db.Database.ExecuteSqlRawAsync("UPDATE " + Schema + "." + Relation + " SET production_start_utc = NULL;");
        await BindAsync(_code);
        string correlation = "acc-" + Guid.NewGuid().ToString("N");
        using var response = await _client.PostAsJsonAsync(BaseFor(_code) + "/runs",
            new { correlationId = correlation, requestedBy = "acceptance" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        JsonElement run = await response.Content.ReadFromJsonAsync<JsonElement>();
        _output.WriteLine("Missing-start negative control: " + run.GetRawText());
        Assert.Equal("Failed", run.GetProperty("status").GetString());
        Assert.Equal(0, await MaterialUnitCountAsync());
        JsonElement read = await _client.GetFromJsonAsync<JsonElement>(BaseFor(_code) + "/runs/" + run.GetProperty("runId").GetGuid());
        _output.WriteLine("Missing-start persisted evidence: " + read.GetRawText());
        JsonElement blocks = read.GetProperty("blocks");
        Assert.True(blocks.GetArrayLength() > 0);
        JsonElement last = blocks[blocks.GetArrayLength() - 1];
        Assert.Equal("Failed", last.GetProperty("status").GetString());
        Assert.Equal("JOB_EXEC_CANONICAL_WRITE_FAILED", last.GetProperty("diagnosticCode").GetString());
        Assert.Contains("without a production start", read.GetProperty("failureReason").GetString() ?? string.Empty);
    }

    private async Task AssertMaterialValuesAsync()
    {
        await using var db = CanvasLaunchAcceptanceSupport.NewContext();
        var rows = await db.MaterialUnits.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SourceSystem == _system).OrderBy(x => x.MaterialCode).ToListAsync();
        Assert.Equal(new[] { "ACC-1", "ACC-2" }, rows.Select(x => x.MaterialCode));
        Assert.Equal(new[] { "r1", "r2" }, rows.Select(x => x.SourceRecordId));
        Assert.All(rows, row =>
        {
            Assert.Equal("AcceptanceUnit", row.MaterialUnitType);
            Assert.Equal(_siteId, row.SiteId);
            Assert.Equal(_system, row.SourceSystem);
            Assert.Equal<DateTime?>(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), row.ProductionStartUtc);
            Assert.Equal("UTC", row.PlantTimeZoneId);
            Assert.Equal(0, row.PlantUtcOffsetMinutes);
        });
    }

    [Fact]
    public async Task A_disconnected_client_does_not_stop_the_run_the_server_owns()
    {
        Guid definition = await PublishDefinitionAsync(_slowCode, SlowRelation);
        await BindAsync(_slowCode);
        string correlation = "acc-" + Guid.NewGuid().ToString("N");
        await using var gate = new NpgsqlConnection(CanvasLaunchAcceptanceSupport.ConnectionString());
        await gate.OpenAsync();
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", gate))
        {
            acquire.Parameters.AddWithValue("key", _gate);
            await acquire.ExecuteNonQueryAsync();
        }
        Guid runId = Guid.Empty;
        using var abort = new CancellationTokenSource();
        using var launchClient = await CanvasLaunchAcceptanceSupport.AuthenticatedClientAsync();
        Task<HttpResponseMessage>? launching = null;
        try
        {
            launching = launchClient.PostAsJsonAsync(BaseFor(_slowCode) + "/runs",
                new { correlationId = correlation, requestedBy = "acceptance" }, abort.Token);
            JsonElement running = await AwaitAsync(_slowCode, correlation,
                run => !run.GetProperty("isTerminal").GetBoolean()
                    && run.GetProperty("blocks").EnumerateArray().Any(block =>
                        block.GetProperty("blockId").GetString() == SlowRelation
                        && block.GetProperty("status").GetString() == "Running"),
                TimeSpan.FromSeconds(45), "the exact source block Running");
            runId = running.GetProperty("runId").GetGuid();
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            bool waiting = false;
            while (DateTime.UtcNow < deadline && !waiting)
            {
                await using var probe = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted AND database=(SELECT oid FROM pg_database WHERE datname=current_database()) AND classid::bigint=@hi AND objid::bigint=@lo AND objsubid=1)", gate);
                probe.Parameters.AddWithValue("hi", _gate >> 32);
                probe.Parameters.AddWithValue("lo", _gate & 0xffffffffL);
                waiting = (bool)(await probe.ExecuteScalarAsync())!;
                if (!waiting) await Task.Delay(100);
            }
            Assert.True(waiting, "The real source query never waited on this fixture's advisory lock.");
            Assert.False(launching.IsCompleted, "Launch completed before disconnection.");
            abort.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await launching; });
            launchClient.Dispose();
        }
        finally
        {
            abort.Cancel();
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", gate);
            release.Parameters.AddWithValue("key", _gate);
            await release.ExecuteScalarAsync();
            if (launching is not null) { try { using var ignored = await launching; } catch (OperationCanceledException) { } }
        }
        _client.Dispose();
        _client = await CanvasLaunchAcceptanceSupport.AuthenticatedClientAsync();
        JsonElement finished = await AwaitAsync(_slowCode, correlation,
            run => run.GetProperty("isTerminal").GetBoolean(), TimeSpan.FromSeconds(90),
            "a terminal outcome after reconnecting with a new client");
        Assert.Equal(runId, finished.GetProperty("runId").GetGuid());
        await AssertSuccessfulRunAsync(_slowCode, finished);
        Assert.Equal(definition, finished.GetProperty("targetDefinitionId").GetGuid());
        Assert.Equal(1, finished.GetProperty("targetDefinitionVersion").GetInt32());
        await AssertMaterialValuesAsync();
        JsonElement read = await _client.GetFromJsonAsync<JsonElement>(BaseFor(_slowCode) + "/runs/" + runId);
        Assert.Equal(correlation, read.GetProperty("correlationId").GetString());
        Assert.Equal(runId, read.GetProperty("runId").GetGuid());
        Assert.Equal(1, read.GetProperty("targetDefinitionVersion").GetInt32());
        Assert.Contains(read.GetProperty("blocks").EnumerateArray(), x =>
            x.GetProperty("blockId").GetString() == SlowRelation && x.GetProperty("status").GetString() == "Succeeded");
    }
}
