// ============================================================================
// T-014 - Auth lifecycle: login -> protected access -> refresh (rotation) ->
//          logout (revoke) -> refresh-after-logout is rejected.
// Uses an in-process client WITH cookie handling so the refresh cookie is
// carried (the full-suite external host uses a cookie-less client).
// ============================================================================
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Auth;

[Trait("Category", "Integration")]
[Trait("Task", "T-014")]
public sealed class AuthLifecycleTests : AuthenticatedApiTestBase
{
    public AuthLifecycleTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_session_refresh_logout_then_refresh_is_rejected()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.Timeout = TimeSpan.FromSeconds(120);

        // 1) Login -> access token + refresh cookie.
        var login = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = TestAdminUserName,
            Password = TestAdminPassword
        });
        Assert.True(login.IsSuccessStatusCode, "login status " + (int)login.StatusCode);

        var accessToken = ReadToken(await login.Content.ReadFromJsonAsync<JsonElement>());
        Assert.False(string.IsNullOrWhiteSpace(accessToken), "login must return an access token");

        // 2) Access token authorizes a protected endpoint.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var session = await client.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // 3) Refresh via the cookie -> a new access token (rotation).
        var refresh = await client.PostAsync("/auth/refresh", null);
        Assert.True(refresh.IsSuccessStatusCode, "refresh status " + (int)refresh.StatusCode);
        Assert.False(
            string.IsNullOrWhiteSpace(ReadToken(await refresh.Content.ReadFromJsonAsync<JsonElement>())),
            "refresh must return a new access token");

        // 4) Logout revokes the current refresh token.
        var logout = await client.PostAsync("/auth/logout", null);
        Assert.True((int)logout.StatusCode < 400, "logout status " + (int)logout.StatusCode);

        // 5) Refresh after logout must be rejected (token revoked).
        var afterLogout = await client.PostAsync("/auth/refresh", null);
        Assert.False(afterLogout.IsSuccessStatusCode,
            "refresh after logout must be rejected, got " + (int)afterLogout.StatusCode);
    }

    private static string ReadToken(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty("accessToken", out var a)) return a.GetString();
            if (json.TryGetProperty("token", out var t)) return t.GetString();
        }
        return null;
    }
}