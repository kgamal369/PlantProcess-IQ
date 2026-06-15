// PPIQ-T06: cross-tenant isolation. A token scoped to tenant A requesting tenant B's resource
// must get 403 or an empty set - never another tenant's rows. This test is env-gated because it
// needs two seeded tenants. Provide:
//   PPIQ_TEST_TENANT_A_TOKEN, PPIQ_TEST_TENANT_B_RESOURCE  (a URL returning tenant B data)
// When unset it no-ops; when set it executes the isolation assertion.
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

[Trait("Task", "T06")]
public sealed class T06CrossTenantIsolationTests : AuthenticatedApiTestBase
{
    public T06CrossTenantIsolationTests(WebApplicationFactory<Program> factory) : base(factory) { }

    [SkippableFact]
    public async Task TenantA_token_cannot_read_TenantB_resource()
    {
        var tenantAToken = Environment.GetEnvironmentVariable("PPIQ_TEST_TENANT_A_TOKEN");
        var tenantBResource = Environment.GetEnvironmentVariable("PPIQ_TEST_TENANT_B_RESOURCE");
        if (string.IsNullOrWhiteSpace(tenantAToken) || string.IsNullOrWhiteSpace(tenantBResource))
            return; // two-tenant seed not provided -> no-op (CI/demo wires PPIQ_TEST_TENANT_*)

        using var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantAToken);

        var resp = await client.GetAsync(tenantBResource);

        if (resp.StatusCode == HttpStatusCode.Forbidden || resp.StatusCode == HttpStatusCode.NotFound)
            return; // denied outright - correct

        resp.IsSuccessStatusCode.Should().BeTrue("a 2xx is only acceptable if the body is an empty set");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().MatchRegex(@"^\s*(\[\s*\]|\{\s*""(items|data|rows)""\s*:\s*\[\s*\]\s*\}|null)\s*$",
            "cross-tenant read must return 403 or an EMPTY set, never another tenant's rows. Body: " + body);
    }
}
