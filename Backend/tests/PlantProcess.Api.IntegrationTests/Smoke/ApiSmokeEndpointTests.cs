using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Smoke;

public sealed class ApiSmokeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiSmokeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123");

            builder.UseSetting(
                "PLANTPROCESS_ALLOWED_ORIGINS",
                "http://localhost:5173,http://localhost:3000");
        });
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
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
