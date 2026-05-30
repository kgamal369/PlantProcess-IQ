// ============================================================
// FILE: Backend/tests/PlantProcess.Api.IntegrationTests/Infrastructure/AuthenticatedApiTestBase.cs
//
// Fix:
//   Resolves WebApplicationFactory IServiceProvider disposed failures.
//
// Why:
//   The previous version used factory.WithWebHostBuilder(...) inside the
//   base-class constructor. Across many xUnit integration test classes,
//   this can create cloned factories tied to a disposed provider lifecycle.
//   The symptom is:
//      ObjectDisposedException: Cannot access a disposed object.
//      Object name: 'IServiceProvider'.
//      at WebApplicationFactory<T>.CreateClient()
//
// Strategy:
//   1. Configure test settings through environment variables.
//   2. Use the injected WebApplicationFactory<Program> directly.
//   3. Avoid creating a new cloned factory per test class.
//   4. Keep one consistent test admin identity.
// ============================================================

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantProcess.Api.IntegrationTests.Infrastructure;

public abstract class AuthenticatedApiTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected const string TestAdminUserName = "admin";
    protected const string TestAdminPassword = "PpiqIntegrationAdmin!2026_Rotated";

    protected readonly WebApplicationFactory<Program> Factory;

    private static readonly object EnvironmentLock = new();
    private static bool _environmentConfigured;

    protected AuthenticatedApiTestBase(WebApplicationFactory<Program> factory)
    {
        ConfigureTestEnvironmentOnce();

        // Important:
        // Use the injected factory directly. Do NOT call WithWebHostBuilder here.
        // Creating a derived factory per test class is what triggers the disposed
        // IServiceProvider cascade in large integration suites.
        Factory = factory;
    }

    protected HttpClient CreateAnonymousClient()
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateAnonymousClient();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = TestAdminUserName,
            Password = TestAdminPassword
        });

        if (!loginResponse.IsSuccessStatusCode)
        {
            var body = await loginResponse.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                "Integration test login failed. " +
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

    private static void ConfigureTestEnvironmentOnce()
    {
        lock (EnvironmentLock)
        {
            if (_environmentConfigured)
            {
                return;
            }

            // Test host identity
            Set("ASPNETCORE_ENVIRONMENT", "Development");
            Set("DOTNET_ENVIRONMENT", "Development");

            // Database
            Set(
                "ConnectionStrings__PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=PpiqIntegrationDb!2026_LocalOnly");

            // CORS
            Set("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173");

            // Auth
            Set("PlantProcess__Auth__SigningKey", "SuperSecretTestKeyThatIsAtLeast32Bytes!!");
            Set("PlantProcess__Auth__Issuer", "PlantProcessIQ");
            Set("PlantProcess__Auth__Audience", "PlantProcessIQ.Client");
            Set("PlantProcess__Auth__AccessTokenMinutes", "60");

            // Important:
            // Keep bootstrap disabled for integration tests.
            // This avoids conflicts with bootstrap-lock hardening.
            Set(
                "PlantProcess__Auth__BootstrapAdminUser",
                "__bootstrap_disabled_for_integration_tests__");

            Set(
                "PlantProcess__Auth__BootstrapAdminPassword",
                "BootstrapDisabledOnlyForTests123!");

            // Real configured test admin identity.
            Set("PlantProcess__Auth__Users__0__UserName", TestAdminUserName);
            Set("PlantProcess__Auth__Users__0__Password", TestAdminPassword);
            Set("PlantProcess__Auth__Users__0__Role", "Admin");
            Set("PlantProcess__Auth__Users__0__DisplayName", "Integration Test Admin");
            Set("PlantProcess__Auth__Users__0__IsBootstrapAdmin", "false");
            Set("PlantProcess__Auth__Users__0__ForcePasswordChangeOnFirstLogin", "false");

            // Plant defaults
            Set("PlantProcess__PlantTimeZoneId", "Europe/Berlin");
            Set("PlantProcess__PlantUtcOffsetMinutes", "60");

            _environmentConfigured = true;
        }
    }

    private static void Set(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
    }
}