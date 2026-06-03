using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

/// <summary>
/// P2-05 two-tenant isolation proof (SCAFFOLD). Requires a live API + two seeded tenants/users.
/// Skips until PPIQ_TEST_BASEURL and credentials for two tenants are provided, and the
/// SENSITIVE_ROUTES list + Authenticate() are wired to your auth. Each sensitive domain must
/// have at least one assertion that tenant A cannot read tenant B's data (403 or empty),
/// including identifier-guessing.
/// </summary>
public sealed class TenantIsolationMatrixTests
{
    private static string? BaseUrl => Environment.GetEnvironmentVariable("PPIQ_TEST_BASEURL");

    // TODO: one representative read path per sensitive domain (sources, mappings, dashboards,
    // materials, suggestions, reports, audit, assistant). Use a tenant-B resource id while
    // authenticated as tenant A.
    private static readonly (string Domain, string PathTemplate)[] SensitiveRoutes =
    {
        ("sources",      "/api/admin/source-systems"),
        ("mappings",     "/api/admin/mappings"),
        ("dashboards",   "/api/dashboards"),
        ("materials",    "/api/materials/{idB}"),
        ("suggestions",  "/api/analytics/suggestions"),
        ("reports",      "/api/reports"),
        ("audit",        "/api/admin/audit"),
        ("assistant",    "/api/assistant/ask"),
    };

    // TODO: authenticate a user for the given tenant (login endpoint -> bearer token, or mint one).
    private static Task<HttpClient> AuthenticateAsync(string tenant) =>
        throw new NotImplementedException("Wire AuthenticateAsync to your login/token flow for tenant A and tenant B.");

    [SkippableFact]
    public async Task Cross_tenant_access_is_denied_or_empty_for_every_sensitive_domain()
    {
        Skip.If(string.IsNullOrWhiteSpace(BaseUrl), "Set PPIQ_TEST_BASEURL and wire AuthenticateAsync to run the isolation proof.");

        var clientA = await AuthenticateAsync("A");
        foreach (var (domain, template) in SensitiveRoutes)
        {
            var path = template.Replace("{idB}", "<a-tenant-B-resource-id>");
            var res = await clientA.GetAsync($"{BaseUrl}{path}");
            // Either an explicit deny, or a body that contains none of tenant B's rows.
            Assert.True(
                res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                    || (res.IsSuccessStatusCode && !(await res.Content.ReadAsStringAsync()).Contains("<tenant-B-marker>")),
                $"Cross-tenant leakage on domain '{domain}' ({path}).");
        }
    }
}
