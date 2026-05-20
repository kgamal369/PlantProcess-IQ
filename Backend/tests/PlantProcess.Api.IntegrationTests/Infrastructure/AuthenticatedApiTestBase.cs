using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantProcess.Api.IntegrationTests.Infrastructure;

public abstract class AuthenticatedApiTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> Factory;

    protected AuthenticatedApiTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123");

            builder.UseSetting("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173");

            builder.UseSetting("PlantProcess:Auth:BootstrapAdminUser", "admin");
            builder.UseSetting("PlantProcess:Auth:BootstrapAdminPassword", "ChangeMe123!");
            builder.UseSetting("PlantProcess:Auth:SigningKey", "SuperSecretTestKeyThatIsAtLeast32Bytes!!");
            builder.UseSetting("PlantProcess:Auth:Issuer", "PlantProcessIQ");
            builder.UseSetting("PlantProcess:Auth:Audience", "PlantProcessIQ.Client");
            builder.UseSetting("PlantProcess:Auth:AccessTokenMinutes", "60");

            builder.UseSetting("PlantProcess:PlantTimeZoneId", "Europe/Berlin");
            builder.UseSetting("PlantProcess:PlantUtcOffsetMinutes", "60");
        });
    }

    protected HttpClient CreateAnonymousClient()
    {
        return Factory.CreateClient();
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = Factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = "admin",
            Password = "ChangeMe123!"
        });

        loginResponse.EnsureSuccessStatusCode();

        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        string? token = null;

        if (json.TryGetProperty("accessToken", out var accessTokenProperty))
        {
            token = accessTokenProperty.GetString();
        }
        else if (json.TryGetProperty("token", out var tokenProperty))
        {
            token = tokenProperty.GetString();
        }

        token.Should().NotBeNullOrWhiteSpace("login must return a bearer token");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}