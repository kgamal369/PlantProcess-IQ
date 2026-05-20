using System.Net;
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
        yield return new object[] { "Dashboard definitions", "/analytics/dashboard/definitions" };
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

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, $"{name} must not return 500");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, $"{name} route must exist");
    }

    [Theory]
    [MemberData(nameof(AuthenticatedStableGetEndpoints))]
    public async Task Authenticated_stable_get_endpoint_should_not_return_server_error(
        string name,
        string url)
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(url);

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, $"{name} must not return 500");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, $"{name} route must exist");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Unauthorized, $"{name} must accept authenticated admin user");

        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Forbidden, $"{name} must accept authenticated admin user role/policy");
    }
}