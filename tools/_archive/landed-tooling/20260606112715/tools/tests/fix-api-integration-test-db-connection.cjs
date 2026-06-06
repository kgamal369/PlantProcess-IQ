const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function write(file, text) {
  fs.mkdirSync(path.dirname(p(file)), { recursive: true });
  fs.writeFileSync(p(file), text.replace(/^\n/, ""), "utf8");
  console.log("Wrote " + file);
}

write("Backend/tests/PlantProcess.Api.IntegrationTests/Infrastructure/AuthenticatedApiTestBase.cs", `
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

            Set("ASPNETCORE_ENVIRONMENT", "Development");
            Set("DOTNET_ENVIRONMENT", "Development");

            var connectionString = ResolveIntegrationTestConnectionString();

            Set("ConnectionStrings__PlantProcessDb", connectionString);

            Set("PLANTPROCESS_ALLOWED_ORIGINS", "http://localhost:5173,http://localhost:3000");

            Set("PlantProcess__RequireDatabaseConnectionString", "true");
            Set("PlantProcess__PlantTimeZoneId", "Europe/Berlin");
            Set("PlantProcess__PlantUtcOffsetMinutes", "60");

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
            Set("PlantProcess__Auth__Users__0__IsBootstrapAdmin", "false");
            Set("PlantProcess__Auth__Users__0__ForcePasswordChangeOnFirstLogin", "false");

            _environmentConfigured = true;
        }
    }

    private static string ResolveIntegrationTestConnectionString()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING"),
            Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb"),
            Environment.GetEnvironmentVariable("PLANTPROCESS_TEST_CONNECTION_STRING"),
            ReadConnectionStringFromAppSettings(),
            ReadConnectionStringFromEnvFile("..\\\\..\\\\..\\\\..\\\\Infrastructure\\\\deploy\\\\.env"),
            ReadConnectionStringFromEnvFile("..\\\\..\\\\..\\\\..\\\\Infrastructure\\\\deploy\\\\.env.production"),
            ReadConnectionStringFromEnvFile("..\\\\..\\\\..\\\\..\\\\Backend\\\\.env"),
        };

        foreach (var candidate in candidates)
        {
            if (IsUsableConnectionString(candidate))
            {
                return candidate!;
            }
        }

        throw new InvalidOperationException(
            "API integration tests need a valid PostgreSQL connection string. " +
            "Set PPIQ_TEST_CONNECTION_STRING before running dotnet test, for example: " +
            "$env:PPIQ_TEST_CONNECTION_STRING='Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=<your-local-password>'");
    }

    private static string? ReadConnectionStringFromAppSettings()
    {
        var path = FindFileUpwards(
            AppContext.BaseDirectory,
            Path.Combine("PlantProcess.Api", "appsettings.Development.json"));

        if (path is null)
        {
            path = FindFileUpwards(
                AppContext.BaseDirectory,
                Path.Combine("Backend", "PlantProcess.Api", "appsettings.Development.json"));
        }

        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
            {
                return null;
            }

            if (!connectionStrings.TryGetProperty("PlantProcessDb", out var plantProcessDb))
            {
                return null;
            }

            return plantProcessDb.GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadConnectionStringFromEnvFile(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

        if (!File.Exists(fullPath))
        {
            return null;
        }

        foreach (var rawLine in File.ReadAllLines(fullPath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            if (string.Equals(key, "ConnectionStrings__PlantProcessDb", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? FindFileUpwards(string startDirectory, string relativePath)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsUsableConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();

        if (!normalized.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!normalized.Contains("Database=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!normalized.Contains("Username=", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("User ID=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!normalized.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.Contains("SET_LOCAL_POSTGRES_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("SET_BY_USER_SECRETS_OR_ENV", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("<your-local-password>", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("[MASKED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static void Set(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
    }
}
`);

console.log("");
console.log("Integration test DB connection resolver patched.");
console.log("");
console.log("Next:");
console.log("1) Set PPIQ_TEST_CONNECTION_STRING only if your appsettings.Development.json / .env does not contain the real local password.");
console.log("2) Run dotnet test again.");
