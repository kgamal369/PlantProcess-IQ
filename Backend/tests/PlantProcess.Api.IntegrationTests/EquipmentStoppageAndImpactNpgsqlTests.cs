using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-045-R1-D. The surface is checked against the SOURCE TABLE, not against
/// itself.
///
/// A test that only asserted the result is internally consistent would pass on
/// a source that silently summed the wrong column. Every total below is
/// compared to an independent SQL aggregation of the same canonical field.
/// </summary>
public sealed class EquipmentStoppageAndImpactNpgsqlTests : AuthenticatedApiTestBase
{
    public EquipmentStoppageAndImpactNpgsqlTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private sealed record SourceTotal(string EquipmentId, decimal Stopped, decimal Impact);

    private static DashboardWidgetQueryDto Query()
    {
        return new DashboardWidgetQueryDto(
            WidgetType: "table",
            ChartType: "table",
            DimensionCode: null,
            MeasureCode: "equipmentStoppageAndImpact",
            ParameterCode: null,
            Filters: null,
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync()
    {
        Assert.True(IsIntegrationDbReachable(),
            "The equipment impact surface cannot be accepted without an executed PostgreSQL proof.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(Query(), CancellationToken.None);
        Assert.True(result.IsSuccess, "The equipment impact query failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static async Task<Dictionary<string, SourceTotal>> SourceTotalsAsync()
    {
        var totals = new Dictionary<string, SourceTotal>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new Npgsql.NpgsqlConnection(ResolveIntegrationTestConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT e.id::text,
                   COALESCE(SUM(d.stopped_minutes), 0),
                   COALESCE(SUM(d.production_impact_minutes), 0)
            FROM downtime_events d
            JOIN equipment e ON e.id = d.equipment_id
            WHERE NOT d.is_deleted AND NOT e.is_deleted
            GROUP BY e.id;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            totals[reader.GetString(0)] = new SourceTotal(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2));

        return totals;
    }

    [Fact]
    public async Task Every_total_matches_an_independent_aggregation_of_its_own_column()
    {
        var result = await ExecuteAsync();
        var source = await SourceTotalsAsync();

        var state = result.Rows[0]["state"]?.ToString();

        if (state != "STOPPAGE_AND_IMPACT_PUBLISHED")
        {
            // A truthful terminal state is only acceptable if the source table
            // genuinely holds nothing. Otherwise the surface is hiding data.
            Assert.Equal("NO_DOWNTIME_IN_SELECTION", state);
            Assert.Empty(source);
            return;
        }

        Assert.NotEmpty(source);

        foreach (var row in result.Rows)
        {
            var id = row["equipmentId"]?.ToString() ?? "";
            Assert.True(source.ContainsKey(id), "the surface reported an equipment the source does not group: " + id);

            Assert.Equal(source[id].Stopped, Convert.ToDecimal(row["stoppedMinutes"]));
            Assert.Equal(source[id].Impact, Convert.ToDecimal(row["productionImpactMinutes"]));
        }
    }

    [Fact]
    public async Task The_published_roles_are_all_present_and_bound_by_name()
    {
        var result = await ExecuteAsync();

        var codes = result.Columns.Select(c => c.Code).ToList();
        foreach (var role in new[]
                 { "state", "equipmentId", "equipmentCode", "equipmentLabel", "stoppedMinutes", "productionImpactMinutes" })
        {
            Assert.Contains(role, codes);
        }
    }

    [Fact]
    public async Task One_row_per_equipment_and_no_manufactured_value()
    {
        var result = await ExecuteAsync();

        if (result.Rows[0]["state"]?.ToString() != "STOPPAGE_AND_IMPACT_PUBLISHED")
            return;

        var ids = result.Rows.Select(r => r["equipmentId"]?.ToString()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        foreach (var row in result.Rows)
        {
            // Present, because the row exists only where events exist. An
            // absent quantity would be a null, never a substituted zero.
            Assert.NotNull(row["stoppedMinutes"]);
            Assert.NotNull(row["productionImpactMinutes"]);
            Assert.False(string.IsNullOrWhiteSpace(row["equipmentCode"]?.ToString()));
        }
    }

    [Fact]
    public async Task The_two_quantities_are_reported_independently()
    {
        var result = await ExecuteAsync();

        if (result.Rows[0]["state"]?.ToString() != "STOPPAGE_AND_IMPACT_PUBLISHED")
            return;

        // Not an assertion that they differ - on some datasets they legitimately
        // will not. It asserts each equals ITS OWN source column, which a
        // derivation could not satisfy for both at once.
        var source = await SourceTotalsAsync();

        foreach (var row in result.Rows)
        {
            var id = row["equipmentId"]?.ToString() ?? "";
            Assert.Equal(source[id].Stopped, Convert.ToDecimal(row["stoppedMinutes"]));
            Assert.Equal(source[id].Impact, Convert.ToDecimal(row["productionImpactMinutes"]));
        }
    }
}