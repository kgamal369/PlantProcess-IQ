using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Discipline;
using PlantProcess.Analytics.Core.Numerics;
using Xunit;
// T-033: rank by effect size, FDR rejects nulls, stratification non-survival, bootstrap stability.
namespace PlantProcess.Analytics.Core.Tests;
public sealed class P06_DisciplineTests
{
[Fact]
public void Ranking_is_by_effect_size_not_p_value()
{
var findings = new[]
{
new Finding("small_effect_tiny_p", 0.10, 0.0001, "Spearman", 200),
new Finding("big_effect_larger_p", 0.80, 0.0400, "Spearman", 200),
};
var ranked = EffectRanking.RankByEffect(findings);
Assert.Equal("big_effect_larger_p", ranked[0].Id);
}
[Fact]
public void Many_nulls_produce_no_significant_findings_under_fdr()
{
    // Evenly spread null p-values in [0.10, 0.99] -> deterministically none cross BH at q<0.05.
    var nulls = Enumerable.Range(0, 50).Select(i => 0.10 + 0.89 * i / 49.0).ToList();
    var adjusted = BenjaminiHochberg.Adjust(nulls, 0.05);
    Assert.DoesNotContain(adjusted, a => a.Significant);
}

[Fact]
public void A_strong_true_signal_survives_fdr_amongst_nulls_and_q_is_reported()
{
    var p = new List<double> { 1e-8 };
    p.AddRange(Enumerable.Range(0, 40).Select(i => 0.15 + 0.80 * i / 39.0));
    var adjusted = BenjaminiHochberg.Adjust(p, 0.05);
    Assert.True(adjusted[0].Significant);
    Assert.True(adjusted[0].QValue < 0.05);
    Assert.Equal(1, adjusted.Count(a => a.Significant));
}

[Fact]
public void Stratification_flags_non_survival_on_sign_flip()
{
    var strata = new[] { new StratumEffect("grade=A", 0.6, 60), new StratumEffect("grade=B", -0.5, 60) };
    Assert.False(Stratification.Evaluate(0.55, strata).Survives);
}

[Fact]
public void Stratification_confirms_survival_when_consistent()
{
    var strata = new[] { new StratumEffect("grade=A", 0.5, 60), new StratumEffect("grade=B", 0.6, 60) };
    Assert.True(Stratification.Evaluate(0.55, strata).Survives);
}

[Fact]
public void Bootstrap_marks_independent_pair_unstable_and_strong_pair_stable()
{
    var (a, b) = GoldenData.Independent(n: 120, seed: 9001UL);
    var unstable = Bootstrap.Stability(a, b, (xx, yy) => Stats.Spearman(xx, yy), iterations: 600);
    Assert.False(unstable.Stable);

    var (x, y) = GoldenData.MonotonicStrong(n: 150, seed: 9002UL);
    var stable = Bootstrap.Stability(x, y, (xx, yy) => Stats.Spearman(xx, yy), iterations: 600);
    Assert.True(stable.Stable);
    Assert.True(stable.SignConsistency >= 0.95);
}
}