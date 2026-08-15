using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-047 Pack D. Both multi-series sources run against a real PostgreSQL.
///
/// The risk here is translation: a grouped DISTINCT count and a left-joined
/// two-key grouping are the two expressions in this slice that compile
/// cleanly and can still fail the first time a widget asks for them.
/// </summary>
public sealed class MultiSeriesSourcesNpgsqlTests : AuthenticatedApiTestBase
{
    public MultiSeriesSourcesNpgsqlTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private static DashboardWidgetQueryDto Query(string measureCode, string? dimensionCode)
    {
        return new DashboardWidgetQueryDto(
            WidgetType: "chart",
            ChartType: "stackedColumn",
            DimensionCode: dimensionCode,
            MeasureCode: measureCode,
            ParameterCode: null,
            Filters: null,
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync(string measureCode, string? dimensionCode)
    {
        Assert.True(IsIntegrationDbReachable(),
            "The multi-series slice cannot be accepted without an executed PostgreSQL proof.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(Query(measureCode, dimensionCode), CancellationToken.None);
        Assert.True(result.IsSuccess, measureCode + " failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static void AssertSeriesShape(DashboardWidgetQueryResultDto result)
    {
        var codes = result.Columns.Select(c => c.Code).ToList();
        foreach (var role in new[] { "state", "category", "categoryLabel", "series", "seriesLabel", "value" })
            Assert.Contains(role, codes);

        Assert.NotEmpty(result.Rows);
        var state = result.Rows[0]["state"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(state));

        if (state != "SERIES_PUBLISHED")
        {
            Assert.Contains(state, new[]
            {
                "NO_OBSERVATIONS_IN_SELECTION",
                "SINGLE_SERIES_POPULATION",
                "POPULATION_EXCEEDS_SAFE_LIMIT"
            });
            Assert.Single(result.Rows);
            return;
        }

        // A published composition must carry at least two series, or the
        // single-series refusal failed to fire.
        var series = result.Rows.Select(r => r["series"]?.ToString()).Distinct().ToList();
        Assert.True(series.Count >= 2, "A published stack carried only one series.");

        foreach (var row in result.Rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row["category"]?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(row["series"]?.ToString()));
            Assert.NotNull(row["value"]);
        }

        // One cell per (category, series). A duplicate would double-count in
        // the stack and still render as a taller, plausible column.
        var cells = result.Rows
            .Select(r => (r["category"]?.ToString() ?? "") + "|" + (r["series"]?.ToString() ?? ""))
            .ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    /// <summary>
    /// This source derives its own category from the step's day and its own
    /// series from the crew, so it takes no grouping dimension - which is why
    /// PO_WEEK is seeded dimensionless.
    /// </summary>
    [Fact]
    public async Task Throughput_by_shift_executes_against_postgres()
    {
        AssertSeriesShape(await ExecuteAsync("materialThroughputByShift", dimensionCode: null));
    }

    /// <summary>
    /// The grouping is the one QM_BREAK actually carries. v2 of this test sent
    /// none and read the resulting GROUPING_NOT_SELECTED as a pass, which would
    /// have proved only that the test forgot its own grouping.
    /// </summary>
    [Fact]
    public async Task Defect_type_mix_executes_against_postgres()
    {
        AssertSeriesShape(await ExecuteAsync("defectTypeMix", "gradeOrRecipe"));
    }

    /// <summary>
    /// The refusal has its own test rather than being folded into the accepted
    /// states of the one above, so a real query and a missing grouping can
    /// never be mistaken for each other.
    /// </summary>
    [Fact]
    public async Task Defect_type_mix_without_a_grouping_says_so()
    {
        var result = await ExecuteAsync("defectTypeMix", dimensionCode: null);

        Assert.Single(result.Rows);
        Assert.Equal("GROUPING_NOT_SELECTED", result.Rows[0]["state"]?.ToString());
    }

    [Fact]
    public async Task Both_sources_publish_the_same_roles_from_different_questions()
    {
        var throughput = await ExecuteAsync("materialThroughputByShift", dimensionCode: null);
        var defects = await ExecuteAsync("defectTypeMix", "gradeOrRecipe");

        Assert.Equal(
            throughput.Columns.Select(c => c.Code).ToList(),
            defects.Columns.Select(c => c.Code).ToList());
    }
}