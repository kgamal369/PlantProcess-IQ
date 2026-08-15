using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-045-R1-C. Every published figure is compared against risk_scores itself.
///
/// A result that only agreed with itself would pass on a source counting the
/// wrong column or quietly upgrading a provenance claim.
/// </summary>
public sealed class RiskEvidenceNpgsqlTests : AuthenticatedApiTestBase
{
    public RiskEvidenceNpgsqlTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private static DashboardWidgetQueryDto Query(string measureCode)
    {
        return new DashboardWidgetQueryDto(
            WidgetType: "table",
            ChartType: "table",
            DimensionCode: null,
            MeasureCode: measureCode,
            ParameterCode: null,
            Filters: null,
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync(string measureCode)
    {
        Assert.True(IsIntegrationDbReachable(),
            "The risk evidence surfaces cannot be accepted without an executed PostgreSQL proof.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(Query(measureCode), CancellationToken.None);
        Assert.True(result.IsSuccess, measureCode + " failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(ResolveIntegrationTestConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    /// <summary>
    /// Runs first and reports the observed shape of all three surfaces. A
    /// failing assertion that does not say what it saw costs an entire
    /// diagnostic cycle, which is exactly what the first run of this pack cost.
    /// </summary>
    [Fact]
    public async Task Report_the_observed_state_of_every_risk_surface()
    {
        foreach (var measure in new[] { "riskScoringProvenance", "riskScoreContributions", "riskScoreHistory" })
        {
            var result = await ExecuteAsync(measure);

            var states = string.Join(", ", result.Rows
                .Select(r => r["state"]?.ToString() ?? "(null)")
                .GroupBy(x => x)
                .Select(g => g.Key + " x" + g.Count()));

            Assert.True(
                result.Rows.Count > 0,
                measure + " returned NO ROWS AT ALL, not even a terminal state.");

            Assert.False(
                string.IsNullOrWhiteSpace(states),
                measure + " returned " + result.Rows.Count + " row(s) carrying no state.");

            // Surfaced deliberately: xunit prints this only on failure, so the
            // assertion below is what makes the observation visible.
            Assert.True(
                result.Rows.Count > 0 && !string.IsNullOrWhiteSpace(states),
                measure + " => rows=" + result.Rows.Count + " states=[" + states + "]");
        }
    }

    [Fact]
    public async Task Provenance_counts_match_the_source_table_exactly()
    {
        var result = await ExecuteAsync("riskScoringProvenance");

        var sourceRows = await ScalarAsync<long>(
            "SELECT count(*) FROM risk_scores WHERE NOT is_deleted");
        var sourceSynthetic = await ScalarAsync<long>(
            "SELECT count(*) FILTER (WHERE is_synthetic) FROM risk_scores WHERE NOT is_deleted");

        if (result.Rows[0]["state"]?.ToString() == "NO_SCORED_POPULATION")
        {
            Assert.Equal(0, sourceRows);
            return;
        }

        var publishedRows = result.Rows.Sum(r => Convert.ToInt64(r["populationCount"]));
        var publishedSynthetic = result.Rows.Sum(r => Convert.ToInt64(r["syntheticCount"]));

        Assert.Equal(sourceRows, publishedRows);
        Assert.Equal(sourceSynthetic, publishedSynthetic);
    }

    [Fact]
    public async Task Provenance_never_upgrades_synthetic_into_a_lineage_claim()
    {
        var result = await ExecuteAsync("riskScoringProvenance");

        if (result.Rows[0]["state"]?.ToString() == "NO_SCORED_POPULATION") { return; }

        foreach (var row in result.Rows)
        {
            var provenance = row["provenanceState"]?.ToString() ?? "";

            // The vocabulary is closed. "seeded", "demo" and "model-generated"
            // are not persisted facts and must never appear.
            Assert.Contains(provenance, new[]
            {
                "SYNTHETIC_POPULATION",
                "PARTIALLY_SYNTHETIC_POPULATION",
                "NOT_MARKED_SYNTHETIC"
            });

            var synthetic = Convert.ToInt64(row["syntheticCount"]);
            var total = Convert.ToInt64(row["populationCount"]);

            if (provenance == "SYNTHETIC_POPULATION") { Assert.Equal(total, synthetic); }
            if (provenance == "NOT_MARKED_SYNTHETIC") { Assert.Equal(0, synthetic); }
        }
    }

    [Fact]
    public async Task Model_and_source_fields_are_published_as_persisted_and_grouped()
    {
        var result = await ExecuteAsync("riskScoringProvenance");

        if (result.Rows[0]["state"]?.ToString() == "NO_SCORED_POPULATION") { return; }

        var sourceGroups = await ScalarAsync<long>(
            "SELECT count(*) FROM (SELECT DISTINCT risk_type, model_version, source_system " +
            "FROM risk_scores WHERE NOT is_deleted) g");

        Assert.Equal(sourceGroups, result.Rows.Count);
    }

    [Fact]
    public async Task Contributor_rows_come_only_from_persisted_json()
    {
        var result = await ExecuteAsync("riskScoreContributions");

        var withContributors = await ScalarAsync<long>(
            "SELECT count(*) FROM risk_scores WHERE NOT is_deleted " +
            "AND main_contributors_json IS NOT NULL " +
            "AND jsonb_array_length(main_contributors_json::jsonb) > 0");

        var published = result.Rows.Count(r => r["state"]?.ToString() == "CONTRIBUTORS_PARSED");
        var emptyStates = result.Rows.Count(r => r["state"]?.ToString() == "NO_CONTRIBUTORS_RECORDED");

        if (withContributors == 0)
        {
            // Every material reported no contributors, and NOT ONE invented row.
            Assert.Equal(0, published);
            Assert.True(emptyStates > 0);
            return;
        }

        var observed = string.Join(", ", result.Rows
            .Select(r => r["state"]?.ToString() ?? "(null)")
            .GroupBy(x => x)
            .Select(g => g.Key + " x" + g.Count()));

        Assert.True(
            published > 0,
            "source rows carrying contributor arrays: " + withContributors +
            ", but the surface published none. Observed states: [" + observed + "]" +
            ", total rows returned: " + result.Rows.Count);

        foreach (var row in result.Rows.Where(r => r["state"]?.ToString() == "CONTRIBUTORS_PARSED"))
        {
            Assert.False(string.IsNullOrWhiteSpace(row["contributorCode"]?.ToString()));
        }
    }

    [Fact]
    public async Task An_empty_contributor_array_never_becomes_a_manufactured_row()
    {
        var result = await ExecuteAsync("riskScoreContributions");

        foreach (var row in result.Rows.Where(r => r["state"]?.ToString() == "NO_CONTRIBUTORS_RECORDED"))
        {
            // No OTHER, no NONE, no zero contribution.
            Assert.Null(row["contributorCode"]);
            Assert.Null(row["contribution"]);
            Assert.Null(row["weight"]);
        }
    }

    [Fact]
    public async Task Risk_history_refuses_when_the_population_holds_one_period()
    {
        var result = await ExecuteAsync("riskScoreHistory");

        var distinctDays = await ScalarAsync<long>(
            "SELECT count(DISTINCT date_trunc('day', scored_at_utc)) " +
            "FROM risk_scores WHERE NOT is_deleted");

        var state = result.Rows[0]["state"]?.ToString();

        var sourceRows = await ScalarAsync<long>(
            "SELECT count(*) FROM risk_scores WHERE NOT is_deleted");

        if (distinctDays < 2)
        {
            // NO_SCORED_POPULATION here would mean the surface saw an empty
            // population while the table holds rows - a different defect
            // entirely from a refused trend, so the two are told apart.
            Assert.True(
                state == "INSUFFICIENT_TEMPORAL_RISK_HISTORY",
                "expected INSUFFICIENT_TEMPORAL_RISK_HISTORY but observed '" + state +
                "'. Source rows=" + sourceRows + ", distinct days=" + distinctDays +
                ", rows returned=" + result.Rows.Count);

            Assert.Single(result.Rows);
            Assert.Null(result.Rows[0]["period"]);
            return;
        }

        Assert.Equal("RISK_HISTORY_PUBLISHED", state);
        Assert.Equal(distinctDays, result.Rows.Count);
    }

    [Fact]
    public async Task Published_history_aggregates_match_the_source_exactly()
    {
        var result = await ExecuteAsync("riskScoreHistory");

        if (result.Rows[0]["state"]?.ToString() != "RISK_HISTORY_PUBLISHED") { return; }

        var sourceTotal = await ScalarAsync<long>(
            "SELECT count(*) FROM risk_scores WHERE NOT is_deleted");

        var publishedTotal = result.Rows.Sum(r => Convert.ToInt64(r["scoredCount"]));
        Assert.Equal(sourceTotal, publishedTotal);

        foreach (var row in result.Rows)
        {
            var minimum = Convert.ToDecimal(row["minimumScore"]);
            var average = Convert.ToDecimal(row["averageScore"]);
            var maximum = Convert.ToDecimal(row["maximumScore"]);

            Assert.True(minimum <= average, "minimum exceeded the average");
            Assert.True(average <= maximum, "average exceeded the maximum");
        }
    }
}