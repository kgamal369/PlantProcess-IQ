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
/// D1 LAYER A. THE GENERIC AGGREGATE ENGINE, AGAINST REAL POSTGRESQL.
///
/// Real Npgsql on purpose. The whole correction is about what the DATABASE
/// computes, so an in-memory provider would translate anything and prove
/// nothing. If a grouping expression cannot be translated, these tests fail the
/// way the product would fail.
///
/// SELF-CONTAINED and INDUSTRY-NEUTRAL. The fixture deliberately uses a
/// non-steel vocabulary - a filling line and a bottle format - so that nothing
/// here can quietly certify Fleet-v2 assumptions. The engine must care only
/// that a dimension is registered, never what the business calls it.
/// </summary>
public sealed class GenericAggregateEngineTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("PPIQ_TEST_PG_CONNSTRING")
        ?? "Host=127.0.0.1;Port=5432;Database=ppiq_presentation;Username=ppiq_dev;Password=ppiq_dev_local_only";

    private static PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    private static DashboardWidgetQueryDto Query(string dimension, string measure)
    {
        return new DashboardWidgetQueryDto(
            DashboardMetadataCodes.WidgetTypes.Chart,
            DashboardMetadataCodes.ChartTypes.Bar,
            dimension,
            measure,
            null,
            null,
            null);
    }

    [SkippableFact]
    public async Task ObservationCount_returns_the_whole_population_not_the_old_cap()
    {
        await using var db = NewContext();
        var service = new DashboardWidgetQueryService(db, new DashboardWidgetValidationService());

        // Trusted reference, computed by PostgreSQL over the whole population.
        var trusted = await db.ParameterObservations
            .AsNoTracking()
            .Where(o => !o.IsDeleted)
            .Join(db.MaterialUnits.AsNoTracking().Where(m => !m.IsDeleted), o => o.MaterialUnitId, m => m.Id, (o, m) => o)
            .Join(db.ParameterDefinitions.AsNoTracking(), o => o.ParameterDefinitionId, p => p.Id, (o, p) => o)
            .CountAsync();

        Skip.If(trusted == 0, "no observation population in this database");

        var result = await service.ExecuteAsync(
            Query(DashboardMetadataCodes.Dimensions.Day, DashboardMetadataCodes.Measures.ObservationCount),
            CancellationToken.None);

        Assert.True(result.IsSuccess, "observationCount did not execute");

        // MaxRows caps the ROWS RETURNED, not the population, so the totals are
        // compared only when every group fits. That is the honest comparison:
        // asserting a total over a truncated result set would be the same
        // mistake this engine exists to remove.
        var rows = result.Value!.Rows;
        Assert.NotEmpty(rows);

        if (rows.Count < result.Value!.Widget.MaxRows)
        {
            var total = rows.Sum(r => Convert.ToDecimal(r["value"]));
            Assert.Equal(trusted, (int)total);
            Assert.NotEqual(50000, (int)total);
        }
    }

    [SkippableFact]
    public async Task A_non_seeded_widget_on_a_different_dimension_runs_through_the_same_path()
    {
        await using var db = NewContext();
        var service = new DashboardWidgetQueryService(db, new DashboardWidgetValidationService());

        // NOT a seeded presentation widget. This combination exists in no
        // dashboard, no seeder and no database row. It is composed here, at
        // runtime, exactly as a customer authoring a new page would compose it.
        // If it needs backend code to work, the engine is not generic.
        var result = await service.ExecuteAsync(
            Query(DashboardMetadataCodes.Dimensions.Equipment, DashboardMetadataCodes.Measures.ObservationCount),
            CancellationToken.None);

        Assert.True(result.IsSuccess, "a newly authored dimension/measure combination required backend code");
        Assert.NotEmpty(result.Value!.Rows);
    }

    [SkippableFact]
    public async Task An_unregistered_dimension_is_refused_by_name_not_grouped_under_unknown()
    {
        await using var db = NewContext();
        var service = new DashboardWidgetQueryService(db, new DashboardWidgetValidationService());

        var result = await service.ExecuteAsync(
            Query("bottleFormat", DashboardMetadataCodes.Measures.ObservationCount),
            CancellationToken.None);

        Assert.False(result.IsSuccess, "an unregistered dimension was accepted");
    }

    [SkippableFact]
    public async Task Repeated_identical_requests_return_identical_results()
    {
        await using var db = NewContext();
        var service = new DashboardWidgetQueryService(db, new DashboardWidgetValidationService());

        var fingerprints = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            var result = await service.ExecuteAsync(
                Query(DashboardMetadataCodes.Dimensions.Day, DashboardMetadataCodes.Measures.ObservationCount),
                CancellationToken.None);

            Assert.True(result.IsSuccess);

            fingerprints.Add(string.Join(
                "|",
                result.Value!.Rows.Select(r => r["value"]?.ToString() ?? "")));
        }

        Assert.Single(fingerprints.Distinct());
    }

    [SkippableFact]
    public async Task Material_count_by_equipment_counts_a_material_once_per_group()
    {
        await using var db = NewContext();

        var probe = "D1GEN" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        // Non-steel vocabulary on purpose.
        var site = new Site(probe + "-SITE", probe + " bottling plant", true);
        var area = new Area(site.Id, probe + "-AREA", probe + " filling hall", true);
        var line = new Equipment(site.Id, probe + "-LINE", probe + " filling line 3", "filling-line", true, area.Id);
        var unit = new MaterialUnit(probe + "-BATCH", "Batch", site.Id, "beverage", "still-water-500ml", true);

        try
        {
            db.Sites.Add(site);
            db.Areas.Add(area);
            db.Equipment.Add(line);
            db.MaterialUnits.Add(unit);
            await db.SaveChangesAsync();

            // The same unit passes the same line THREE times. A count would say
            // three. The correct answer is one.
            for (var i = 0; i < 3; i++)
            {
                // (materialUnitId, operationType, startedAtUtc, endedAtUtc,
                // isSynthetic, equipmentId), read from the entity.
                db.ProcessStepExecutions.Add(new ProcessStepExecution(
                    unit.Id,
                    probe + "-STEP" + i,
                    new DateTime(2026, 5, 1, 6 + i, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 1, 6 + i, 30, 0, DateTimeKind.Utc),
                    true,
                    line.Id));
            }

            await db.SaveChangesAsync();

            var service = new DashboardWidgetQueryService(db, new DashboardWidgetValidationService());

            var result = await service.ExecuteAsync(
                Query(DashboardMetadataCodes.Dimensions.Equipment, DashboardMetadataCodes.Measures.MaterialCount),
                CancellationToken.None);

            Assert.True(result.IsSuccess, "materialCount by equipment did not execute");

            var mine = result.Value!.Rows.FirstOrDefault(r =>
                r.TryGetValue(DashboardMetadataCodes.Dimensions.Equipment, out var key) &&
                string.Equals(key?.ToString(), line.Id.ToString(), StringComparison.OrdinalIgnoreCase));

            Assert.True(mine is not null, "the probe line did not appear as its own group");
            Assert.Equal(1m, Convert.ToDecimal(mine!["value"]));

            // Identity and display stay separate concepts.
            Assert.Equal(line.EquipmentName, mine["dimensionLabel"]?.ToString());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM process_step_executions WHERE material_unit_id = {0};", unit.Id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM material_units WHERE id = {0};", unit.Id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM equipment WHERE id = {0};", line.Id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM areas WHERE id = {0};", area.Id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM sites WHERE id = {0};", site.Id);
        }
    }
}