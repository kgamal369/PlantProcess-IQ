using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Security;

public sealed class AuthGateMatrixTests : AuthenticatedApiTestBase
{
    public AuthGateMatrixTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [SkippableFact]
    public async Task Login_gate_should_reject_empty_payload()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { });

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Login_gate_should_reject_wrong_password()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = TestAdminUserName,
            Password = "wrong-password"
        });

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Login_gate_should_issue_admin_token_with_expected_claim_surface()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = TestAdminUserName,
            Password = TestAdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("role").GetString()
            .Should()
            .Be("Admin");

        json.GetProperty("accessToken").GetString()
            .Should()
            .NotBeNullOrWhiteSpace();

        json.GetProperty("scopes").EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Contain("source.configure");
    }

    [SkippableFact]
    public async Task Protected_admin_endpoint_should_reject_anonymous_user()
    {
        using var client = CreateAnonymousClient();

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Protected_admin_endpoint_should_accept_admin_token()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Malformed_bearer_token_should_not_be_accepted()
    {
        using var client = CreateAnonymousClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
