using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Queries;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Analytics;

/// <summary>
/// PPIQ T-044. downtimeMinutes MUST EXECUTE, AND MUST MEAN StoppedMinutes.
///
/// downtimeMinutes was registered in DashboardMetadataCodes.Measures, published
/// by the metadata endpoint, offered in the authoring panel, listed in
/// ExecutableMeasures and fully implemented - and it threw HTTP 500 on EVERY
/// call, because
///
///     materialIds.Contains(downtime.MaterialUnitId.GetValueOrDefault())
///
/// cannot be translated to SQL by Npgsql. Nothing caught it because no widget
/// on any of the seven seeded dashboards bound the measure, so it was never
/// once executed. It was found only when T-044 proposed binding a widget to it.
///
/// THIS TEST RUNS AGAINST REAL POSTGRESQL ON PURPOSE. An in-memory or SQLite
/// provider would translate the broken expression happily and prove nothing:
/// the defect IS the Npgsql translation. A proof that cannot fail the way the
/// product failed is not a proof.
///
/// A SECOND DEFECT, found by the first run of this very test. The measure did
/// not sum StoppedMinutes and did not sum ProductionImpactMinutes. It computed
/// EndedAtUtc minus StartedAtUtc - a wall-clock quantity the plant never
/// recorded - and returned 0 for any event with no end timestamp. Both governed
/// decimal columns were discarded.
///
/// THE CONTRACT IS NOW FROZEN: downtimeMinutes = recorded StoppedMinutes.
/// ProductionImpactMinutes is a different question and must have its own named
/// measure if it is ever wanted; the entity's own comment explains why the two
/// cannot be conflated - a three-minute trip can cost six hours of production.
///
/// THE FIXTURE MAKES ALL THREE QUANTITIES DIFFERENT ON PURPOSE, so this test
/// cannot pass by accident on the wrong one:
///     StoppedMinutes total          51   <- the contract
///     wall-clock total              50   <- the old behaviour
///     ProductionImpactMinutes total 103  <- a different measure entirely
///
/// SELF-CONTAINED. It creates its own site, area, equipment, material and three
/// downtime events under unique codes, and removes all of them in a finally
/// block whatever happens. It asserts only on ITS OWN equipment group, so it
/// cannot pass or fail because of whatever else is in the database.
/// </summary>
public sealed class DowntimeMinutesMeasureExecutionTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("PPIQ_TEST_PG_CONNSTRING")
        ?? Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
        ?? "Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only";

    private static PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    [SkippableFact]
    public async Task Downtime_minutes_by_equipment_executes_and_sums_recorded_stopped_minutes()
    {
        await using var db = NewContext();

        var probe = "T044DT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        var site = new Site(probe + "-SITE", probe + " site", true);
        var area = new Area(site.Id, probe + "-AREA", probe + " area", true);
        var equipment = new Equipment(site.Id, probe + "-EQ", probe + " caster", "caster", true, area.Id);
        var material = new MaterialUnit(probe + "-MAT", "Coil", site.Id, "flat", "DP600", true);

        // The cases that matter. The engine's material rule is "material is null
        // OR material is in the filtered set", so all three must be counted.
        // Wall clock, stopped and impact are deliberately different on every row.

        // 30 wall-clock minutes, 17 stopped, 41 impact.
        var withMaterial = new DowntimeEvent(
            new DateTime(2026, 5, 1, 6, 0, 0, DateTimeKind.Utc),
            "unplanned",
            17m,
            41m,
            true,
            new DateTime(2026, 5, 1, 6, 30, 0, DateTimeKind.Utc),
            material.Id,
            null,
            equipment.Id);

        // 20 wall-clock minutes, 23 stopped, 59 impact, and NO material.
        var withoutMaterial = new DowntimeEvent(
            new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            "planned",
            23m,
            59m,
            true,
            new DateTime(2026, 5, 1, 8, 20, 0, DateTimeKind.Utc),
            null,
            null,
            equipment.Id);

        // NO END TIMESTAMP. The old implementation returned 0 for this row and
        // threw away a recorded 11 stopped minutes. A stoppage that has not been
        // closed out is not a stoppage of zero.
        var stillOpen = new DowntimeEvent(
            new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            "unplanned",
            11m,
            3m,
            true,
            null,
            null,
            null,
            equipment.Id);

        // Computed here, not read back from the thing under test.
        const decimal expectedStoppedMinutes = 51m;   // 17 + 23 + 11
        const decimal wallClockMinutes = 50m;         // 30 + 20 + 0, the old behaviour
        const decimal productionImpactMinutes = 103m; // 41 + 59 + 3, a different measure

        try
        {
            db.Sites.Add(site);
            db.Areas.Add(area);
            db.Equipment.Add(equipment);
            db.MaterialUnits.Add(material);
            await db.SaveChangesAsync();

            db.DowntimeEvents.Add(withMaterial);
            db.DowntimeEvents.Add(withoutMaterial);
            db.DowntimeEvents.Add(stillOpen);
            await db.SaveChangesAsync();

            var service = new DashboardWidgetQueryService(db, new DashboardWidgetValidationService());

            var result = await service.ExecuteAsync(
                new DashboardWidgetQueryDto(
                    DashboardMetadataCodes.WidgetTypes.Chart,
                    DashboardMetadataCodes.ChartTypes.Bar,
                    DashboardMetadataCodes.Dimensions.Equipment,
                    DashboardMetadataCodes.Measures.DowntimeMinutes,
                    null,
                    null,
                    null),
                CancellationToken.None);

            // If the LINQ cannot be translated, ExecuteAsync throws before this
            // line and the test fails with the translation exception itself,
            // which is exactly the failure the product had.
            Assert.True(result.IsSuccess, "downtimeMinutes did not execute successfully");

            var rows = result.Value!.Rows;

            var mine = rows.FirstOrDefault(r =>
                r.TryGetValue(DashboardMetadataCodes.Dimensions.Equipment, out var key) &&
                key is not null &&
                string.Equals(key.ToString(), equipment.Id.ToString(), StringComparison.OrdinalIgnoreCase));

            Assert.True(mine is not null, "the probe equipment did not appear as its own group");

            Assert.True(
                mine!.TryGetValue("value", out var value) && value is not null,
                "the probe group carried no value");

            var actual = Convert.ToDecimal(value);

            Assert.True(
                actual != wallClockMinutes,
                "downtimeMinutes returned the wall-clock duration, not the recorded StoppedMinutes");
            Assert.True(
                actual != productionImpactMinutes,
                "downtimeMinutes returned ProductionImpactMinutes, which is a different measure");
            Assert.Equal(expectedStoppedMinutes, actual);

            // The label must be the equipment NAME, not its id. Identity and
            // display are separate concepts and the row carries both.
            Assert.True(mine.TryGetValue("dimensionLabel", out var label) && label is not null);
            Assert.Equal(equipment.EquipmentName, label!.ToString());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM downtime_events WHERE equipment_id = {0};", equipment.Id);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM material_units WHERE id = {0};", material.Id);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM equipment WHERE id = {0};", equipment.Id);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM areas WHERE id = {0};", area.Id);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM sites WHERE id = {0};", site.Id);
        }
    }
}