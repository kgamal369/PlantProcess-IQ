using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Smoke;

public sealed class ApiSmokeEndpointTests : AuthenticatedApiTestBase
{
    public ApiSmokeEndpointTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/admin/jobs-monitor")]
    [InlineData("/admin/overview")]
    [InlineData("/admin/two-stage-import-model")]
    [InlineData("/admin/db-configuration/summary")]
    [InlineData("/admin/schema-configuration/summary")]
    public async Task Smoke_endpoint_should_return_success(string url)
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"{url} must be reachable for the configured integration-test Admin user");
    }
}