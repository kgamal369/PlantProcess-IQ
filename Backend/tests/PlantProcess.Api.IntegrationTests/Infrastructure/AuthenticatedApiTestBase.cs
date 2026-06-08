using System.Diagnostics;
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

    private static readonly object EnvironmentLock = new();
    private static bool _environmentConfigured;

    protected AuthenticatedApiTestBase(WebApplicationFactory<Program> factory)
    {
        ConfigureTestEnvironmentOnce();

        // Do not call WithWebHostBuilder here.
        // A cloned factory per test class can break the TestServer lifecycle.
        Factory = factory;
    }

    protected HttpClient CreateAnonymousClient()
    {
        // PPIQ_PLAIN_DOTNET_TEST_WEB_FACTORY_ONLY:
        // Plain dotnet test must use WebApplicationFactory by default.
        // External API host is opt-in only via PPIQ_FORCE_EXTERNAL_API_TEST_HOST=1.
        if (IsForcedExternalHost())
        {
            return ExternalApiTestHost.CreateClient();
        }

        return CreateFactoryClient();
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

    private HttpClient CreateFactoryClient()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.Timeout = TimeSpan.FromSeconds(120);
        return client;
    }

    private static bool IsForcedExternalHost()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("PPIQ_FORCE_EXTERNAL_API_TEST_HOST"),
            "1",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool UseInProcessFactoryHost()
    {
        return !IsForcedExternalHost()
            && string.Equals(
                Environment.GetEnvironmentVariable("PPIQ_USE_WEBAPPLICATION_FACTORY_TEST_HOST"),
                "1",
                StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsFactoryStartupFailure(Exception ex)
    {
        var text = ex.ToString();

        return text.Contains(
                "server has not been started",
                StringComparison.OrdinalIgnoreCase)
            || text.Contains(
                "no web application was configured",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFactoryLifecycleFailure(Exception ex)
    {
        var text = ex.ToString();

        return text.Contains(
                "Cannot access a disposed object",
                StringComparison.OrdinalIgnoreCase)
            || text.Contains(
                "Object name: 'IServiceProvider'",
                StringComparison.OrdinalIgnoreCase)
            || (
                text.Contains("IServiceProvider", StringComparison.OrdinalIgnoreCase)
                && text.Contains("disposed", StringComparison.OrdinalIgnoreCase)
            );
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

            Set("PPIQ_RUN_CONNECTOR_INTEGRATION", "0");

            Set(
                "ConnectionStrings__PlantProcessDb",
                ResolveIntegrationTestConnectionString());

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
            Set("PlantProcess__Auth__Users__0__IsBootstrapAdmin", "false");
            Set("PlantProcess__Auth__Users__0__ForcePasswordChangeOnFirstLogin", "false");

            Set("PlantProcess__PlantTimeZoneId", "Europe/Berlin");
            Set("PlantProcess__PlantUtcOffsetMinutes", "60");

            _environmentConfigured = true;
        }
    }

    private static string ResolveIntegrationTestConnectionString()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING"),
            Environment.GetEnvironmentVariable("PLANTPROCESS_TEST_CONNECTION_STRING"),
            Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb"),
            "Host=127.0.0.1;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123"
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim().Trim('"')
                    .Replace("Host=postgres", "Host=127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    .Replace("Server=postgres", "Server=127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    .Replace("Data Source=postgres", "Data Source=127.0.0.1", StringComparison.OrdinalIgnoreCase);
            }
        }

        throw new InvalidOperationException("No integration-test connection string could be resolved.");
    }

    private static void Set(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
    }

    private static class ExternalApiTestHost
    {
        private static readonly object Gate = new();

        private static Uri? _baseAddress;
        private static Process? _process;
    private static readonly string ExternalHostLogPath =
        Path.Combine(Path.GetTempPath(), "ppiq-api-integration-host.log");

        private static bool _cleanupRegistered;

        public static HttpClient CreateClient()
        {
            EnsureStarted();

            return new HttpClient
            {
                BaseAddress = _baseAddress!,
                Timeout = TimeSpan.FromSeconds(120)
            };
        }

        private static void EnsureStarted()
        {
            lock (Gate)
            {
                if (_baseAddress is not null && IsReachable(_baseAddress))
                {
                    return;
                }

                ConfigureTestEnvironmentOnce();

                var port = ResolvePort();
                var uri = new Uri($"http://127.0.0.1:{port}/");

                if (IsReachable(uri))
                {
                    _baseAddress = uri;
                    return;
                }

                StartApiProcess(uri);
                WaitUntilReachable(uri);

                _baseAddress = uri;
            }
        }

        private static int ResolvePort()
        {
            return int.TryParse(
                Environment.GetEnvironmentVariable("PPIQ_TEST_API_PORT"),
                out var port)
                ? port
                : 15063;
        }

        private static void StartApiProcess(Uri uri)
        {
            var backendRoot = ResolveBackendRoot();
            var apiProject = Path.Combine(
                backendRoot,
                "PlantProcess.Api",
                "PlantProcess.Api.csproj");

            if (!File.Exists(apiProject))
            {
                throw new FileNotFoundException("API project not found for external integration test host.", apiProject);
            }

            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = backendRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--no-launch-profile");
            psi.ArgumentList.Add("--no-build");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(apiProject);

            foreach (var pair in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
            {
                var key = pair.Key?.ToString();
                var value = pair.Value?.ToString();

                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    psi.Environment[key] = value;
                }
            }

            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["DOTNET_ENVIRONMENT"] = "Development";
            psi.Environment["ASPNETCORE_URLS"] = uri.ToString().TrimEnd('/');

            psi.Environment["ConnectionStrings__PlantProcessDb"] =
                ResolveIntegrationTestConnectionString();

            psi.Environment["PLANTPROCESS_ALLOWED_ORIGINS"] = "http://localhost:5173";

            psi.Environment["PlantProcess__Auth__SigningKey"] = "SuperSecretTestKeyThatIsAtLeast32Bytes!!";
            psi.Environment["PlantProcess__Auth__Issuer"] = "PlantProcessIQ";
            psi.Environment["PlantProcess__Auth__Audience"] = "PlantProcessIQ.Client";
            psi.Environment["PlantProcess__Auth__AccessTokenMinutes"] = "60";

            psi.Environment["PlantProcess__Auth__BootstrapAdminUser"] = "__bootstrap_disabled_for_integration_tests__";
            psi.Environment["PlantProcess__Auth__BootstrapAdminPassword"] = "BootstrapDisabledOnlyForTests123!";

            psi.Environment["PlantProcess__Auth__Users__0__UserName"] = TestAdminUserName;
            psi.Environment["PlantProcess__Auth__Users__0__Password"] = TestAdminPassword;
            psi.Environment["PlantProcess__Auth__Users__0__Role"] = "Admin";
            psi.Environment["PlantProcess__Auth__Users__0__DisplayName"] = "Integration Test Admin";
            psi.Environment["PlantProcess__Auth__Users__0__IsBootstrapAdmin"] = "false";
            psi.Environment["PlantProcess__Auth__Users__0__ForcePasswordChangeOnFirstLogin"] = "false";

            psi.Environment["PlantProcess__PlantTimeZoneId"] = "Europe/Berlin";
            psi.Environment["PlantProcess__PlantUtcOffsetMinutes"] = "60";

            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start external API integration test host.");

            try
            {
                File.AppendAllText(
                    ExternalHostLogPath,
                    $"---- PPIQ external API test host started {DateTimeOffset.UtcNow:o} ----{Environment.NewLine}");

                _process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is not null)
                    {
                        File.AppendAllText(ExternalHostLogPath, "[OUT] " + e.Data + Environment.NewLine);
                    }
                };

                _process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null)
                    {
                        File.AppendAllText(ExternalHostLogPath, "[ERR] " + e.Data + Environment.NewLine);
                    }
                };

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch
            {
                // Diagnostic logging must never break the test host.
            }

            if (!_cleanupRegistered)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    try
                    {
                        if (_process is { HasExited: false })
                        {
                            _process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Best-effort cleanup only.
                    }
                };

                _cleanupRegistered = true;
            }
        }

        private static void WaitUntilReachable(Uri uri)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);

            while (DateTime.UtcNow < deadline)
            {
                if (_process is { HasExited: true })
                {
                    throw new InvalidOperationException(
                        $"External API integration test host exited early. ExitCode={_process.ExitCode}. Log={ExternalHostLogPath}");
                }

                if (IsReachable(uri))
                {
                    return;
                }

                Thread.Sleep(500);
            }

            throw new TimeoutException(
                $"External API integration test host did not become reachable at {uri} within 60 seconds.");
        }

        private static bool IsReachable(Uri uri)
        {
            try
            {
                using var client = new HttpClient
                {
                    BaseAddress = uri,
                    Timeout = TimeSpan.FromSeconds(2)
                };

                using var response = client.GetAsync("/health").GetAwaiter().GetResult();

                // Any HTTP response means the ASP.NET app is running.
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveBackendRoot()
        {
            var repoRoot = Environment.GetEnvironmentVariable("PPIQ_REPO_ROOT");

            if (!string.IsNullOrWhiteSpace(repoRoot))
            {
                return Path.Combine(repoRoot, "Backend");
            }

            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }
    }
}