using PlantProcess.Analytics.Core.Readiness;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.Readiness;

/// <summary>
/// T-045-R1-A. KNOWN ANSWERS FOR THE READINESS MEASUREMENT.
///
/// The gate always knew the value and the bounds; it wrote them into a sentence
/// and dropped them. These tests assert the STRUCTURED fields and the SENTENCE
/// agree, so the two can never drift apart - if someone edits one wording and
/// not the other, this fails rather than shipping a card whose number and whose
/// explanation disagree.
///
/// The thresholds below are the shipped defaults in ReadinessThresholds. They
/// are read from the record rather than retyped, so a threshold change is a
/// product decision and not a test failure.
/// </summary>
public class ReadinessDimensionParityTests
{
    private static readonly ReadinessThresholds Defaults = new();

    private static ReadinessReport Evaluate(
        int heats = 100, int events = 100, double minority = 0.50,
        double freshness = 0.5, double completeness = 0.99)
    {
        return ReadinessGate.Evaluate(
            new ReadinessInput(heats, events, minority, freshness, completeness));
    }

    private static ReadinessDimension Dimension(ReadinessReport report, string startsWith)
    {
        var match = report.Dimensions.Single(d => d.Name.StartsWith(startsWith, StringComparison.Ordinal));
        return match;
    }

    [Fact]
    public void All_five_dimensions_carry_a_measurement_and_both_bounds()
    {
        var report = Evaluate();

        Assert.Equal(5, report.Dimensions.Count);

        foreach (var dimension in report.Dimensions)
        {
            Assert.NotEqual(dimension.ReadyThreshold, dimension.PartialThreshold);
        }
    }

    [Fact]
    public void A_ready_count_reports_the_value_it_was_judged_on()
    {
        var dimension = Dimension(Evaluate(heats: 100), "Independent heats");

        Assert.Equal(ReadinessState.Ready, dimension.State);
        Assert.Equal(100d, dimension.MeasuredValue);
        Assert.Equal(Defaults.HeatsReady, dimension.ReadyThreshold);
        Assert.Equal(Defaults.HeatsPartial, dimension.PartialThreshold);
        Assert.True(dimension.HigherIsBetter);
    }

    [Fact]
    public void A_partial_count_reports_the_value_it_was_judged_on()
    {
        // 20 sits inside [EventsPartial, EventsReady).
        var dimension = Dimension(Evaluate(events: 20), "Outcome events");

        Assert.Equal(ReadinessState.Partial, dimension.State);
        Assert.Equal(20d, dimension.MeasuredValue);
    }

    [Fact]
    public void A_blocked_fraction_reports_how_far_short_it_fell()
    {
        var dimension = Dimension(Evaluate(minority: 0.01), "Minority-class balance");

        Assert.Equal(ReadinessState.Blocked, dimension.State);
        Assert.Equal(0.01d, dimension.MeasuredValue);
        Assert.Equal(Defaults.MinorityPartial, dimension.PartialThreshold);
    }

    [Fact]
    public void The_freshness_dimension_declares_that_lower_is_better()
    {
        // THE REASON THIS FIELD EXISTS. Freshness 1.5 against a ready bound of
        // 1.0 is a SHORTFALL. Without HigherIsBetter a bar would draw it as an
        // overshoot and report a failing dimension as a passing one.
        var dimension = Dimension(Evaluate(freshness: 1.5), "Freshness");

        Assert.False(dimension.HigherIsBetter);
        Assert.Equal(ReadinessState.Partial, dimension.State);
        Assert.Equal(1.5d, dimension.MeasuredValue);
    }

    [Fact]
    public void Every_dimension_states_its_measured_value_in_its_own_sentence()
    {
        // The structured field and the prose must never disagree. The FORMAT
        // per dimension is hardcoded here on purpose: a test may know which
        // gate helper each dimension uses, because it is asserting a known
        // answer. The product carries no such knowledge, which is why no unit
        // field was invented for it.
        var report = Evaluate(heats: 45, events: 20, minority: 0.05, freshness: 1.5, completeness: 0.90);

        var expected = new (string Prefix, string Format)[]
        {
            ("Independent heats", "0"),
            ("Outcome events", "0"),
            ("Minority-class balance", "P1"),
            ("Freshness", "F2"),
            ("Required-field completeness", "P1")
        };

        foreach (var (prefix, format) in expected)
        {
            var dimension = Dimension(report, prefix);
            Assert.Contains(dimension.MeasuredValue.ToString(format), dimension.Reason);
        }
    }

    [Fact]
    public void A_completeness_shortfall_can_now_say_how_far_short()
    {
        // The sentence the abstention beat needs: not "Blocked", but
        // "Blocked, 46.5% against an 85.0% bar".
        var dimension = Dimension(Evaluate(completeness: 0.465), "Required-field completeness");

        Assert.Equal(ReadinessState.Blocked, dimension.State);
        Assert.Equal(0.465d, dimension.MeasuredValue);
        Assert.Equal(Defaults.CompletenessPartial, dimension.PartialThreshold);
        Assert.Equal(Defaults.CompletenessReady, dimension.ReadyThreshold);
    }
}