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

/// <summary>Tranche-2 contract guards: mapping create/execute never-500,
/// deny-by-default on the new engine surfaces, monitor filter robustness.</summary>
public sealed class ContractGuardsIntegrationTests : AuthenticatedApiTestBase
{
    public ContractGuardsIntegrationTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [SkippableFact]
    public async Task Mapping_create_with_bogus_source_definition_fails_cleanly_not_500()
    {
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var res = await client.PostAsJsonAsync("/integration/mapping-definitions", new
        {
            sourceSystemDefinitionId = Guid.NewGuid(),
            mappingCode = "IT-BOGUS-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            mappingName = "bogus",
            sourceObjectName = "no_such_object",
            targetEntityName = "MaterialUnit",
            mappingJson = "{\"MaterialCode\":\"x\"}",
            mappingVersion = "v1",
            description = "contract guard",
            isSynthetic = true,
            sourceSystem = (string?)null,
            sourceRecordId = (string?)null
        });

        ((int)res.StatusCode).Should().BeLessThan(500,
            "a bad reference must be a clean 4xx, never an unhandled 500");
    }

    [SkippableFact]
    public async Task Mapping_execute_query_string_binding_never_500()
    {
        // Audit A1-C5: "mapping execute query-string binding (the 400 we hit)".
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var url = $"/integration/mapping-definitions/{Guid.NewGuid()}/execute?importBatchId={Guid.NewGuid()}&stopOnFirstError=false";
        var res = await client.PostAsync(url, content: null);

        ((int)res.StatusCode).Should().BeLessThan(500, "unknown ids must be a clean 4xx");
        if (res.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await res.Content.ReadAsStringAsync();
            body.Should().NotContainEquivalentOf("could not bind",
                "a binding-flavored 400 means the query-string regression returned");
        }
    }

    [SkippableFact]
    public async Task New_engine_surfaces_are_deny_by_default_for_anonymous()
    {
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        var anon = CreateAnonymousClient();

        var sup = await anon.PostAsync("/api/supervisor/run", content: null);
        sup.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        var eval = await anon.PostAsync("/api/alerts/evaluate", content: null);
        eval.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Job_log_filter_with_unknown_type_returns_empty_not_error()
    {
        Skip.IfNot(IsApiHostConfigured, "No API test host configured.");
        using var client = await CreateAuthenticatedClientAsync();

        var res = await client.GetAsync("/admin/job-logs?jobType=NO_SUCH_TYPE_XYZ");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("entries").EnumerateArray().Any().Should().BeFalse();
    }
}