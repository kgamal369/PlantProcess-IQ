using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-047 Pack C2. The relationship source runs against a real PostgreSQL.
///
/// What needs proving here is not translation but PAIRING: that every point
/// comes from a material carrying readings for BOTH parameters, and that a
/// material measured for only one contributes nothing.
/// </summary>
public sealed class ParameterRelationshipNpgsqlTests : AuthenticatedApiTestBase
{
    public ParameterRelationshipNpgsqlTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private static DashboardWidgetQueryDto Query(string? x, string? y)
    {
        return new DashboardWidgetQueryDto(
            WidgetType: "chart",
            ChartType: "scatter",
            DimensionCode: null,
            MeasureCode: "parameterRelationship",
            ParameterCode: x,
            Filters: y is null
                ? null
                : new DashboardWidgetFiltersDto(null, null, null, null, null, null, null, null, null, y, null, null),
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync(DashboardWidgetQueryDto query)
    {
        Assert.True(IsIntegrationDbReachable(),
            "The relationship slice cannot be accepted without an executed PostgreSQL proof.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(query, CancellationToken.None);
        Assert.True(result.IsSuccess, "The relationship query failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static async Task<(string X, string Y)> AnOverlappingPairAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(ResolveIntegrationTestConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            WITH per_material AS (
              SELECT o.material_unit_id, d.parameter_code
              FROM parameter_observations o
              JOIN parameter_definitions d ON d.id = o.parameter_definition_id
              WHERE NOT o.is_deleted AND o.numeric_value IS NOT NULL
              GROUP BY o.material_unit_id, d.parameter_code
            )
            SELECT a.parameter_code, b.parameter_code
            FROM per_material a
            JOIN per_material b
              ON a.material_unit_id = b.material_unit_id
             AND a.parameter_code < b.parameter_code
            GROUP BY a.parameter_code, b.parameter_code
            HAVING count(*) >= 5
            ORDER BY count(*) DESC
            LIMIT 1;";

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "No two parameters share five materials.");
        return (reader.GetString(0), reader.GetString(1));
    }

    [Fact]
    public async Task A_relationship_between_two_real_parameters_executes()
    {
        var pair = await AnOverlappingPairAsync();
        var result = await ExecuteAsync(Query(pair.X, pair.Y));

        var codes = result.Columns.Select(c => c.Code).ToList();
        foreach (var role in new[]
                 { "state", "materialUnitId", "materialLabel", "xValue", "yValue", "xParameterCode", "yParameterCode" })
        {
            Assert.Contains(role, codes);
        }

        Assert.NotEmpty(result.Rows);
        Assert.Equal("RELATIONSHIP_PUBLISHED", result.Rows[0]["state"]?.ToString());

        // EVERY point must carry both quantities and a material identity. A
        // half-populated point is a fabricated pairing.
        foreach (var row in result.Rows)
        {
            Assert.NotNull(row["xValue"]);
            Assert.NotNull(row["yValue"]);
            Assert.False(string.IsNullOrWhiteSpace(row["materialUnitId"]?.ToString()));
            Assert.Equal(pair.X, row["xParameterCode"]?.ToString());
            Assert.Equal(pair.Y, row["yParameterCode"]?.ToString());
        }

        // Each material appears at most once: one aggregated value per side.
        var ids = result.Rows.Select(r => r["materialUnitId"]?.ToString()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task A_parameter_against_itself_is_refused_not_drawn()
    {
        var pair = await AnOverlappingPairAsync();
        var result = await ExecuteAsync(Query(pair.X, pair.X));

        Assert.Single(result.Rows);
        Assert.Equal("SAME_PARAMETER_SELECTED", result.Rows[0]["state"]?.ToString());
    }

    [Fact]
    public async Task A_missing_second_parameter_says_so()
    {
        var pair = await AnOverlappingPairAsync();
        var result = await ExecuteAsync(Query(pair.X, y: null));

        Assert.Single(result.Rows);
        Assert.Equal("SECOND_PARAMETER_NOT_SELECTED", result.Rows[0]["state"]?.ToString());
    }
}