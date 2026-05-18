using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantProcess.Api.IntegrationTests.Health;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123");

            builder.UseSetting("PLANTPROCESS_ALLOWED_ORIGINS",
                "http://localhost:5173");
        });
    }

    [Fact]
    public async Task Health_endpoint_should_return_success()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
