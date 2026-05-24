using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Health;

public sealed class HealthEndpointTests : AuthenticatedApiTestBase
{
    public HealthEndpointTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Health_endpoint_should_return_success()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}