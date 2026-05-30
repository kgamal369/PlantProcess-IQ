using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Security;

public sealed class AuthEndpointTests : AuthenticatedApiTestBase
{
    public AuthEndpointTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_should_return_access_token_for_valid_admin_credentials()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = "admin",
            Password = TestAdminPassword
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var hasAccessToken =
            json.TryGetProperty("accessToken", out var accessToken)
            && !string.IsNullOrWhiteSpace(accessToken.GetString());

        var hasToken =
            json.TryGetProperty("token", out var token)
            && !string.IsNullOrWhiteSpace(token.GetString());

        (hasAccessToken || hasToken)
            .Should()
            .BeTrue("login must return a bearer token");
    }

    [Fact]
    public async Task Login_should_reject_invalid_credentials()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = "admin",
            Password = "wrong-password"
        });

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_should_reject_anonymous_request()
    {
        using var client = CreateAnonymousClient();

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}