using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Demo;

public sealed class Phase2DemoLifecycleContractTests : AuthenticatedApiTestBase
{
    public Phase2DemoLifecycleContractTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [SkippableFact]
    public async Task Demo_lifecycle_exposes_complete_customer_journey_steps()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/demo/lifecycle");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var json = document.RootElement.GetRawText();

        foreach (var expected in new[]
        {
            "LICENSE",
            "CONNECT",
            "STAGE",
            "MAP",
            "MONITOR",
            "DASHBOARD",
            "ML_READINESS",
            "REPORT"
        })
        {
            Assert.Contains(expected, json);
        }

        Assert.Contains("without changing the customer source schema", json);
        Assert.Contains("No trained production model is active", json);
    }

    [SkippableFact]
    public async Task Demo_reset_endpoint_returns_controlled_status_or_authorization_boundary()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/demo-lifecycle/reset", new
        {
            scope = "RealismDemo"
        });

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.Accepted, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests });

        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            Assert.Contains("jobId", body);
            Assert.Contains("statusUrl", body);
        }
    }
}