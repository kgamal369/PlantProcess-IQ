using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-047 Pack B. The spread source runs against a real PostgreSQL.
///
/// The binning proof in Pack A covered a SQL-side GroupBy. This source instead
/// fetches a BOUNDED population and summarises in memory, so what needs proving
/// here is different: that the bounded fetch executes, that an over-limit
/// population refuses instead of truncating, and that a grouping the source
/// cannot resolve says so rather than returning an empty chart.
/// </summary>
public sealed class ParameterValueSpreadNpgsqlTests : AuthenticatedApiTestBase
{
    public ParameterValueSpreadNpgsqlTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private static DashboardWidgetQueryDto Query(string? dimensionCode, string? parameterCode)
    {
        return new DashboardWidgetQueryDto(
            WidgetType: "chart",
            ChartType: "boxPlot",
            DimensionCode: dimensionCode,
            MeasureCode: "parameterValueSpread",
            ParameterCode: parameterCode,
            Filters: null,
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync(DashboardWidgetQueryDto query)
    {
        Assert.True(
            IsIntegrationDbReachable(),
            "The spread slice cannot be accepted without an executed PostgreSQL proof.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(query, CancellationToken.None);
        Assert.True(result.IsSuccess, "The spread query failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static async Task<string?> AnyParameterCodeAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(ResolveIntegrationTestConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT parameter_code FROM parameter_definitions WHERE NOT is_deleted LIMIT 1";
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    [Fact]
    public async Task A_spread_by_grouping_executes_against_postgres()
    {
        Assert.True(IsIntegrationDbReachable(), "No integration database was reachable.");

        var parameterCode = await AnyParameterCodeAsync();
        Assert.False(string.IsNullOrWhiteSpace(parameterCode), "No parameter definitions exist.");

        var result = await ExecuteAsync(Query("gradeOrRecipe", parameterCode));

        var codes = result.Columns.Select(x => x.Code).ToList();
        foreach (var role in new[]
                 { "state", "category", "label", "minimum", "q1", "median", "q3", "maximum", "observationCount" })
        {
            Assert.Contains(role, codes);
        }

        Assert.NotEmpty(result.Rows);

        var state = result.Rows[0]["state"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(state));

        if (state == "SPREAD_PUBLISHED")
        {
            // Quartiles must be ordered. An unordered summary would render as a
            // box drawn inside out and would still look like a chart.
            foreach (var row in result.Rows.Where(r => r["state"]?.ToString() == "SPREAD_PUBLISHED"))
            {
                var minimum = Convert.ToDecimal(row["minimum"]);
                var q1 = Convert.ToDecimal(row["q1"]);
                var median = Convert.ToDecimal(row["median"]);
                var q3 = Convert.ToDecimal(row["q3"]);
                var maximum = Convert.ToDecimal(row["maximum"]);

                Assert.True(minimum <= q1, "minimum exceeded the lower quartile");
                Assert.True(q1 <= median, "lower quartile exceeded the median");
                Assert.True(median <= q3, "median exceeded the upper quartile");
                Assert.True(q3 <= maximum, "upper quartile exceeded the maximum");
            }
        }
    }

    [Fact]
    public async Task An_unresolvable_grouping_says_so_rather_than_returning_nothing()
    {
        var parameterCode = await AnyParameterCodeAsync();
        Assert.False(string.IsNullOrWhiteSpace(parameterCode), "No parameter definitions exist.");

        // A registered dimension this source cannot resolve to a group.
        var result = await ExecuteAsync(Query("equipment", parameterCode));

        Assert.Single(result.Rows);
        Assert.Equal("GROUPING_NOT_SELECTED", result.Rows[0]["state"]?.ToString());
    }

    [Fact]
    public async Task An_unnamed_parameter_is_refused_by_name_before_reaching_the_source()
    {
        Assert.True(IsIntegrationDbReachable(), "No integration database was reachable.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(
            Query("gradeOrRecipe", parameterCode: null), CancellationToken.None);

        Assert.False(result.IsSuccess, "A spread with no parameter was accepted.");
        Assert.False(string.IsNullOrWhiteSpace(result.Error?.Message));
    }
}