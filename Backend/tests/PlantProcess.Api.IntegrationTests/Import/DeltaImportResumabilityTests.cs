using System.Net.Http.Json;
using FluentAssertions;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Import;

public sealed class DeltaImportResumabilityTests : AuthenticatedApiTestBase
{
    public DeltaImportResumabilityTests(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task TwoStageDeltaImport_ReadinessEndpoint_IsReachable_WhenOptInEnabled()
    {
        if (!IsEnabled())
            return;

        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/two-stage-import/readiness");

        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TwoStageDeltaImport_FullCycle_IsIdempotent_WhenOptInEnabled()
    {
        if (!IsEnabled())
            return;

        using var client = await CreateAuthenticatedClientAsync();

        var first = await client.PostAsJsonAsync("/admin/two-stage-import/run-full-cycle", new
        {
            sourceSystemCode = "FlatSteelGoldenDemo",
            maxRows = 250,
            simulateCrashAfterRows = (int?)null
        });

        first.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
        first.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Forbidden);

        var second = await client.PostAsJsonAsync("/admin/two-stage-import/run-full-cycle", new
        {
            sourceSystemCode = "FlatSteelGoldenDemo",
            maxRows = 250,
            simulateCrashAfterRows = (int?)null
        });

        second.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
        second.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Forbidden);
    }

    private static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("PPIQ_RUN_CONNECTOR_INTEGRATION"),
            "1",
            StringComparison.Ordinal);
    }
}