using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using PlantProcess.Analytics.Core.Readiness;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.Journey;

/// <summary>
/// Automated evidence for canonical journey steps 8-10 and 11-13.
/// These tests deliberately validate honest method selection, readiness blocking,
/// effect behavior and null-control behavior without depending on seeded product data.
/// </summary>
public sealed class CanonicalJourneyAnalyticsContractTests
{
    [Fact]
    public void J09_Readiness_gate_allows_a_well_supported_population()
    {
        var report = ReadinessGate.Evaluate(new ReadinessInput(
            IndependentHeats: 120,
            OutcomeEvents: 75,
            MinorityClassFraction: 0.18,
            FreshnessFactor: 0.8,
            RequiredFieldCompleteness: 0.99));

        Assert.True(report.CanRun);
        Assert.Equal(ReadinessState.Ready, report.Overall);
        Assert.All(report.Dimensions, dimension => Assert.Equal(ReadinessState.Ready, dimension.State));
    }

    [Fact]
    public void J09_Readiness_gate_blocks_an_undersampled_population_and_explains_why()
    {
        var report = ReadinessGate.Evaluate(new ReadinessInput(
            IndependentHeats: 10,
            OutcomeEvents: 5,
            MinorityClassFraction: 0.01,
            FreshnessFactor: 3.5,
            RequiredFieldCompleteness: 0.60));

        Assert.False(report.CanRun);
        Assert.Equal(ReadinessState.Blocked, report.Overall);
        Assert.Contains(report.Dimensions, dimension =>
            dimension.State == ReadinessState.Blocked &&
            dimension.Reason.Contains("Blocked", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(VariableType.Numeric, VariableType.Numeric, false, AnalysisMethod.Spearman)]
    [InlineData(VariableType.Numeric, VariableType.Numeric, true, AnalysisMethod.MutualInformation)]
    [InlineData(VariableType.Binary, VariableType.Numeric, false, AnalysisMethod.PointBiserial)]
    [InlineData(VariableType.Categorical, VariableType.Binary, false, AnalysisMethod.CramersV)]
    public void J08_Method_toolbox_selects_a_shape_appropriate_method(
        VariableType feature,
        VariableType outcome,
        bool nonlinear,
        AnalysisMethod expected)
    {
        var choice = MethodSelector.Select(feature, outcome, numericRelationshipNonlinear: nonlinear);

        Assert.True(choice.IsApplicable);
        Assert.Equal(expected, choice.Method);
        Assert.False(string.IsNullOrWhiteSpace(choice.Rationale));
    }

    [Fact]
    public void J08_Method_toolbox_uses_lasso_vif_for_many_collinear_predictors()
    {
        var choice = MethodSelector.Select(
            VariableType.Numeric,
            VariableType.Numeric,
            manyCollinearPredictors: true);

        Assert.Equal(AnalysisMethod.LassoVif, choice.Method);
        Assert.Contains("VIF", choice.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void J10_Strong_monotonic_signal_is_detected_with_low_p_value()
    {
        var x = Enumerable.Range(1, 80).Select(value => (double)value).ToArray();
        var y = x.Select(value => value * 3.0 + 7.0).ToArray();

        var effect = Stats.Spearman(x, y);
        var pValue = Stats.CorrelationPValue(effect, x.Length);

        Assert.InRange(effect, 0.999999, 1.0);
        Assert.InRange(pValue, 0.0, 0.000001);
    }

    [Fact]
    public void J10_Null_control_remains_near_zero_and_is_not_fabricated_as_a_driver()
    {
        var x = Enumerable.Range(1, 120).Select(value => (double)value).ToArray();
        var y = Enumerable.Range(1, 120)
            .Select(value => value % 2 == 0 ? 1.0 : -1.0)
            .ToArray();

        var effect = Stats.Spearman(x, y);

        Assert.InRange(Math.Abs(effect), 0.0, 0.03);
    }

    [Fact]
    public void J10_Equal_length_is_required_for_reported_correlations()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Stats.Pearson(new[] { 1.0, 2.0 }, new[] { 1.0 }));

        Assert.Contains("equal-length", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
