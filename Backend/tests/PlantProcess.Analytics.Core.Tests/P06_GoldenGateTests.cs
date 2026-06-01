using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Contracts;
using PlantProcess.Analytics.Core.Discipline;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using PlantProcess.Analytics.Core.Readiness;
using Xunit;
// T-035: end-to-end gate -- recover planted true signals, reject decoys under FDR, correct stability flags.
namespace PlantProcess.Analytics.Core.Tests;
public sealed class P06_GoldenGateTests
{
[Fact]
public void Engine_recovers_true_signals_and_rejects_decoys_with_correct_discipline()
{
// --- True monotonic driver: recovered live by Spearman, passes FDR ---
var (xMono, yMono) = GoldenData.MonotonicStrong(n: 250, seed: 7001UL);
double rhoTrue = Stats.Spearman(xMono, yMono);
double pTrue = Stats.CorrelationPValue(rhoTrue, xMono.Length);
Assert.True(rhoTrue > 0.9);
// --- True nonlinear driver: missed by Spearman, recovered live by MI ---
    var (xNl, yNl) = GoldenData.NonlinearSymmetric(n: 600, seed: 7002UL);
    Assert.True(Math.Abs(Stats.Spearman(xNl, yNl)) < 0.25);
    double nmiNl = MutualInformation.NormalizedNumeric(xNl, yNl);
    Assert.True(nmiNl > 0.40);

    // --- Decoys: genuinely null (loose live bound) ---
    for (ulong s = 0; s < 8; s++)
    {
        var (da, db) = GoldenData.Independent(n: 250, seed: 7100UL + s);
        Assert.True(Math.Abs(Stats.Spearman(da, db)) < 0.35, "A decoy is genuinely uncorrelated with the outcome.");
    }

    // --- FDR discipline: true signal vs decoy null p-values -> only the true one survives ---
    var pValues = new List<double> { pTrue };
    pValues.AddRange(Enumerable.Range(0, 12).Select(i => 0.20 + 0.75 * i / 11.0)); // decoy null distribution
    var fdr = BenjaminiHochberg.Adjust(pValues, 0.05);
    Assert.True(fdr[0].Significant, "The planted true signal must survive FDR.");
    Assert.Equal(1, fdr.Count(f => f.Significant));   // every decoy rejected
    Assert.True(fdr[0].QValue < 0.05);

    // --- Lasso screen: informative kept, decoys dropped ---
    var (lx, ly, _) = GoldenData.LassoDesign(n: 300, noiseCols: 8, seed: 7003UL);
    var lasso = Lasso.Fit(lx, ly, lambda: 0.7);
    Assert.Contains(0, lasso.SelectedFeatures);
    Assert.Contains(1, lasso.SelectedFeatures);
    Assert.True(lasso.SelectedFeatures.All(j => j < 2), "Lasso must drop all decoy/noise features.");

    // --- VIF: planted collinear pair flagged ---
    var vif = VarianceInflation.Compute(GoldenData.CollinearDesign(n: 200, seed: 7004UL), 5.0);
    Assert.Contains(0, vif.Flagged);
    Assert.Contains(2, vif.Flagged);
    Assert.DoesNotContain(1, vif.Flagged);

    // --- Stability: true signal stable, decoy unstable ---
    var stableTrue = Bootstrap.Stability(xMono, yMono, (a, b) => Stats.Spearman(a, b), iterations: 600, seed: 7005UL);
    Assert.True(stableTrue.Stable);
    var (na, nb) = GoldenData.Independent(n: 250, seed: 7006UL);
    var unstableDecoy = Bootstrap.Stability(na, nb, (a, b) => Stats.Spearman(a, b), iterations: 600, seed: 7007UL);
    Assert.False(unstableDecoy.Stable);
}

[Fact]
public void Advanced_result_contract_blocks_render_without_method_samplesize_or_caveat()
{
    var ok = new AdvancedAnalysisResult("f1", AnalysisMethod.Spearman, 0.7, 0.001, 250,
        new[] { "grade=DX51D" }, ReadinessState.Ready, 0.99, 0.55, 0.82, 12,
        new[] { "3% of heats excluded for missing chemistry" }, true, AdvancedAnalysisResult.DefaultCaveat);
    Assert.True(ok.IsRenderable);

    var noMethod = ok with { Method = AnalysisMethod.NotApplicable };
    var noSample = ok with { SampleSize = 0 };
    var noCaveat = ok with { HonestyCaveat = "" };
    Assert.False(noMethod.IsRenderable);
    Assert.False(noSample.IsRenderable);
    Assert.False(noCaveat.IsRenderable);
}
}