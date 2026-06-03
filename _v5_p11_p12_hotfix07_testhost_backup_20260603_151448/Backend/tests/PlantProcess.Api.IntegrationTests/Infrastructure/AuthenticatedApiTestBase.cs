using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantProcess.Api.IntegrationTests.Infrastructure;

public abstract class AuthenticatedApiTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected const string TestAdminUserName = "admin";
    protected const string TestAdminPassword = "ChangeMe123!";

    protected readonly WebApplicationFactory<Program> Factory;

    private static readonly object EnvironmentLock = new();
    private static bool _environmentConfigured;

    protected AuthenticatedApiTestBase(WebApplicationFactory<Program> factory)
    {
        ConfigureTestEnvironmentOnce();

        // Use a configured derived factory for every integration-test class.
        // This avoids the "server has not been started or no web application was configured"
        // cascade and makes the test host explicitly carry the same settings as local dev.
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.UseSetting(
                "ConnectionStrings:PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123");

            builder.UseSetting("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173");

            builder.UseSetting("PlantProcess:Auth:SigningKey", "SuperSecretTestKeyThatIsAtLeast32Bytes!!");
            builder.UseSetting("PlantProcess:Auth:Issuer", "PlantProcessIQ");
            builder.UseSetting("PlantProcess:Auth:Audience", "PlantProcessIQ.Client");
            builder.UseSetting("PlantProcess:Auth:AccessTokenMinutes", "60");

            builder.UseSetting("PlantProcess:Auth:BootstrapAdminUser", "__bootstrap_disabled_for_integration_tests__");
            builder.UseSetting("PlantProcess:Auth:BootstrapAdminPassword", "BootstrapDisabledOnlyForTests123!");

            builder.UseSetting("PlantProcess:Auth:Users:0:UserName", TestAdminUserName);
            builder.UseSetting("PlantProcess:Auth:Users:0:Password", TestAdminPassword);
            builder.UseSetting("PlantProcess:Auth:Users:0:Role", "Admin");
            builder.UseSetting("PlantProcess:Auth:Users:0:DisplayName", "Integration Test Admin");

            builder.UseSetting("PlantProcess:PlantTimeZoneId", "Europe/Berlin");
            builder.UseSetting("PlantProcess:PlantUtcOffsetMinutes", "60");
        });
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

            Set("ASPNETCORE_ENVIRONMENT", "Development");
            Set("DOTNET_ENVIRONMENT", "Development");

            Set(
                "ConnectionStrings__PlantProcessDb",
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123");

            Set("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173");

            Set("PlantProcess__Auth__SigningKey", "SuperSecretTestKeyThatIsAtLeast32Bytes!!");
            Set("PlantProcess__Auth__Issuer", "PlantProcessIQ");
            Set("PlantProcess__Auth__Audience", "PlantProcessIQ.Client");
            Set("PlantProcess__Auth__AccessTokenMinutes", "60");

            Set("PlantProcess__Auth__BootstrapAdminUser", "__bootstrap_disabled_for_integration_tests__");
            Set("PlantProcess__Auth__BootstrapAdminPassword", "BootstrapDisabledOnlyForTests123!");

            Set("PlantProcess__Auth__Users__0__UserName", TestAdminUserName);
            Set("PlantProcess__Auth__Users__0__Password", TestAdminPassword);
            Set("PlantProcess__Auth__Users__0__Role", "Admin");
            Set("PlantProcess__Auth__Users__0__DisplayName", "Integration Test Admin");

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