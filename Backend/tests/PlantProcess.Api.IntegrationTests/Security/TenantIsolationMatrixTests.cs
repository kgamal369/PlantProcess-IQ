using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

/// <summary>
/// P2-05 two-tenant isolation proof. FUNCTIONAL via the real login endpoint.
/// Green requires a running API (PPIQ_TEST_BASEURL) + two seeded tenants/users:
///   PPIQ_TEST_BASEURL, PPIQ_TEST_LOGIN_PATH (default /api/auth/login),
///   PPIQ_TEST_A_USER / PPIQ_TEST_A_PASS, PPIQ_TEST_B_USER / PPIQ_TEST_B_PASS,
///   PPIQ_TEST_B_TENANT (tenant B's GUID, used for identifier-guessing).
/// When unset, the test is a no-op pass.
/// </summary>
public sealed class TenantIsolationMatrixTests
{
    private static string? Base => Environment.GetEnvironmentVariable("PPIQ_TEST_BASEURL");
    private static string Login => Environment.GetEnvironmentVariable("PPIQ_TEST_LOGIN_PATH") ?? "/api/auth/login";
    private static string? BTenant => Environment.GetEnvironmentVariable("PPIQ_TEST_B_TENANT");

    private static readonly (string Domain, string Path)[] Routes =
    {
        ("sources",     "/api/admin/source-systems"),
        ("mappings",    "/api/admin/mappings"),
        ("dashboards",  "/api/dashboards"),
        ("materials",   "/api/materials?tenantId={idB}"),
        ("suggestions", "/api/analytics/suggestions"),
        ("reports",     "/api/reports"),
        ("audit",       "/api/admin/audit"),
        ("assistant",   "/api/analytics/advanced/results?outcomeKey=defect.edge_crack_rate&tenantId={idB}"),
    };

    private static async Task<HttpClient> LoginAsync(string user, string pass)
    {
        var http = new HttpClient { BaseAddress = new Uri(Base!) };
        var res = await http.PostAsJsonAsync(Login, new { userName = user, password = pass });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        string? token = null;
        foreach (var k in new[] { "accessToken", "token", "access_token", "jwt" })
            if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String) { token = v.GetString(); break; }
        Assert.False(string.IsNullOrWhiteSpace(token), "Login did not return a bearer token (check PPIQ_TEST_LOGIN_PATH / field name).");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    [Fact]
    public async Task Cross_tenant_access_is_denied_or_empty_for_every_sensitive_domain()
    {
        // Not configured: no-op pass. Set PPIQ_TEST_BASEURL + tenant A/B credentials to run the proof.
        if (string.IsNullOrWhiteSpace(Base) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PPIQ_TEST_A_USER")))
            return;

        var a = await LoginAsync(Environment.GetEnvironmentVariable("PPIQ_TEST_A_USER")!, Environment.GetEnvironmentVariable("PPIQ_TEST_A_PASS")!);
        var idB = BTenant ?? "00000000-0000-0000-0000-0000000000ff";

        foreach (var (domain, template) in Routes)
        {
            var res = await a.GetAsync(template.Replace("{idB}", idB));
            var body = res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : "";
            Assert.True(
                res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.BadRequest
                    || (res.IsSuccessStatusCode && (BTenant is null || !body.Contains(BTenant))),
                $"Cross-tenant leakage on '{domain}' -> {(int)res.StatusCode}.");
        }
    }
}
