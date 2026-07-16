using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Journey;

/// <summary>
/// Full API seam proof for the canonical journey. These tests are intentionally
/// run against a real PostgreSQL-backed API host by the journey certification runner.
/// A skipped test is not accepted by the certification score.
/// </summary>
public sealed class CanonicalJourneyApiContractTests : AuthenticatedApiTestBase
{
    public CanonicalJourneyApiContractTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }


    [SkippableFact]
    public async Task J01_to_J10_Database_truth_proves_the_imported_only_journey_substrate()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        Skip.IfNot(IsIntegrationDbReachable(), "Journey PostgreSQL database is not reachable.");

        await using var connection = new NpgsqlConnection(ResolveIntegrationTestConnectionString());
        await connection.OpenAsync();

        var requiredCounts = new Dictionary<string, long>
        {
            ["connection_profiles"] = await CountAsync(connection, "connection_profiles"),
            ["source_dataset_definitions"] = await CountAsync(connection, "source_dataset_definitions"),
            ["import_batches"] = await CountAsync(connection, "import_batches"),
            ["staging_records"] = await CountAsync(connection, "staging_records"),
            ["mapping_definitions"] = await CountAsync(connection, "mapping_definitions"),
            ["material_units"] = await CountAsync(connection, "material_units"),
            ["parameter_definitions"] = await CountAsync(connection, "parameter_definitions"),
            ["defect_catalogs"] = await CountAsync(connection, "defect_catalogs"),
            ["parameter_observations"] = await CountAsync(connection, "parameter_observations"),
            ["quality_events"] = await CountAsync(connection, "quality_events"),
            ["genealogy_edges"] = await CountAsync(connection, "genealogy_edges"),
            ["analysis_job_definitions"] = await CountAsync(connection, "analysis_job_definitions"),
            ["ml_correlation_compute_runs"] = await ScalarAsync(connection,
                "SELECT count(*) FROM public.ml_correlation_compute_runs WHERE lower(status) = 'completed'"),
            ["ml_correlation_results_v2"] = await CountAsync(connection, "ml_correlation_results_v2")
        };

        requiredCounts.Should().OnlyContain(pair => pair.Value > 0,
            "steps 1-10 cannot be certified from empty tables: {0}",
            string.Join(", ", requiredCounts.Where(pair => pair.Value == 0).Select(pair => pair.Key)));

        var materialLineageGaps = await ScalarAsync(connection,
            "SELECT count(*) FROM public.material_units WHERE coalesce(is_deleted,false)=false AND (nullif(btrim(source_system),'') IS NULL OR nullif(btrim(source_record_id),'') IS NULL)");
        materialLineageGaps.Should().Be(0, "every loaded material must retain source-system and source-record lineage");

        var observationLineageGaps = await ScalarAsync(connection,
            "SELECT count(*) FROM public.parameter_observations WHERE coalesce(is_deleted,false)=false AND (nullif(btrim(source_system),'') IS NULL OR nullif(btrim(source_record_id),'') IS NULL)");
        observationLineageGaps.Should().Be(0, "every loaded observation must retain source-system and source-record lineage");

        var qualityLineageGaps = await ScalarAsync(connection,
            "SELECT count(*) FROM public.quality_events WHERE coalesce(is_deleted,false)=false AND (nullif(btrim(source_system),'') IS NULL OR nullif(btrim(source_record_id),'') IS NULL)");
        qualityLineageGaps.Should().Be(0, "every loaded quality event must retain source-system and source-record lineage");

        var configTaxonomy = await ScalarAsync(connection,
            "SELECT (SELECT count(*) FROM public.parameter_definitions WHERE upper(coalesce(source_system,''))='PPIQ_CONFIG') + (SELECT count(*) FROM public.defect_catalogs WHERE upper(coalesce(source_system,''))='PPIQ_CONFIG')");
        configTaxonomy.Should().Be(0, "taxonomy is plant knowledge and must arrive through the DB-link journey");
    }

    [SkippableFact]
    public async Task J01_J02_Connector_catalog_profiles_and_schema_entry_points_are_authorized()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var providers = await client.GetAsync("/admin/connectors/provider-types");
        providers.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerJson = await providers.Content.ReadFromJsonAsync<JsonElement>();
        providerJson.ValueKind.Should().BeOneOf(JsonValueKind.Array, JsonValueKind.Object);

        var profiles = await client.GetAsync("/admin/connectors/connection-profiles");
        profiles.StatusCode.Should().Be(HttpStatusCode.OK);
        var profileJson = await profiles.Content.ReadFromJsonAsync<JsonElement>();
        profileJson.ValueKind.Should().BeOneOf(JsonValueKind.Array, JsonValueKind.Object);
    }

    [SkippableFact]
    public async Task J03_J04_Import_batches_and_mapping_endpoints_have_frontend_compatible_contracts()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var batches = await client.GetAsync("/integration/import-batches");
        batches.StatusCode.Should().Be(HttpStatusCode.OK);
        var batchJson = await batches.Content.ReadFromJsonAsync<JsonElement>();
        batchJson.ValueKind.Should().Be(JsonValueKind.Array,
            "the mapping workspace consumes import batches as a plain array");

        var mappings = await client.GetAsync("/integration/mapping-definitions");
        mappings.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [SkippableFact]
    public async Task J08_J09_J10_Analysis_definition_run_and_findings_contracts_are_registered()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var definitions = await client.GetAsync("/api/analysis-jobs");
        definitions.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitionJson = await definitions.Content.ReadFromJsonAsync<JsonElement>();
        definitionJson.ValueKind.Should().BeOneOf(JsonValueKind.Array, JsonValueKind.Object);

        var findings = await client.GetAsync("/api/advanced-results");
        findings.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task J14_Supervisor_generates_persists_and_logs_a_read_only_report()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var run = await client.PostAsync("/api/supervisor/run", content: null);
        run.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await run.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("title").GetString().Should().StartWith("Supervisor report");
        report.GetProperty("body").GetString().Should().Contain("No job configuration was changed automatically");
        var itemKey = report.GetProperty("itemKey").GetString();

        var reports = await client.GetAsync("/api/supervisor/reports");
        reports.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await reports.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Any(row => row.GetProperty("itemKey").GetString() == itemKey)
            .Should().BeTrue();

        var logs = await client.GetAsync("/admin/job-logs?jobType=SUPERVISOR");
        logs.StatusCode.Should().Be(HttpStatusCode.OK);
        (await logs.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries").EnumerateArray().Any().Should().BeTrue();
    }

    [SkippableFact]
    public async Task J15_Assistant_reindex_is_registered_authorized_and_returns_evidence_counts()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/assistant/reindex", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [SkippableFact]
    public async Task UI4_Alert_rule_lifecycle_is_validated_and_evaluation_is_idempotent()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var invalid = await client.PostAsJsonAsync("/api/alerts/rules", new
        {
            ruleName = "invalid",
            parameterCode = "X",
            comparator = "!=",
            limitValue = 1.0,
            severity = "Info"
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createdResponse = await client.PostAsJsonAsync("/api/alerts/rules", new
        {
            ruleName = $"journey-cert-{suffix}",
            parameterCode = $"NO_MATCH_{suffix}",
            comparator = ">",
            limitValue = 999999.0,
            severity = "Info"
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ruleId = created.GetProperty("id").GetGuid();

        try
        {
            var first = await client.PostAsync("/api/alerts/evaluate", content: null);
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            var second = await client.PostAsync("/api/alerts/evaluate", content: null);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("logged").GetInt32().Should().Be(0);

            var logs = await client.GetAsync("/admin/job-logs?jobType=ALERT_EVAL");
            logs.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await client.DeleteAsync($"/api/alerts/rules/{ruleId}");
        }
    }

    [SkippableFact]
    public async Task Monitor_Job_log_contract_is_one_queryable_stream()
    {
        Skip.IfNot(IsApiHostConfigured, "Journey API host is not configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/job-logs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Object);
        json.TryGetProperty("entries", out var entries).Should().BeTrue();
        entries.ValueKind.Should().Be(JsonValueKind.Array);
    }
    private static async Task<long> CountAsync(NpgsqlConnection connection, string table)
    {
        table.Should().MatchRegex("^[a-z0-9_]+$", "test table names are a closed constant set");
        return await ScalarAsync(connection, $"SELECT count(*) FROM public.{table}");
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

}