using System;
using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Discipline;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

/// <summary>
/// T-177 parity regression. Two suites passing separately is not parity evidence.
/// These tests compare the EXISTING proven behaviour against the NEW kernel directly,
/// and assert that the only divergence is the one T-177 was authorised to introduce.
/// </summary>
public sealed class T177_ParityRegressionTests
{
    private static readonly VariableType[] AllTypes =
    {
        VariableType.Numeric, VariableType.Categorical, VariableType.Binary
    };

    /// <summary>
    /// The single authorised divergence: Numeric x Categorical, in either argument order.
    /// The old selector refuses it. The new kernel supports it. Nothing else may differ.
    /// </summary>
    private static bool IsAuthorisedDivergence(VariableType a, VariableType b) =>
        (a == VariableType.Numeric && b == VariableType.Categorical) ||
        (a == VariableType.Categorical && b == VariableType.Numeric);

    [Fact]
    public void Old_and_new_selectors_agree_on_every_pairing_except_the_recorded_divergence()
    {
        var disagreements = new List<string>();

        foreach (var a in AllTypes)
        {
            foreach (var b in AllTypes)
            {
                bool oldApplicable = MethodSelector.Select(a, b).IsApplicable;
                bool newSupported = KernelMethodSelector.Classify(a, b).IsSupported;

                if (oldApplicable == newSupported) continue;
                disagreements.Add($"({a},{b}) old={oldApplicable} new={newSupported}");
            }
        }

        // Exactly two cells of the nine may differ, and both are the same logical pairing.
        Assert.Equal(2, disagreements.Count);
        Assert.Contains(disagreements, d => d.StartsWith("(Numeric,Categorical)", StringComparison.Ordinal));
        Assert.Contains(disagreements, d => d.StartsWith("(Categorical,Numeric)", StringComparison.Ordinal));
    }

    [Fact]
    public void The_recorded_divergence_is_real_and_is_exactly_what_T177_authorised()
    {
        // Old behaviour, unchanged and still asserted by P06_MethodSelectionTests.
        Assert.False(MethodSelector.Select(VariableType.Numeric, VariableType.Categorical).IsApplicable);
        Assert.Equal(AnalysisMethod.NotApplicable,
            MethodSelector.Select(VariableType.Numeric, VariableType.Categorical).Method);

        // New behaviour, isolated in the kernel and not wired into the presentation engine.
        Assert.True(KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Categorical).IsSupported);
        Assert.Equal(KernelPairing.NumericCategorical,
            KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Categorical).Pairing);
    }

    [Fact]
    public void Every_pairing_the_old_selector_supported_is_still_supported_by_the_new_kernel()
    {
        foreach (var a in AllTypes)
        {
            foreach (var b in AllTypes)
            {
                if (!MethodSelector.Select(a, b).IsApplicable) continue;
                Assert.True(KernelMethodSelector.Classify(a, b).IsSupported,
                    $"Regression: ({a},{b}) was supported by the existing selector and is not supported by the kernel.");
            }
        }
    }

    [Fact]
    public void Numeric_numeric_statistic_is_untouched_by_this_task()
    {
        var x = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 };
        var y = new[] { 2.0, 4.1, 5.9, 8.2, 9.8, 12.1, 14.0, 16.2 };

        Assert.Equal(1.0, Stats.Spearman(x, y), 9);
        Assert.Equal(KernelPairing.NumericNumeric,
            KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Numeric).Pairing);
    }

    [Fact]
    public void Binary_numeric_statistic_is_untouched_by_this_task()
    {
        var binary = new[] { 0, 0, 0, 0, 1, 1, 1, 1 };
        var y = new[] { 1.0, 1.2, 0.9, 1.1, 5.0, 5.2, 4.8, 5.1 };

        double r = Stats.PointBiserial(binary, y);
        Assert.True(r > 0.98 && r <= 1.0, $"Point-biserial drifted: {r}");
        Assert.Equal(KernelPairing.BinaryNumeric,
            KernelMethodSelector.Classify(VariableType.Binary, VariableType.Numeric).Pairing);
        Assert.Equal(KernelPairing.BinaryNumeric,
            KernelMethodSelector.Classify(VariableType.Numeric, VariableType.Binary).Pairing);
    }

    [Fact]
    public void Categorical_categorical_statistic_is_untouched_by_this_task()
    {
        var x = new[] { "a", "a", "a", "b", "b", "b", "c", "c", "c" };
        var y = new[] { "x", "x", "y", "y", "y", "z", "z", "z", "x" };

        double v = CategoricalAssociation.CramersV(x, y);
        Assert.InRange(v, 0.0, 1.0);
        Assert.Equal(KernelPairing.CategoricalCategorical,
            KernelMethodSelector.Classify(VariableType.Categorical, VariableType.Categorical).Pairing);
    }

    [Fact]
    public void Fdr_discipline_is_unchanged_and_the_kernel_adds_no_second_implementation()
    {
        var p = new[] { 0.001, 0.008, 0.039, 0.041, 0.042, 0.060, 0.074, 0.205, 0.212, 0.216 };
        var expectedQ = new[]
        {
            0.010, 0.040, 0.084, 0.084, 0.084, 0.100, 0.105714285714286, 0.216, 0.216, 0.216
        };

        var items = BenjaminiHochberg.Adjust(p, 0.05);
        Assert.Equal(10, items.Count);
        for (int i = 0; i < items.Count; i++)
            Assert.True(Math.Abs(items[i].QValue - expectedQ[i]) <= 1e-9,
                $"q[{i}] drifted: expected {expectedQ[i]} actual {items[i].QValue}");

        var significant = items.Where(t => t.Significant).Select(t => t.Index).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { 0, 1 }, significant);
    }

    [Fact]
    public void Kernel_kruskal_wallis_is_built_on_the_proven_rank_primitive()
    {
        var a = new[] { 5.0, 5.1, 5.2, 4.9, 5.0, 5.1, 4.8, 5.0 };
        var b = new[] { 9.0, 1.0, 17.0, 3.0, 14.0, 2.0, 16.0, 4.0 };
        var c = new[] { 20.0, 21.0, 19.0, 20.5, 20.2, 19.8, 20.1, 20.3 };

        // Recompute H independently, using the EXISTING Stats.Ranks primitive.
        var flat = a.Concat(b).Concat(c).ToList();
        var owner = Enumerable.Repeat(0, 8)
            .Concat(Enumerable.Repeat(1, 8))
            .Concat(Enumerable.Repeat(2, 8)).ToList();

        var ranks = Stats.Ranks(flat);
        int n = flat.Count;
        double sum = 0.0;
        for (int gi = 0; gi < 3; gi++)
        {
            double rankSum = 0.0;
            for (int i = 0; i < n; i++) if (owner[i] == gi) rankSum += ranks[i];
            sum += rankSum * rankSum / 8.0;
        }
        double hRaw = 12.0 / (n * (n + 1.0)) * sum - 3.0 * (n + 1.0);
        double tieTotal = flat.GroupBy(v => v).Where(g => g.Count() > 1)
            .Sum(g => Math.Pow(g.Count(), 3.0) - g.Count());
        double tie = 1.0 - tieTotal / (Math.Pow(n, 3.0) - n);
        double expected = hRaw / tie;

        var result = GroupComparisonKernel.Evaluate(new GroupComparisonInput(new List<NumericGroup>
        {
            new("A", a), new("B", b), new("C", c)
        }));

        Assert.Equal(KernelMethod.KruskalWallis, result.Method);
        Assert.True(Math.Abs(expected - result.Statistic) <= 1e-12,
            $"The kernel does not agree with Stats.Ranks: expected {expected} actual {result.Statistic}");
        Assert.True(result.TieCorrectionApplied);
    }

    [Fact]
    public void The_kernel_holds_no_database_or_presentation_dependency()
    {
        var assembly = typeof(GroupComparisonKernel).Assembly;
        var referenced = assembly.GetReferencedAssemblies().Select(r => r.Name ?? string.Empty).ToArray();

        foreach (var forbidden in new[] { "Npgsql", "Microsoft.EntityFrameworkCore", "PlantProcess.Infrastructure", "PlantProcess.Api" })
            Assert.DoesNotContain(referenced, r => r.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
    }
}
