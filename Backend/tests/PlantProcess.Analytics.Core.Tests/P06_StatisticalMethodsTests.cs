using System.Linq;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using Xunit;
// T-031: methods recover KNOWN structure on synthetic data.
namespace PlantProcess.Analytics.Core.Tests;
public sealed class P06_StatisticalMethodsTests
{
[Fact]
public void Spearman_recovers_planted_monotonic_relationship()
{
var (x, y) = GoldenData.MonotonicStrong();
Assert.True(Stats.Spearman(x, y) > 0.9, "Spearman should recover the strong monotonic relationship.");
}
[Fact]
public void Mutual_information_detects_nonlinearity_that_spearman_misses()
{
    var (x, y) = GoldenData.NonlinearSymmetric();
    var (a, b) = GoldenData.Independent();
    double spearman = Math.Abs(Stats.Spearman(x, y));
    double nmiSignal = MutualInformation.NormalizedNumeric(x, y);
    double nmiNoise = MutualInformation.NormalizedNumeric(a, b);
    Assert.True(spearman < 0.25, $"Spearman should miss the symmetric nonlinear relationship (was {spearman:F3}).");
    Assert.True(nmiSignal > 0.40, $"MI should detect the nonlinear dependence (NMI was {nmiSignal:F3}).");
    Assert.True(nmiSignal > nmiNoise + 0.20, "MI on the signal should clearly exceed MI on independent noise.");
}

[Fact]
public void Lasso_drops_noise_features_and_keeps_informative()
{
    var (x, y, _) = GoldenData.LassoDesign();
    var result = Lasso.Fit(x, y, lambda: 0.7);
    Assert.Contains(0, result.SelectedFeatures);
    Assert.Contains(1, result.SelectedFeatures);
    Assert.True(result.SelectedFeatures.All(j => j < 2), "No noise feature (index >= 2) should be selected.");
}

[Fact]
public void Vif_flags_planted_collinear_pair_only()
{
    var x = GoldenData.CollinearDesign();
    var vif = VarianceInflation.Compute(x, threshold: 5.0);
    Assert.True(vif.Vif[0] > 5.0, "Collinear feature 0 should have high VIF.");
    Assert.True(vif.Vif[2] > 5.0, "Collinear feature 2 should have high VIF.");
    Assert.True(vif.Vif[1] < 5.0, "Independent feature 1 should have low VIF.");
}

[Fact]
public void Cramers_v_and_point_biserial_are_bounded_and_sensible()
{
    var cat1 = new[] { "A", "A", "B", "B", "A", "B", "A", "B" };
    var cat2 = new[] { "X", "X", "Y", "Y", "X", "Y", "X", "Y" }; // perfectly associated
    Assert.True(CategoricalAssociation.CramersV(cat1, cat2) > 0.9);

    var bin = new[] { 0, 0, 0, 1, 1, 1 };
    var num = new[] { 1.0, 2.0, 3.0, 7.0, 8.0, 9.0 }; // clear separation
    Assert.True(Stats.PointBiserial(bin, num) > 0.9);
}
}