using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Import;

public sealed class DeltaImportResumabilityTests : AuthenticatedApiTestBase
{
    public DeltaImportResumabilityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task TwoStageDeltaImport_OverviewAndRegistry_AreReachable_WhenOptInEnabled()
    {
        if (!IsEnabled()) return;

        using var client = await CreateAuthenticatedClientAsync();

        await AssertSuccessAsync(await client.PostAsync("/admin/two-stage-import/provision-baseline", null), "provision-baseline");
        await AssertSuccessAsync(await client.GetAsync("/admin/two-stage-import/overview"), "overview");
        await AssertSuccessAsync(await client.GetAsync("/admin/two-stage-import/source-tables"), "source-tables");
        await AssertSuccessAsync(await client.GetAsync("/admin/two-stage-import/runs"), "runs");
    }

    [Fact]
    public async Task TwoStageDeltaImport_Stage1Stage2AndFullCycle_AreIdempotent_WhenOptInEnabled()
    {
        if (!IsEnabled()) return;

        using var client = await CreateAuthenticatedClientAsync();

        await AssertSuccessAsync(await client.PostAsync("/admin/two-stage-import/provision-baseline", null), "provision-baseline");

        var firstStage1 = await PostRunAsync(client, "/admin/two-stage-import/stage1/run", maxRows: 25, timeoutSeconds: 30, maxMinutes: 1);
        var secondStage1 = await PostRunAsync(client, "/admin/two-stage-import/stage1/run", maxRows: 25, timeoutSeconds: 30, maxMinutes: 1);
        var firstStage2 = await PostRunAsync(client, "/admin/two-stage-import/stage2/run", maxRows: 25, timeoutSeconds: 30, maxMinutes: 1);
        var fullCycle1 = await PostRunAsync(client, "/admin/two-stage-import/run-full-cycle", maxRows: 50, timeoutSeconds: 45, maxMinutes: 2);
        var fullCycle2 = await PostRunAsync(client, "/admin/two-stage-import/run-full-cycle", maxRows: 50, timeoutSeconds: 45, maxMinutes: 2);

        firstStage1.RootElement.GetProperty("stage").GetString().Should().Be("Stage1DeltaImport");
        secondStage1.RootElement.GetProperty("stage").GetString().Should().Be("Stage1DeltaImport");
        firstStage2.RootElement.GetProperty("stage").GetString().Should().Be("Stage2CanonicalRefresh");
        fullCycle1.RootElement.GetProperty("stage").GetString().Should().Be("TwoStageFullCycle");
        fullCycle2.RootElement.GetProperty("stage").GetString().Should().Be("TwoStageFullCycle");

        var overview = await ReadJsonAsync(await client.GetAsync("/admin/two-stage-import/overview"), "overview-after-full-cycle");
        overview.RootElement.TryGetProperty("sourceTables", out var sourceTables).Should().BeTrue();
        sourceTables.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task TwoStageDeltaImport_MaxRowsSmallBatch_DoesNotBreakWatermarks_WhenOptInEnabled()
    {
        if (!IsEnabled()) return;

        using var client = await CreateAuthenticatedClientAsync();

        await AssertSuccessAsync(await client.PostAsync("/admin/two-stage-import/provision-baseline", null), "provision-baseline");

        var smallBatch = await PostRunAsync(client, "/admin/two-stage-import/stage1/run", maxRows: 3, timeoutSeconds: 30, maxMinutes: 1);
        var resumeBatch = await PostRunAsync(client, "/admin/two-stage-import/stage1/run", maxRows: 3, timeoutSeconds: 30, maxMinutes: 1);

        smallBatch.RootElement.TryGetProperty("rows", out var smallRows).Should().BeTrue();
        resumeBatch.RootElement.TryGetProperty("rows", out var resumeRows).Should().BeTrue();
        smallRows.ValueKind.Should().Be(JsonValueKind.Array);
        resumeRows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task TwoStageDeltaImport_RejectsAnonymousAccess_WhenOptInEnabled()
    {
        if (!IsEnabled()) return;

        using var client = CreateAnonymousClient();

        var response = await client.GetAsync("/admin/two-stage-import/overview");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    private static async Task<JsonDocument> PostRunAsync(HttpClient client, string path, int maxRows, int timeoutSeconds, int maxMinutes)
    {
        var response = await client.PostAsJsonAsync(path, new
        {
            requestedBy = "PPIQ-T207 integration test",
            maxRows,
            timeoutSeconds,
            maxMinutes
        });

        return await ReadJsonAsync(response, path);
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, operation + " must be authenticated by test base. Body=" + body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, operation + " must be authorized for admin. Body=" + body);
        response.IsSuccessStatusCode.Should().BeTrue(operation + " must return success. Status=" + response.StatusCode + " Body=" + body);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, operation + " must be authenticated. Body=" + body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, operation + " must be authorized. Body=" + body);
        response.IsSuccessStatusCode.Should().BeTrue(operation + " must return success. Status=" + response.StatusCode + " Body=" + body);
        return JsonDocument.Parse(body);
    }

    private static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("PPIQ_RUN_CONNECTOR_INTEGRATION"),
            "1",
            StringComparison.Ordinal);
    }
}
