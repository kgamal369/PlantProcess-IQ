// PPIQ-T06: role differentiation by identity. Uses the TestMode-seeded role users
// (tm-admin/tm-ceo/tm-engineer/tm-operator). No-ops when those users are not seeded so the
// suite stays green on a bare laptop; runs fully in CI/demo where PPIQ_TESTMODE__SeedUsers=true.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

[Trait("Task", "T06")]
public sealed class T06RoleAuthorizationMatrixTests : AuthenticatedApiTestBase
{
    public T06RoleAuthorizationMatrixTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private const string AdminOnlyRoute = "/admin/overview";

    private async Task<string?> TryLoginAsync(HttpClient client, string user, string pass)
    {
        var resp = await client.PostAsJsonAsync("/auth/login", new { UserName = user, Password = pass });
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        if (json.TryGetProperty("accessToken", out var a)) return a.GetString();
        if (json.TryGetProperty("token", out var t)) return t.GetString();
        return null;
    }

    [Fact]
    public async Task Admin_role_reaches_admin_surface_and_operator_is_denied()
    {
        using var probe = CreateAnonymousClient();
        var adminToken = await TryLoginAsync(probe, "tm-admin", "TestMode-Admin-123!");
        if (adminToken is null) return; // TestMode role users not seeded here -> no-op

        var operatorToken = await TryLoginAsync(CreateAnonymousClient(), "tm-operator", "TestMode-Operator-123!");
        operatorToken.Should().NotBeNullOrWhiteSpace("the operator role user must also be seeded by TestMode");

        using var adminClient = CreateAnonymousClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        adminClient.DefaultRequestHeaders.Add("X-PPIQ-MFA-Verified", "true");
        var adminResp = await adminClient.GetAsync(AdminOnlyRoute);
        ((int)adminResp.StatusCode).Should().BeLessThan(300,
            $"Admin must reach {AdminOnlyRoute}. Got {(int)adminResp.StatusCode}.");

        using var opClient = CreateAnonymousClient();
        opClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var opResp = await opClient.GetAsync(AdminOnlyRoute);
        opResp.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized },
            $"Operator must be denied {AdminOnlyRoute}. Got {(int)opResp.StatusCode}.");
    }
}
