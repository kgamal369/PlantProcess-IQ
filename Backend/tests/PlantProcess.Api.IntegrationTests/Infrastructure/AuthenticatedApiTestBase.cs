using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantProcess.Api.IntegrationTests.Infrastructure;

public abstract class AuthenticatedApiTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected const string TestAdminUserName = "admin";
    protected const string TestAdminPassword = "ChangeMe123!";

    protected readonly WebApplicationFactory<Program> Factory;

    protected AuthenticatedApiTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123");

            builder.UseSetting("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173");

            builder.UseSetting("PlantProcess:Auth:SigningKey", "SuperSecretTestKeyThatIsAtLeast32Bytes!!");
            builder.UseSetting("PlantProcess:Auth:Issuer", "PlantProcessIQ");
            builder.UseSetting("PlantProcess:Auth:Audience", "PlantProcessIQ.Client");
            builder.UseSetting("PlantProcess:Auth:AccessTokenMinutes", "60");

            // Important:
            // The bootstrap admin must NOT be the same identity used by tests.
            // Otherwise the lock-after-first-real-admin hardening correctly returns 403.
            builder.UseSetting("PlantProcess:Auth:BootstrapAdminUser", "__bootstrap_disabled_for_integration_tests__");
            builder.UseSetting("PlantProcess:Auth:BootstrapAdminPassword", "BootstrapDisabledOnlyForTests123!");

            // Real configured test admin.
            // This keeps the bootstrap lock behavior intact while giving tests a real Admin identity.
            builder.UseSetting("PlantProcess:Auth:Users:0:UserName", TestAdminUserName);
            builder.UseSetting("PlantProcess:Auth:Users:0:Password", TestAdminPassword);
            builder.UseSetting("PlantProcess:Auth:Users:0:Role", "Admin");
            builder.UseSetting("PlantProcess:Auth:Users:0:DisplayName", "Integration Test Admin");
            builder.UseSetting("PlantProcess:Auth:Users:0:IsBootstrapAdmin", "false");
            builder.UseSetting("PlantProcess:Auth:Users:0:ForcePasswordChangeOnFirstLogin", "false");

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
            UserName = TestAdminUserName,
            Password = TestAdminPassword
        });

        if (!loginResponse.IsSuccessStatusCode)
        {
            var body = await loginResponse.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Integration test login failed. " +
                $"Status={(int)loginResponse.StatusCode} {loginResponse.StatusCode}. " +
                $"Body={body}");
        }

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