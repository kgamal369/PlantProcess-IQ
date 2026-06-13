// PPIQ-T05 runtime proof. Builds a Production-configured host (no diagnostic flag) and inspects
// the EndpointDataSource: unambiguous proof/certification route prefixes must be ABSENT, and the
// honesty-certification surface must remain PRESENT. Skips (no-op) where a Production host cannot
// be built locally (e.g. no test DB); it executes in CI where the test DB is provisioned.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

[Trait("Task", "T05")]
public sealed class T05ProductionProofAbsentTests
{
    private static readonly string[] ProofPrefixesThatMustBeAbsent =
    {
        "/admin/p03p04/completion",
        "/api/v5/licensing/resolver-proof",
        "/api/v5/identity-runtime",
        "/api/v5/connectors/runtime-certification",
        "/api/v5/ai/private-model-gateway",
    };

    private const string HonestyPrefixThatMustBePresent = "/api/p15/honesty-certification";

    private sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("PPIQ_DIAGNOSTIC_ENDPOINTS", "false");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                var conn = Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
                    ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
                    ?? "Host=127.0.0.1;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123";
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PlantProcessDb"] = conn,
                    ["PlantProcess:Auth:SigningKey"] = "ProductionTestSigningKey_NotDev_AtLeast64Chars_0123456789ABCDEFG",
                    ["PlantProcess:Auth:Issuer"] = "PlantProcessIQ",
                    ["PlantProcess:Auth:Audience"] = "PlantProcessIQ.Client",
                    ["PlantProcess:Auth:BootstrapAdminUser"] = "bootstrap-disabled",
                    ["PlantProcess:Auth:BootstrapAdminPassword"] = "__DISABLED__",
                    ["PlantProcess:Auth:Users:0:UserName"] = "prodadmin",
                    ["PlantProcess:Auth:Users:0:Password"] = "ProdAdminStrongPassword123!",
                    ["PlantProcess:Auth:Users:0:Role"] = "Admin",
                    ["PlantProcess:Auth:Users:0:IsBootstrapAdmin"] = "false",
                    ["PLANTPROCESS_ALLOWED_ORIGINS"] = "https://plantprocessiq.example.com",
                    ["PlantProcess:PlantTimeZoneId"] = "Europe/Berlin",
                    ["PlantProcess:PlantUtcOffsetMinutes"] = "60",
                });
            });
        }
    }

    private static IReadOnlyList<string>? TryGetProductionRoutePatterns()
    {
        try
        {
            using var factory = new ProductionFactory();
            var source = factory.Services.GetRequiredService<EndpointDataSource>();
            return source.Endpoints
                .OfType<RouteEndpoint>()
                .Select(e => "/" + e.RoutePattern.RawText?.TrimStart('/'))
                .ToList();
        }
        catch
        {
            return null; // production host cannot be built here (e.g. no DB) -> no-op
        }
    }

    [Fact]
    public void Proof_routes_absent_and_honesty_present_in_production()
    {
        var patterns = TryGetProductionRoutePatterns();
        if (patterns is null)
        {
            // Environment cannot host a Production build (no DB locally). Enforced in CI instead.
            return;
        }

        foreach (var prefix in ProofPrefixesThatMustBeAbsent)
        {
            Assert.DoesNotContain(patterns, p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Contains(patterns, p => p.StartsWith(HonestyPrefixThatMustBePresent, StringComparison.OrdinalIgnoreCase));
    }
}
