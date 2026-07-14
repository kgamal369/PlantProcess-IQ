using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Engine;

/// <summary>
/// Durable evidence for the 13-Jul new surfaces (M1-01/02/04-contract/05/06).
/// SkippableFacts: run green with the live local DB, skip cleanly without it.
/// </summary>
public sealed class EngineSurfacesIntegrationTests : AuthenticatedApiTestBase
{
    public EngineSurfacesIntegrationTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [SkippableFact]
    public async Task Supervisor_run_writes_a_real_report_and_a_monitor_row()
    {
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var run = await client.PostAsync("/api/supervisor/run", content: null);
        run.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await run.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("id").GetGuid().Should().NotBeEmpty();
        report.GetProperty("title").GetString().Should().StartWith("Supervisor report");
        report.GetProperty("body").GetString().Should().Contain("No job configuration was changed automatically");
        report.TryGetProperty("findings", out _).Should().BeTrue();
        report.TryGetProperty("significant", out _).Should().BeTrue();

        var itemKey = report.GetProperty("itemKey").GetString();
        var list = await client.GetAsync("/api/supervisor/reports");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var reports = await list.Content.ReadFromJsonAsync<JsonElement>();
        reports.ValueKind.Should().Be(JsonValueKind.Array);
        reports.EnumerateArray().Any(r => r.GetProperty("itemKey").GetString() == itemKey)
            .Should().BeTrue("the report just written must be listed");

        var logs = await client.GetAsync("/admin/job-logs?jobType=SUPERVISOR");
        logs.StatusCode.Should().Be(HttpStatusCode.OK);
        var logJson = await logs.Content.ReadFromJsonAsync<JsonElement>();
        logJson.GetProperty("entries").EnumerateArray().Any()
            .Should().BeTrue("the run must surface in the jobs monitor (M1-02)");
    }

    [SkippableFact]
    public async Task Alert_rule_validation_rejects_bad_input_cleanly()
    {
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var badComparator = await client.PostAsJsonAsync("/api/alerts/rules",
            new { ruleName = "t", parameterCode = "X", comparator = "!=", limitValue = 1.0 });
        badComparator.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missingName = await client.PostAsJsonAsync("/api/alerts/rules",
            new { ruleName = "", parameterCode = "X", comparator = ">", limitValue = 1.0 });
        missingName.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Alert_lifecycle_create_evaluate_idempotent_log_delete()
    {
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var name = "it-rule-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var create = await client.PostAsJsonAsync("/api/alerts/rules",
            new { ruleName = name, parameterCode = "IT_NON_MATCHING_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                  comparator = ">", limitValue = 999999.0, severity = "Info" });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var ruleId = created.GetProperty("id").GetGuid();
        ruleId.Should().NotBeEmpty();

        var rules = await client.GetAsync("/api/alerts/rules");
        rules.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rules.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Any(r => r.GetProperty("id").GetGuid() == ruleId).Should().BeTrue();

        var eval1 = await client.PostAsync("/api/alerts/evaluate", content: null);
        eval1.StatusCode.Should().Be(HttpStatusCode.OK);
        var logged1 = (await eval1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logged").GetInt32();
        logged1.Should().BeGreaterThanOrEqualTo(0);

        var eval2 = await client.PostAsync("/api/alerts/evaluate", content: null);
        eval2.StatusCode.Should().Be(HttpStatusCode.OK);
        var logged2 = (await eval2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logged").GetInt32();
        logged2.Should().Be(0, "re-evaluation over unchanged observations must not double-log (unique rule+observation)");

        var log = await client.GetAsync("/api/alerts/log");
        log.StatusCode.Should().Be(HttpStatusCode.OK);
        (await log.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);

        var monitor = await client.GetAsync("/admin/job-logs?jobType=ALERT_EVAL");
        monitor.StatusCode.Should().Be(HttpStatusCode.OK);
        (await monitor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entries").EnumerateArray().Any()
            .Should().BeTrue("evaluate must surface in the jobs monitor (M1-02)");

        var del = await client.DeleteAsync($"/api/alerts/rules/{ruleId}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Assistant_reindex_is_registered_and_authorized()
    {
        // Audit A1-C5: the assistant-registration guard test that was never shipped.
        // A 404 here means AddAssistant/MapAssistantEndpoints regressed; a 403 means
        // the access-matrix row regressed. Both are the June incident classes.
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var reindex = await client.PostAsync("/api/assistant/reindex", content: null);
        reindex.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Import_batches_contract_is_a_plain_json_array()
    {
        // Locks the shape the M1-04 Load-to-Plant-Data page assumes.
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var batches = await client.GetAsync("/integration/import-batches");
        batches.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await batches.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Array,
            "the author-mapping page renders this as an array; a wrapper object would blank the dropdown");
    }
}