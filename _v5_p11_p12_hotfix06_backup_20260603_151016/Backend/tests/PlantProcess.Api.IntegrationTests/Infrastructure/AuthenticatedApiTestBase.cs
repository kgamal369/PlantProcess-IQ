using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace PlantProcess.Api.IntegrationTests.Infrastructure;

public abstract class AuthenticatedApiTestBase : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    protected const string TestAdminUserName = "admin";
    protected const string TestAdminPassword = "PpiqIntegrationAdmin!2026_Rotated";

    private readonly WebApplicationFactory<Program> _factory;

    protected AuthenticatedApiTestBase(WebApplicationFactory<Program> factory)
    {
        ConfigureProcessEnvironment();

        /*
         * Do not use the raw injected WebApplicationFactory directly.
         * Create an isolated derived factory with explicit test configuration.
         *
         * This prevents the common poisoned-factory problem where a failed startup
         * leaves the shared IServiceProvider disposed and all later tests cascade
         * into ObjectDisposedException.
         */
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(BuildIntegrationSettings());
            });

            builder.ConfigureTestServices(services =>
            {
                /*
                 * Integration tests validate HTTP/API behavior. Background workers
                 * are disabled here to avoid race conditions and disposed-provider
                 * cascades during WebApplicationFactory startup.
                 */
                services.RemoveAll<IHostedService>();
            });
        });
    }

    protected HttpClient CreateAnonymousClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateAnonymousClient();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            userName = TestAdminUserName,
            password = TestAdminPassword
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

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static void ConfigureProcessEnvironment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development", EnvironmentVariableTarget.Process);

        var connectionString = ResolveRequiredLocalConnectionString();

        Environment.SetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING", connectionString, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("PLANTPROCESS_TEST_CONNECTION_STRING", connectionString, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("ConnectionStrings__PlantProcessDb", connectionString, EnvironmentVariableTarget.Process);

        Environment.SetEnvironmentVariable("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173,http://localhost:3000", EnvironmentVariableTarget.Process);
    }

    private static Dictionary<string, string?> BuildIntegrationSettings()
    {
        var connectionString = ResolveRequiredLocalConnectionString();

        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:PlantProcessDb"] = connectionString,

            ["PlantProcess:RequireDatabaseConnectionString"] = "true",
            ["PlantProcess:PlantTimeZoneId"] = "Europe/Berlin",
            ["PlantProcess:PlantUtcOffsetMinutes"] = "60",

            ["PlantProcess:Workers:EnableImportQueueProcessorJob"] = "false",
            ["PlantProcess:Workers:EnableDataQualityScanJob"] = "false",
            ["PlantProcess:Workers:EnableRiskScoringJob"] = "false",

            ["PlantProcess:Auth:SigningKey"] = "SuperSecretTestKeyThatIsAtLeast32Bytes!!",
            ["PlantProcess:Auth:Issuer"] = "PlantProcessIQ",
            ["PlantProcess:Auth:Audience"] = "PlantProcessIQ.Client",
            ["PlantProcess:Auth:AccessTokenMinutes"] = "60",

            ["PlantProcess:Auth:BootstrapAdminUser"] = "__bootstrap_disabled_for_integration_tests__",
            ["PlantProcess:Auth:BootstrapAdminPassword"] = "__DISABLED__",

            ["PlantProcess:Auth:Users:0:UserName"] = TestAdminUserName,
            ["PlantProcess:Auth:Users:0:Password"] = TestAdminPassword,
            ["PlantProcess:Auth:Users:0:Role"] = "Admin",
            ["PlantProcess:Auth:Users:0:DisplayName"] = "Integration Test Admin",
            ["PlantProcess:Auth:Users:0:IsBootstrapAdmin"] = "false",
            ["PlantProcess:Auth:Users:0:ForcePasswordChangeOnFirstLogin"] = "false",

            ["AllowedHosts"] = "*",
            ["PLANTPROCESS_ALLOWED_ORIGINS"] = "http://localhost:5173,http://localhost:3000"
        };
    }

    private static string ResolveRequiredLocalConnectionString()
    {
        var value =
            Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("PLANTPROCESS_TEST_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb");

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "PPIQ_TEST_CONNECTION_STRING is required for API integration tests. " +
                "Set it in the same PowerShell session before running dotnet test. " +
                "This is intentional so no local PostgreSQL password is committed to source.");
        }

        var normalized = value.Trim().Trim('"')
            .Replace("Host=postgres", "Host=127.0.0.1", StringComparison.OrdinalIgnoreCase)
            .Replace("Server=postgres", "Server=127.0.0.1", StringComparison.OrdinalIgnoreCase)
            .Replace("Data Source=postgres", "Data Source=127.0.0.1", StringComparison.OrdinalIgnoreCase);

        if (!normalized.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Integration test connection string must contain Host/Server/Data Source.");
        }

        if (!normalized.Contains("Database=", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Integration test connection string must contain Database/Initial Catalog.");
        }

        if (!normalized.Contains("Username=", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("User ID=", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("User Id=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Integration test connection string must contain Username/User ID.");
        }

        if (!normalized.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Integration test connection string must contain Password.");
        }

        if (normalized.Contains("plantprocess_app_db", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Username=postgres", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("User ID=postgres", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("User Id=postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests are still pointing to the Docker/server-style database. " +
                "Use Karim's local laptop DB: database plantprocessiq, user plantprocess.");
        }

        return normalized;
    }
}
