const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const file = path.join(
  root,
  "Backend",
  "tests",
  "PlantProcess.Api.IntegrationTests",
  "Smoke",
  "ApiEndpointCatalogSmokeTests.cs"
);

const content = `using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Smoke;

public sealed class ApiEndpointCatalogSmokeTests : AuthenticatedApiTestBase
{
    public ApiEndpointCatalogSmokeTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    public static IEnumerable<object[]> PublicOrLightweightEndpoints()
    {
        yield return new object[] { "Health", "/health" };
        yield return new object[] { "DB health", "/db-health" };
    }

    public static IEnumerable<object[]> AuthenticatedStableGetEndpoints()
    {
        yield return new object[] { "Admin overview", "/admin/overview" };
        yield return new object[] { "Admin jobs monitor", "/admin/jobs-monitor" };
        yield return new object[] { "Admin two-stage import model", "/admin/two-stage-import-model" };

        yield return new object[] { "Connector provider types", "/admin/connectors/provider-types" };
        yield return new object[] { "Connection profiles", "/admin/connectors/connection-profiles?includeInactive=true" };
        yield return new object[] { "Source datasets", "/admin/connectors/datasets?includeInactive=true" };

        yield return new object[] { "Schema configuration summary", "/admin/schema-configuration/summary" };

        yield return new object[] { "Dashboard overview", "/analytics/dashboard/overview" };
        yield return new object[] { "Dashboard metadata", "/analytics/dashboard/metadata" };

        // Dashboard definitions are template-backed. The canonical validation route first ensures/repairs
        // system templates and then reads definitions with explicit includeSystemTemplates=true.
        yield return new object[]
        {
            "Dashboard definitions",
            "/analytics/dashboard/definitions?includeInactive=false&includeSystemTemplates=true"
        };

        yield return new object[] { "Dashboard risk", "/analytics/dashboard/risk" };
        yield return new object[] { "Dashboard data quality", "/analytics/dashboard/data-quality" };

        yield return new object[] { "Data quality issues", "/data-quality/issues" };
        yield return new object[] { "Data quality scan preview", "/data-quality/scan-preview" };
    }

    [Theory]
    [MemberData(nameof(PublicOrLightweightEndpoints))]
    public async Task Lightweight_endpoint_should_not_return_server_error(
        string name,
        string url)
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, $"{name} must not return 500. Body: {body}");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, $"{name} route must exist. Body: {body}");
    }

    [Theory]
    [MemberData(nameof(AuthenticatedStableGetEndpoints))]
    public async Task Authenticated_stable_get_endpoint_should_not_return_server_error(
        string name,
        string url)
    {
        using var client = await CreateAuthenticatedClientAsync();

        if (name == "Dashboard definitions")
        {
            await EnsureDashboardDefinitionsAreReadyAsync(client);
        }

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, $"{name} must not return 500. Body: {body}");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, $"{name} route must exist. Body: {body}");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Unauthorized, $"{name} must accept authenticated admin user. Body: {body}");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Forbidden, $"{name} must accept authenticated admin user role/policy. Body: {body}");
    }

    private static async Task EnsureDashboardDefinitionsAreReadyAsync(HttpClient client)
    {
        var ensureResponse = await client.PostAsJsonAsync(
            "/analytics/dashboard/definitions/system-templates/ensure",
            new { });

        var ensureBody = await ensureResponse.Content.ReadAsStringAsync();

        ensureResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, $"dashboard template ensure must not return 500. Body: {ensureBody}");

        ensureResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, $"dashboard template ensure route must exist. Body: {ensureBody}");

        var repairResponse = await client.PostAsJsonAsync(
            "/analytics/dashboard/definitions/system-templates/repair",
            new { });

        var repairBody = await repairResponse.Content.ReadAsStringAsync();

        repairResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, $"dashboard template repair must not return 500. Body: {repairBody}");

        repairResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, $"dashboard template repair route must exist. Body: {repairBody}");
    }
}
`;

fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");

console.log("Replaced ApiEndpointCatalogSmokeTests.cs with canonical dashboard definitions preflight smoke test.");
