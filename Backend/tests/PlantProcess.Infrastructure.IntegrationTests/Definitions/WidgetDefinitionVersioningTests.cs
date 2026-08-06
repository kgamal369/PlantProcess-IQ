using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Definitions;
using PlantProcess.Domain.Entities.Dashboarding;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Persistence;
using System.Text.Json;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-039. THE FROZEN VALIDATION.
///
///   create a widget definition through IDefinitionService
///   read it back BY VERSION
///   update it
///   confirm TWO IMMUTABLE VERSIONS EXIST
///
/// AND NOT ONE TABLE NAME APPEARS IN IT. That is the task's stated acceptance
/// criterion, not a stylistic preference: this file has to still pass unchanged
/// after M2a replaces the storage underneath the service. Every read and write
/// below goes through the contract or through a mapped entity.
///
/// SELF-CONTAINED BY CONSTRUCTION. It creates its own dashboard with a unique
/// code, creates its own widget under it, and removes both in a finally block
/// whatever happens. It reads nothing that existed before it ran, so it cannot
/// pass because some earlier local state happened to be there, and it leaves
/// nothing behind on a developer database.
/// </summary>
public sealed class WidgetDefinitionVersioningTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("PPIQ_TEST_PG_CONNSTRING")
        ?? Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
        ?? "Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only";

    /// <summary>
    /// BUILT THE WAY PRODUCTION BUILDS IT. AddInfrastructure configures the
    /// context with UseNpgsql AND UseSnakeCaseNamingConvention; a test that
    /// supplies only the first gets a context that emits PascalCase column
    /// names and fails on the first insert with "column Id does not exist".
    /// The convention is part of the mapping, not a detail of hosting.
    /// </summary>
    private static PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    private static string PayloadFor(Guid dashboardId, string code, string title, string? expression)
    {
        var payload = new WidgetDefinitionPayload(
            dashboardId,
            code,
            title,
            "chart",
            "bar",
            "dim_probe",
            "mea_probe",
            null,
            "{}",
            "{}",
            "{\"roleBinding\":{\"category\":\"group_code\",\"value\":\"measured_value\",\"secondary\":null}}",
            0,
            expression);

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    [SkippableFact]
    public async Task Create_read_by_version_update_leaves_two_immutable_versions()
    {
        await using var db = NewContext();
        Skip.IfNot(
            await db.Database.CanConnectAsync(),
            "No database reachable. Set PPIQ_TEST_PG_CONNSTRING to run this integration test.");

        var service = new DefinitionService(db);
        var probe = "t039_" + Guid.NewGuid().ToString("N")[..12];
        var dashboard = new DashboardDefinition(probe, "T-039 probe dashboard", false);

        db.DashboardDefinitions.Add(dashboard);
        await db.SaveChangesAsync();

        Guid widgetId = Guid.Empty;
        try
        {
            // CREATE. Version 1 is written in the same transaction as the widget.
            var created = await service.CreateAsync(
                DefinitionKind.Widget,
                PayloadFor(dashboard.Id, probe, "First title", "dimension group_code"),
                CancellationToken.None);

            Assert.True(created.IsSuccess, created.Error?.Message);
            Assert.Equal(1, created.Value!.VersionNumber);
            widgetId = created.Value.DefinitionId;

            // READ IT BACK BY VERSION.
            var v1 = await service.GetVersionAsync(
                DefinitionKind.Widget, widgetId, 1, CancellationToken.None);
            Assert.True(v1.IsSuccess, v1.Error?.Message);
            Assert.Contains("First title", v1.Value!.PayloadJson);

            // UPDATE.
            var updated = await service.UpdateAsync(
                DefinitionKind.Widget,
                widgetId,
                PayloadFor(dashboard.Id, probe, "Second title", "dimension shift_code"),
                CancellationToken.None);

            Assert.True(updated.IsSuccess, updated.Error?.Message);
            Assert.Equal(2, updated.Value!.VersionNumber);

            // TWO VERSIONS EXIST, and they are REAL rows rather than one row
            // reported twice: the list has both numbers and the payloads differ.
            var versions = await service.ListVersionsAsync(
                DefinitionKind.Widget, widgetId, CancellationToken.None);
            Assert.True(versions.IsSuccess, versions.Error?.Message);
            Assert.Equal(new[] { 1, 2 }, versions.Value!.Select(v => v.VersionNumber).ToArray());

            // VERSION 1 IS IMMUTABLE. The update did not rewrite it.
            var v1Again = await service.GetVersionAsync(
                DefinitionKind.Widget, widgetId, 1, CancellationToken.None);
            Assert.True(v1Again.IsSuccess, v1Again.Error?.Message);
            Assert.Contains("First title", v1Again.Value!.PayloadJson);
            Assert.DoesNotContain("Second title", v1Again.Value.PayloadJson);

            var v2 = await service.GetVersionAsync(
                DefinitionKind.Widget, widgetId, 2, CancellationToken.None);
            Assert.True(v2.IsSuccess, v2.Error?.Message);
            Assert.Contains("Second title", v2.Value!.PayloadJson);

            // The CURRENT definition agrees with the newest version.
            var current = await service.GetCurrentAsync(
                DefinitionKind.Widget, widgetId, CancellationToken.None);
            Assert.True(current.IsSuccess, current.Error?.Message);
            Assert.Equal(2, current.Value!.VersionNumber);
            Assert.Contains("Second title", current.Value.PayloadJson);

            // A version nobody wrote is refused, not invented.
            var missing = await service.GetVersionAsync(
                DefinitionKind.Widget, widgetId, 3, CancellationToken.None);
            Assert.True(missing.IsFailure);
        }
        finally
        {
            await CleanUpAsync(widgetId, dashboard.Id);
        }
    }

    [SkippableFact]
    public async Task A_kind_with_no_version_adapter_is_refused_rather_than_answered()
    {
        await using var db = NewContext();
        Skip.IfNot(
            await db.Database.CanConnectAsync(),
            "No database reachable. Set PPIQ_TEST_PG_CONNSTRING to run this integration test.");

        var service = new DefinitionService(db);

        // M1 versions the widget kind only. Every other kind must refuse, because
        // a synthesised version one would read as a fact and would not be one.
        var listed = await service.ListVersionsAsync(
            DefinitionKind.Transformation, Guid.NewGuid(), CancellationToken.None);

        Assert.True(listed.IsFailure);
        Assert.Contains("widget kind only", listed.Error!.Message);
    }

    private static async Task CleanUpAsync(Guid widgetId, Guid dashboardId)
    {
        await using var db = NewContext();

        if (widgetId != Guid.Empty)
        {
            var versions = await db.DefinitionVersions
                .Where(v => v.DefinitionId == widgetId)
                .ToListAsync();
            db.DefinitionVersions.RemoveRange(versions);
        }

        var dashboard = await db.DashboardDefinitions
            .FirstOrDefaultAsync(d => d.Id == dashboardId);
        if (dashboard is not null)
        {
            // The widget is removed with it: the mapping cascades on delete.
            db.DashboardDefinitions.Remove(dashboard);
        }

        await db.SaveChangesAsync();
    }
}