using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Analytics.Core.Methods;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

/// <summary>
/// T-177 known-answer and falsification tests for the Numeric x Categorical kernel.
/// Expected values were computed by an independent reference implementation and then
/// re-derived from first principles by hand. They were never produced by this kernel.
/// Fixture pack: Documentation/T-177/t177_known_answer_fixtures.json (v2).
/// </summary>
public sealed class T177_StatisticalKernelTests
{
    private const double Tol = 1e-9;

    private static GroupComparisonInput Input(params (string Key, double[] Values)[] groups) =>
        new(groups.Select(g => new NumericGroup(g.Key, g.Values)).ToList());

    private static void Close(double expected, double actual, double tol = Tol)
    {
        double delta = System.Math.Abs(expected - actual);
        double rel = delta / System.Math.Max(1e-300, System.Math.Abs(expected));
        Assert.True(delta <= tol || rel <= tol,
            $"expected {expected:E15} actual {actual:E15} absolute {delta:E3} relative {rel:E3}");
    }

    // ---------------- F-01 ANOVA known answer ----------------

    [Fact]
    public void F01_anova_known_answer_matches_reference()
    {
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 10.1, 10.4, 9.8, 10.2, 10.5, 9.9, 10.3, 10.0 }),
            ("B", new[] { 12.2, 12.5, 11.9, 12.1, 12.4, 12.0, 12.3, 12.6 }),
            ("C", new[] { 14.0, 14.3, 13.8, 14.1, 14.4, 13.9, 14.2, 14.5 })));

        Assert.Equal(TerminalState.Finding, r.TerminalState);
        Assert.Equal(KernelMethod.Anova, r.Method);
        Assert.Equal(24, r.AlignedPopulation);
        Assert.Equal(new[] { 8, 8, 8 }, r.GroupSizes);
        Assert.Equal(2, r.DegreesOfFreedom1);
        Assert.Equal(21, r.DegreesOfFreedom2);
        Close(533.77777777777777, r.Statistic);
        Close(9.9167943998172654e-19, r.PValue);
        Assert.Equal("EtaSquared", r.EffectSizeMeasure);
        Close(0.98070838003877217, r.EffectSize);
        Assert.False(r.TieCorrectionApplied);
    }

    // ---------------- F-02 falsification: variance heterogeneity ----------------

    [Fact]
    public void F02_variance_heterogeneity_must_not_report_anova()
    {
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 5.0, 5.1, 5.2, 4.9, 5.0, 5.1, 4.8, 5.0 }),
            ("B", new[] { 9.0, 1.0, 17.0, 3.0, 14.0, 2.0, 16.0, 4.0 }),
            ("C", new[] { 20.0, 21.0, 19.0, 20.5, 20.2, 19.8, 20.1, 20.3 })));

        Assert.NotEqual(KernelMethod.Anova, r.Method);
        Assert.Equal(KernelMethod.KruskalWallis, r.Method);
        Assert.Equal(TerminalState.Finding, r.TerminalState);
        Close(15.393464052287582, r.Statistic);
        Assert.Equal(2, r.DegreesOfFreedom1);
        Close(4.5430943093664556e-4, r.PValue);
        Assert.Equal("EpsilonSquared", r.EffectSizeMeasure);
        Close(0.63778400249464674, r.EffectSize);
        Assert.True(r.TieCorrectionApplied);
        Assert.NotNull(r.Assumptions);
        Assert.False(r.Assumptions!.ParametricAssumptionsSupported);
    }

    [Fact]
    public void F02_tie_correction_changes_the_statistic()
    {
        // H_raw is 15.36 for this fixture. The tie-corrected value must differ.
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 5.0, 5.1, 5.2, 4.9, 5.0, 5.1, 4.8, 5.0 }),
            ("B", new[] { 9.0, 1.0, 17.0, 3.0, 14.0, 2.0, 16.0, 4.0 }),
            ("C", new[] { 20.0, 21.0, 19.0, 20.5, 20.2, 19.8, 20.1, 20.3 })));

        Assert.True(r.Statistic > 15.36,
            "A tie-blind Kruskal-Wallis returns 15.36 and understates the statistic.");
    }

    // ---------------- F-03 falsification: severe skew ----------------

    [Fact]
    public void F03_severe_skew_must_not_report_anova()
    {
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 1.0, 1.1, 1.2, 1.0, 1.1, 1.0, 1.2, 60.0 }),
            ("B", new[] { 2.0, 2.1, 2.2, 2.0, 2.1, 2.0, 2.2, 80.0 }),
            ("C", new[] { 3.0, 3.1, 3.2, 3.0, 3.1, 3.0, 3.2, 95.0 })));

        Assert.NotEqual(KernelMethod.Anova, r.Method);
        Assert.Equal(KernelMethod.KruskalWallis, r.Method);
        Close(12.598597721297107, r.Statistic);
        Close(1.837592734035238e-3, r.PValue);

        // F-03 falsifies a DIFFERENT assumption than F-02. Here the variances are
        // homogeneous (Levene p = 0.955, ratio 2.43) and it is the skew of 2.83
        // that rejects the parametric path. Two fixtures, two distinct triggers.
        Assert.NotNull(r.Assumptions);
        Assert.True(r.Assumptions!.GroupSkewness.All(sk => System.Math.Abs(sk) > 2.0),
            "F-03 must fall back because of skew, not because of variance heterogeneity.");
        Assert.True(r.Assumptions!.VarianceRatio < GroupComparisonKernel.VarianceRatioCeiling,
            "F-03 variances are homogeneous; the fallback must not be attributed to variance.");
    }

    // ---------------- F-04..F-07 THE EXCLUSION TAXONOMY ----------------

    [Fact]
    public void F04_constant_zero_variance_is_attributed_to_the_data()
    {
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 7.0, 7.0, 7.0, 7.0, 7.0, 7.0, 7.0, 7.0 }),
            ("B", new[] { 7.0, 7.0, 7.0, 7.0, 7.0, 7.0, 7.0, 7.0 }),
            ("C", new[] { 7.0, 7.0, 7.0, 7.0, 7.0, 7.0, 7.0, 7.0 })));

        Assert.Equal(TerminalState.NotApplicable, r.TerminalState);
        Assert.Equal(StatisticalExclusionReason.ConstantZeroVariance, r.ExclusionReason);
        Assert.Equal(ExclusionAttribution.Data, r.Attribution);
        Assert.Contains("constant", r.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void F05_unsupported_pairing_is_attributed_to_the_method_never_the_data()
    {
        var c = KernelMethodSelector.Classify((VariableType)99, (VariableType)98);

        Assert.False(c.IsSupported);
        Assert.Equal(StatisticalExclusionReason.UnsupportedMethodPairing, c.ExclusionReason);
        Assert.Equal(ExclusionAttribution.Method, c.Attribution);

        // The reason must name the method limitation and must never blame the data.
        Assert.Contains("method", c.Rationale, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zero variance", c.Rationale, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("constant", c.Rationale, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("insufficient", c.Rationale, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void F06_insufficient_groups_reports_the_measured_count()
    {
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 1.0, 2.0, 3.0, 4.0 })));

        Assert.Equal(TerminalState.InsufficientData, r.TerminalState);
        Assert.Equal(StatisticalExclusionReason.InsufficientGroups, r.ExclusionReason);
        Assert.Equal(ExclusionAttribution.Data, r.Attribution);
        Assert.Contains("1", r.Reason);
    }

    [Fact]
    public void F07_insufficient_sample_reports_the_smallest_group()
    {
        var r = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }),
            ("B", new[] { 9.0 })));

        Assert.Equal(TerminalState.InsufficientData, r.TerminalState);
        Assert.Equal(StatisticalExclusionReason.InsufficientSample, r.ExclusionReason);
        Assert.Equal(ExclusionAttribution.Data, r.Attribution);
    }

    [Fact]
    public void Exclusion_taxonomy_never_collapses_four_causes_into_one_reason()
    {
        var zeroVariance = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 7.0, 7.0, 7.0 }), ("B", new[] { 7.0, 7.0, 7.0 })));
        var fewGroups = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 1.0, 2.0, 3.0 })));
        var smallGroup = GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 1.0, 2.0, 3.0 }), ("B", new[] { 9.0 })));
        var unsupported = KernelMethodSelector.Classify((VariableType)99, (VariableType)98);

        var reasons = new HashSet<StatisticalExclusionReason>
        {
            zeroVariance.ExclusionReason,
            fewGroups.ExclusionReason,
            smallGroup.ExclusionReason,
            unsupported.ExclusionReason
        };

        Assert.Equal(4, reasons.Count);
        Assert.NotEqual(zeroVariance.Reason, fewGroups.Reason);
        Assert.NotEqual(fewGroups.Reason, smallGroup.Reason);
    }

    // ---------------- F-09 determinism ----------------

    [Fact]
    public void F09_repeated_evaluation_is_deterministic()
    {
        GroupComparisonResult Run() => GroupComparisonKernel.Evaluate(Input(
            ("A", new[] { 10.1, 10.4, 9.8, 10.2, 10.5, 9.9, 10.3, 10.0 }),
            ("B", new[] { 12.2, 12.5, 11.9, 12.1, 12.4, 12.0, 12.3, 12.6 }),
            ("C", new[] { 14.0, 14.3, 13.8, 14.1, 14.4, 13.9, 14.2, 14.5 })));

        var a = Run();
        var b = Run();

        Assert.Equal(a.Method, b.Method);
        Assert.Equal(a.TerminalState, b.TerminalState);
        Assert.Equal(a.ExclusionReason, b.ExclusionReason);
        Assert.Equal(a.Statistic, b.Statistic);
        Assert.Equal(a.PValue, b.PValue);
        Assert.Equal(a.EffectSize, b.EffectSize);
        Assert.Equal(a.GroupKeys, b.GroupKeys);
    }

    // ---------------- Pairing classification ----------------

    [Fact]
    public void Numeric_categorical_is_now_a_supported_pairing()
    {
        var c = KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Categorical);
        Assert.True(c.IsSupported);
        Assert.Equal(KernelPairing.NumericCategorical, c.Pairing);

        var reversed = KernelMethodSelector.Classify(VariableType.Categorical, VariableType.Numeric);
        Assert.True(reversed.IsSupported);
        Assert.Equal(KernelPairing.NumericCategorical, reversed.Pairing);
    }

    [Fact]
    public void Existing_pairings_are_still_classified_unchanged()
    {
        Assert.Equal(KernelPairing.NumericNumeric,
            KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Numeric).Pairing);
        Assert.Equal(KernelPairing.BinaryNumeric,
            KernelMethodSelector.Classify(VariableType.Binary, VariableType.Numeric).Pairing);
        Assert.Equal(KernelPairing.BinaryNumeric,
            KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Binary).Pairing);
        Assert.Equal(KernelPairing.CategoricalCategorical,
            KernelMethodSelector.Classify(VariableType.Categorical, VariableType.Categorical).Pairing);
        Assert.Equal(KernelPairing.CategoricalCategorical,
            KernelMethodSelector.Classify(VariableType.Binary, VariableType.Binary).Pairing);
    }

    // ---------------- Special function accuracy ----------------

    [Fact]
    public void Special_functions_match_the_independent_reference()
    {
        Close(0.11903956265831, SpecialFunctions.FDistributionSurvival(2.5, 3, 10), 1e-11);
        Close(0.5, SpecialFunctions.FDistributionSurvival(1.0, 1, 1), 1e-11);
        Close(0.47950012218695, SpecialFunctions.ChiSquareSurvival(0.5, 1), 1e-11);
        Close(2.66908342490437e-7, SpecialFunctions.ChiSquareSurvival(50.0, 10), 1e-11);
    }
}
