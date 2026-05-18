using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

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

            // Explicitly force test auth credentials
            builder.UseSetting("PlantProcess:Auth:BootstrapAdminUser", "admin");
            builder.UseSetting("PlantProcess:Auth:BootstrapAdminPassword", "password123");
            builder.UseSetting("PlantProcess:Auth:SigningKey", "SuperSecretTestKeyThatIsAtLeast32Bytes!!");
            builder.UseSetting("PlantProcess:Auth:Issuer", "TestIssuer");
            builder.UseSetting("PlantProcess:Auth:Audience", "TestAudience");
            builder.UseSetting("PlantProcess:Auth:AccessTokenMinutes", "60");
        });
    }

    [Fact]
    public async Task Health_endpoint_should_return_success()
    {
        using var client = _factory.CreateClient();

        // 1. Authenticate using the exact credentials forced in the test builder
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = "admin", 
            Password = "password123" 
        });

        loginResponse.EnsureSuccessStatusCode();

        // 2. Extract and attach the token
        var loginData = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginData.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // 3. Make the secured request
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}