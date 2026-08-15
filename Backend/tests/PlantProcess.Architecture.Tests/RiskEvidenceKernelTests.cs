using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Queries;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045-R1-C. KNOWN ANSWERS FOR THE TWO EVIDENCE KERNELS.
///
/// Both kernels are pure, so the cases that matter most - malformed contributor
/// evidence, and a population with genuine multi-period history - can be proved
/// deterministically WITHOUT touching the database. The live database currently
/// holds one scoring day, and manufacturing a second one in it to make a test
/// pass would be the exact dishonesty this task forbids.
/// </summary>
public class RiskEvidenceKernelTests
{
    // ------------------------------------------------------- the parser

    [Fact]
    public void An_empty_array_is_evidence_of_no_contributors_and_not_an_error()
    {
        var (state, contributors) = RiskContributorParser.Parse("[]");

        Assert.Equal(RiskContributorParser.StateEmpty, state);
        Assert.Empty(contributors);
    }

    [Fact]
    public void An_absent_json_is_also_no_contributors()
    {
        Assert.Equal(RiskContributorParser.StateEmpty, RiskContributorParser.Parse(null).State);
        Assert.Equal(RiskContributorParser.StateEmpty, RiskContributorParser.Parse("   ").State);
    }

    [Fact]
    public void A_real_persisted_contributor_round_trips_every_field()
    {
        // The exact shape measured in the presentation database on 15-Aug.
        const string json = "[{\"weight\": 0.18, \"direction\": \"increase\", " +
            "\"explanation\": \"2 signal(s) exist for this material.\", " +
            "\"contribution\": 0.072, \"contributorCode\": \"EXISTING_QUALITY_SIGNALS\", " +
            "\"contributorName\": \"Existing inspection/quality signals\", " +
            "\"contributorType\": \"Quality\"}]";

        var (state, contributors) = RiskContributorParser.Parse(json);

        Assert.Equal(RiskContributorParser.StateParsed, state);
        var only = Assert.Single(contributors);

        Assert.Equal("EXISTING_QUALITY_SIGNALS", only.ContributorCode);
        Assert.Equal("Existing inspection/quality signals", only.ContributorName);
        Assert.Equal("Quality", only.ContributorType);
        Assert.Equal(0.18m, only.Weight);
        Assert.Equal("increase", only.Direction);
        Assert.Equal(0.072m, only.Contribution);
        Assert.Contains("signal", only.Explanation);
    }

    [Fact]
    public void Absent_optional_fields_stay_null_and_are_never_defaulted()
    {
        var (state, contributors) = RiskContributorParser.Parse("[{\"contributorCode\": \"C1\"}]");

        Assert.Equal(RiskContributorParser.StateParsed, state);
        var only = Assert.Single(contributors);

        // A zero weight would be a measurement. Absent is not zero.
        Assert.Null(only.Weight);
        Assert.Null(only.Contribution);
        Assert.Null(only.ContributorName);
    }

    [Theory]
    [InlineData("{\"contributorCode\": \"C1\"}")]
    [InlineData("[\"not-an-object\"]")]
    [InlineData("[{\"contributorName\": \"no code\"}]")]
    [InlineData("[{\"contributorCode\": \"\"}]")]
    [InlineData("not json at all")]
    [InlineData("[{\"contributorCode\": \"C1\"},")]
    public void Structurally_incompatible_evidence_is_refused_and_never_partially_published(string json)
    {
        var (state, contributors) = RiskContributorParser.Parse(json);

        Assert.Equal(RiskContributorParser.StateMalformed, state);
        Assert.Empty(contributors);
    }

    [Fact]
    public void One_bad_entry_refuses_the_whole_row_rather_than_shortening_the_list()
    {
        // Silently dropping the second entry would publish a one-contributor
        // list that looks complete, and a reader would rank a set missing a
        // member without ever being told.
        var (state, contributors) = RiskContributorParser.Parse(
            "[{\"contributorCode\": \"GOOD\"}, {\"contributorName\": \"no code\"}]");

        Assert.Equal(RiskContributorParser.StateMalformed, state);
        Assert.Empty(contributors);
    }

    // ------------------------------------------------------- the fold

    private static (DateTime, decimal)[] OneDay() => new[]
    {
        (new DateTime(2026, 8, 5, 10, 41, 21, DateTimeKind.Utc), 0.40m),
        (new DateTime(2026, 8, 5, 10, 41, 35, DateTimeKind.Utc), 0.60m),
        (new DateTime(2026, 8, 5, 10, 41, 49, DateTimeKind.Utc), 0.80m)
    };

    private static (DateTime, decimal)[] TwoDays() => new[]
    {
        (new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc), 0.40m),
        (new DateTime(2026, 8, 5, 18, 0, 0, DateTimeKind.Utc), 0.60m),
        (new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc), 0.10m),
        (new DateTime(2026, 8, 6, 21, 0, 0, DateTimeKind.Utc), 0.30m)
    };

    [Fact]
    public void A_single_scoring_batch_is_not_a_trend()
    {
        // This is the CURRENT live population: 500 scores inside 27 seconds.
        Assert.Empty(RiskHistoryFold.Fold(OneDay()));
    }

    [Fact]
    public void Real_multi_period_history_publishes_real_aggregates()
    {
        // The proof that the surface is not permanently refusing: give it two
        // genuine periods and it produces them. No database was altered to
        // create this case.
        var periods = RiskHistoryFold.Fold(TwoDays());

        Assert.Equal(2, periods.Count);

        Assert.Equal(new DateTime(2026, 8, 5), periods[0].PeriodStartUtc);
        Assert.Equal(2, periods[0].ScoredCount);
        Assert.Equal(0.50m, periods[0].AverageScore);
        Assert.Equal(0.40m, periods[0].MinimumScore);
        Assert.Equal(0.60m, periods[0].MaximumScore);

        Assert.Equal(new DateTime(2026, 8, 6), periods[1].PeriodStartUtc);
        Assert.Equal(0.20m, periods[1].AverageScore);
        Assert.Equal(0.10m, periods[1].MinimumScore);
        Assert.Equal(0.30m, periods[1].MaximumScore);
    }

    [Fact]
    public void Periods_are_ordered_oldest_first_regardless_of_input_order()
    {
        var shuffled = TwoDays().Reverse().ToArray();
        var periods = RiskHistoryFold.Fold(shuffled);

        Assert.True(periods[0].PeriodStartUtc < periods[1].PeriodStartUtc);
    }

    [Fact]
    public void The_minimum_is_two_periods_and_it_is_declared_not_inlined()
    {
        Assert.Equal(2, RiskHistoryFold.MinimumPeriods);
    }

    // ------------------------------------------------------- registration

    [Theory]
    [InlineData("riskScoringProvenance")]
    [InlineData("riskScoreContributions")]
    [InlineData("riskScoreHistory")]
    public void Every_risk_evidence_measure_passes_its_registration_gates(string measure)
    {
        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedMeasure(measure));
        Assert.True(DashboardWidgetQuerySafetyRegistry.MeasureProvidesOwnColumns(measure));
        Assert.False(DashboardWidgetQuerySafetyRegistry.MeasureRequiresParameterCode(measure));
    }

    [Fact]
    public void The_measure_codes_are_declared_once_in_the_registry()
    {
        Assert.Equal("riskScoringProvenance", DashboardMetadataCodes.Measures.RiskScoringProvenance);
        Assert.Equal("riskScoreContributions", DashboardMetadataCodes.Measures.RiskScoreContributions);
        Assert.Equal("riskScoreHistory", DashboardMetadataCodes.Measures.RiskScoreHistory);
    }
}