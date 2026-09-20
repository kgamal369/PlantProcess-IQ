using System.Text;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Jobs;

// Direct HTTP clients for the runner-owned production host. No service replacement.
public static class CanvasLaunchAcceptanceSupport
{
    public static HttpClient AnonymousClient() => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("PPIQ_CANVAS_ACCEPTANCE_URL")
            ?? throw new InvalidOperationException("Runner-owned host URL is required.")),
        Timeout = TimeSpan.FromMinutes(3)
    };

    public static async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = AnonymousClient();
        try
        {
            using var response = await client.PostAsJsonAsync("/auth/login", new
            {
                UserName = Environment.GetEnvironmentVariable("PPIQ_CANVAS_ACCEPTANCE_USER"),
                Password = Environment.GetEnvironmentVariable("PPIQ_CANVAS_ACCEPTANCE_PASSWORD")
            });
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            string? token = json.TryGetProperty("accessToken", out var access) ? access.GetString()
                : json.GetProperty("token").GetString();
            Assert.False(string.IsNullOrWhiteSpace(token));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-PPIQ-MFA-Verified", "true");
            Assert.Equal(Guid.Parse(Environment.GetEnvironmentVariable("PPIQ_CANVAS_ACCEPTANCE_TENANT")!), TenantOf(client));
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    public static string ConnectionString()
    {
        string? connection = Environment.GetEnvironmentVariable(
            PlantProcess.TestSupport.TestDatabaseTarget.IntegrationVariable);

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                "T-245 acceptance requires the runner-owned disposable database in "
                + PlantProcess.TestSupport.TestDatabaseTarget.IntegrationVariable + ".");
        }

        string? database = PlantProcess.TestSupport.TestDatabaseTarget.DatabaseNameOf(connection);
        if (string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("The disposable connection must name a database explicitly.");

        if (PlantProcess.TestSupport.TestDatabaseTarget.IsProtected(database))
        {
            throw new InvalidOperationException(
                "T-245 acceptance refuses to run against the protected database " + database + ".");
        }

        return connection!;
    }

    public static PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString())
            .UseSnakeCaseNamingConvention()
            .EnableDetailedErrors()
            .Options;

        return new PlantProcessDbContext(options);
    }

    /// <summary>The tenant the caller's own token carries. Nothing is assumed about it.</summary>
    public static Guid TenantOf(HttpClient authenticated)
    {
        string? token = authenticated.DefaultRequestHeaders.Authorization?.Parameter;
        Assert.False(string.IsNullOrWhiteSpace(token), "the acceptance client carries no bearer token");

        string[] parts = token!.Split('.');
        Assert.True(parts.Length >= 2, "the access token is not a JWT");

        string payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using JsonDocument claims = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

        Assert.True(claims.RootElement.TryGetProperty("tenant_id", out JsonElement tenant),
            "the access token carries no tenant_id claim, so no definition can be resolved for this caller");

        return Guid.Parse(tenant.GetString()!);
    }
}
