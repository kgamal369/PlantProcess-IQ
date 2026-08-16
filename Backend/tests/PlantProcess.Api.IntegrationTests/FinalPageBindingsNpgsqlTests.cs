using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-047 FINAL. The three last bindings, against real canonical data.
///
/// Includes the P6 evidence T-046-R1 left open: both equipment measures
/// OBSERVED from the real downtime population, not merely declared.
/// </summary>
public sealed class FinalPageBindingsNpgsqlTests : AuthenticatedApiTestBase
{
    public FinalPageBindingsNpgsqlTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private static DashboardWidgetQueryDto Query(string measureCode, string chartType)
    {
        return new DashboardWidgetQueryDto(
            WidgetType: chartType == "table" ? "table" : "chart",
            ChartType: chartType,
            DimensionCode: null,
            MeasureCode: measureCode,
            ParameterCode: null,
            Filters: null,
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync(string measureCode, string chartType)
    {
        Assert.True(IsIntegrationDbReachable(),
            "The final page bindings cannot be certified without a populated database.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(Query(measureCode, chartType), CancellationToken.None);
        Assert.True(result.IsSuccess, measureCode + " failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(ResolveIntegrationTestConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task The_positional_heatmap_publishes_two_axes_from_real_defects()
    {
        var sourceRows = await ScalarAsync(
            "SELECT count(*) FROM quality_events WHERE NOT is_deleted AND position_start_m IS NOT NULL");
        Assert.True(sourceRows > 0, "no positional facts exist; this certification would be vacuous");

        var result = await ExecuteAsync("defectPositionDensity", "heatmap");

        foreach (var role in new[] { "state", "x", "y", "value" })
            Assert.Contains(role, result.Columns.Select(c => c.Code));

        Assert.NotEmpty(result.Rows);
        Assert.Equal("POSITION_DENSITY_PUBLISHED", result.Rows[0]["state"]?.ToString());

        // Two real axes, not one wearing colour.
        Assert.True(result.Rows.Select(r => r["x"]?.ToString()).Distinct().Count() > 1);
        Assert.True(result.Rows.Select(r => r["y"]?.ToString()).Distinct().Count() > 1);

        // Every published cell was observed. The total must equal the source
        // population: no defect invented, none lost.
        var published = result.Rows.Sum(r => Convert.ToInt64(r["value"]));
        Assert.Equal(sourceRows, published);
    }

    [Fact]
    public async Task The_specification_table_reads_every_value_from_canonical_specifications()
    {
        var sourceRows = await ScalarAsync(
            "SELECT count(*) FROM product_specifications WHERE NOT is_deleted");
        Assert.True(sourceRows > 0, "no specifications exist; this certification would be vacuous");

        var result = await ExecuteAsync("specificationLimits", "table");

        Assert.Equal("SPECIFICATIONS_PUBLISHED", result.Rows[0]["state"]?.ToString());
        Assert.Equal(sourceRows, result.Rows.Count);

        foreach (var row in result.Rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row["gradeOrRecipe"]?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(row["parameterCode"]?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(row["provenance"]?.ToString()));
        }

        // A nullable minimum must survive as null, not as a floor of zero.
        var sourceWithoutMin = await ScalarAsync(
            "SELECT count(*) FROM product_specifications WHERE NOT is_deleted AND min_value IS NULL");
        var publishedWithoutMin = result.Rows.Count(r => r["minValue"] is null);
        Assert.Equal(sourceWithoutMin, publishedWithoutMin);
    }

    [Fact]
    public async Task The_equipment_pair_observes_both_measures_from_real_downtime()
    {
        // T-046-R1 P6, closed here.
        var sourceRows = await ScalarAsync(
            "SELECT count(*) FROM downtime_events WHERE NOT is_deleted");
        Assert.True(sourceRows > 0, "no downtime exists; this certification would be vacuous");

        var result = await ExecuteAsync("equipmentStoppageAndImpact", "combo");

        foreach (var role in new[] { "category", "categoryLabel", "seriesALabel", "seriesAValue",
                                     "seriesBLabel", "seriesBValue" })
        {
            Assert.Contains(role, result.Columns.Select(c => c.Code));
        }

        Assert.Equal("STOPPAGE_AND_IMPACT_PUBLISHED", result.Rows[0]["state"]?.ToString());

        foreach (var row in result.Rows)
        {
            // BOTH observed, and each equal to its own canonical quantity.
            Assert.NotNull(row["seriesAValue"]);
            Assert.NotNull(row["seriesBValue"]);
            Assert.Equal(Convert.ToDecimal(row["stoppedMinutes"]), Convert.ToDecimal(row["seriesAValue"]));
            Assert.Equal(Convert.ToDecimal(row["productionImpactMinutes"]), Convert.ToDecimal(row["seriesBValue"]));
        }

        // The two series must not be the same number everywhere: if they were,
        // one is being derived from the other.
        var differs = result.Rows.Any(r =>
            Convert.ToDecimal(r["seriesAValue"]) != Convert.ToDecimal(r["seriesBValue"]));
        Assert.True(differs, "every equipment reported identical stoppage and impact");
    }

    [Fact]
    public async Task Correlation_risk_and_readiness_remain_truthfully_empty_or_refused()
    {
        // Certified as they stand. These assertions exist so a later change
        // cannot quietly turn a governed refusal into a drawn chart.
        var findings = await ExecuteAsync("findingStatus", "table");
        Assert.Equal("NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED", findings.Rows[0]["state"]?.ToString());

        var history = await ExecuteAsync("riskScoreHistory", "table");
        Assert.Equal("INSUFFICIENT_TEMPORAL_RISK_HISTORY", history.Rows[0]["state"]?.ToString());
    }
}