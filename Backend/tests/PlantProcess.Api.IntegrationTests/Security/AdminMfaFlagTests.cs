// PPIQ-T021 - admin MFA enforcement flag + step-up endpoint contract.
// Default posture (RequireAdminMfa=false): plain admin tokens reach /admin with NO
// MFA header and NO mfa claim - this is the production go-live setting.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

[Trait("Task", "T021")]
[Trait("Category", "Integration")]
public sealed class AdminMfaFlagTests : AuthenticatedApiTestBase
{
    public AdminMfaFlagTests(WebApplicationFactory<Program> factory) : base(factory) { }

    [Fact]
    public async Task With_flag_off_plain_admin_token_reaches_admin_without_mfa_header()
    {
        using var client = await CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Remove("X-PPIQ-MFA-Verified"); // prove the CLAIM path is not needed when off

        var response = await client.GetAsync("/admin/overview");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "PPIQ-T021: with RequireAdminMfa=false (default), admin MFA must not block the admin surface");
    }

    [Fact]
    public async Task Step_up_without_recent_verify_is_refused_403()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/auth/mfa/step-up", null);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "step-up must refuse a user with no recent successful mfa_verify audit event");

        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        body.TryGetProperty("error", out var error).Should().BeTrue(
            $"the 403 must come from StepUpAsync (error=mfa_step_up_refused), not an upstream layer. Raw body: {raw}");
        error.GetString().Should().Be("mfa_step_up_refused");
    }

    [Fact]
    public async Task Step_up_endpoint_requires_authentication()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsync("/auth/mfa/step-up", null);

        ((int)response.StatusCode).Should().BeOneOf(new[] { 401, 403 },
            "an anonymous caller must never reach step-up");
    }
}