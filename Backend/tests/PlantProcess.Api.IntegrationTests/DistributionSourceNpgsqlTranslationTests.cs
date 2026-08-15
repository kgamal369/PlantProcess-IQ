using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using Xunit;

namespace PlantProcess.Api.IntegrationTests;

/// <summary>
/// T-047 Pack A. THE NPGSQL TRANSLATION PROOF.
///
/// dotnet build cannot see whether a LINQ grouping expression translates. The
/// binning GroupBy is the one line in this slice that can compile, pass every
/// unit test, and still throw the first time a real widget asks for it.
///
/// These run the REAL query service through DI against a REAL PostgreSQL, so a
/// translation failure surfaces here rather than in a browser.
///
/// WHY THIS FAILS RATHER THAN SKIPS. A skipped test is not a proof, and the
/// acceptance condition for leaving Histogram Implemented is that the
/// translation was OBSERVED. On a machine with no integration database this
/// class fails loudly and truthfully instead of reporting green.
/// </summary>
public sealed class DistributionSourceNpgsqlTranslationTests : AuthenticatedApiTestBase
{
    public DistributionSourceNpgsqlTranslationTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    private static DashboardWidgetQueryDto Query(string measureCode, string? parameterCode)
    {
        return new DashboardWidgetQueryDto(
            WidgetType: "chart",
            ChartType: "histogram",
            DimensionCode: null,
            MeasureCode: measureCode,
            ParameterCode: parameterCode,
            Filters: null,
            Options: null);
    }

    private async Task<DashboardWidgetQueryResultDto> ExecuteAsync(DashboardWidgetQueryDto query)
    {
        Assert.True(
            IsIntegrationDbReachable(),
            "The distribution slice cannot be accepted without an executed Npgsql proof. " +
            "No integration database was reachable, so the binning expression is UNPROVEN.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess, "The distribution query failed: " + result.Error?.Message);
        return result.Value!;
    }

    private static void AssertPublishedShape(DashboardWidgetQueryResultDto result)
    {
        // Roles are bound by name, so the proof asserts names and never ordinals.
        var codes = result.Columns.Select(x => x.Code).ToList();
        foreach (var role in new[] { "state", "binLabel", "binLower", "binUpper", "count" })
            Assert.Contains(role, codes);

        Assert.NotEmpty(result.Rows);

        var state = result.Rows[0]["state"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(state), "A distribution answered without a terminal state.");

        if (state == "DISTRIBUTION_PUBLISHED")
        {
            // Bins came back, which is what proves floor() translated and
            // executed rather than merely compiling.
            Assert.All(result.Rows, row => Assert.NotNull(row["binLabel"]));
            Assert.All(result.Rows, row => Assert.NotNull(row["count"]));
            Assert.True(result.Rows.Count >= 2, "A published distribution must carry at least two intervals.");
        }
        else
        {
            // A truthful terminal state is a valid measured result. It must
            // still be one of the declared four and must carry no bins.
            Assert.Contains(state, new[]
            {
                "PARAMETER_NOT_SELECTED",
                "NO_OBSERVATIONS_IN_SELECTION",
                "SINGLE_VALUE_POPULATION"
            });

            Assert.Single(result.Rows);
            Assert.Null(result.Rows[0]["binLabel"]);
        }
    }

    [Fact]
    public async Task Risk_score_distribution_executes_against_postgres()
    {
        var result = await ExecuteAsync(Query("riskScoreDistribution", parameterCode: null));
        AssertPublishedShape(result);
    }

    [Fact]
    public async Task Parameter_value_distribution_executes_against_postgres()
    {
        // Any real parameter code exercises the same join and the same binning.
        // Taking one from the database rather than naming one keeps this test
        // free of plant vocabulary and portable to another installation.
        string? parameterCode;

        Assert.True(IsIntegrationDbReachable(),
            "No integration database was reachable, so the binning expression is UNPROVEN.");

        await using (var connection = new Npgsql.NpgsqlConnection(ResolveIntegrationTestConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT parameter_code FROM parameter_definitions WHERE NOT is_deleted LIMIT 1";
            parameterCode = (await command.ExecuteScalarAsync())?.ToString();
        }

        Assert.False(string.IsNullOrWhiteSpace(parameterCode),
            "No parameter definitions exist, so the parameter distribution cannot be proven here.");

        var result = await ExecuteAsync(Query("parameterValueDistribution", parameterCode));
        AssertPublishedShape(result);
    }

    /// <summary>
    /// MEASURED, 15-Aug. An unnamed parameter is refused by the VALIDATOR, not
    /// by the source. Registering the measure in MeasuresRequiringParameter -
    /// the same treatment avgParameterValue receives - means the query never
    /// reaches the source at all.
    ///
    /// The source's PARAMETER_NOT_SELECTED state therefore does not surface on
    /// this path. It is kept as defence in depth for a direct call, and this
    /// test asserts the behaviour that actually occurs rather than the one the
    /// source was written to expect. Two mechanisms guard one condition; the
    /// outer one wins, and saying so is cheaper than discovering it later.
    /// </summary>
    [Fact]
    public async Task An_unnamed_parameter_is_refused_by_name_before_reaching_the_source()
    {
        Assert.True(
            IsIntegrationDbReachable(),
            "The distribution slice cannot be accepted without an executed Npgsql proof.");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardWidgetQueryService>();

        var result = await service.ExecuteAsync(
            Query("parameterValueDistribution", parameterCode: null),
            CancellationToken.None);

        Assert.False(result.IsSuccess, "A parameter distribution with no parameter was accepted.");
        Assert.False(
            string.IsNullOrWhiteSpace(result.Error?.Message),
            "The refusal carried no sentence a human can act on.");
    }
}