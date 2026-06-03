// PPIQ-GENERATED (T014) - deterministic golden anchor for the canonical Analytics.Core engine (V7 §7.4)
using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Discipline;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using Xunit;

namespace PlantProcess.Phase2.Tests;

public class Phase2_AnalyticsGoldenTests
{
    [Fact]
    public void Canonical_engine_reproduces_the_worked_finding()
    {
        double[] x = { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16 };
        double[] y = { 2,3,3,5,6,6,8,9,9,11,12,12,14,15,15,17 };
        
        // (1) method auto-selection: numeric/numeric -> Spearman
        var choice = MethodSelector.Select(VariableType.Numeric, VariableType.Numeric);
                Assert.True(choice.Method == AnalysisMethod.Spearman);
        
        // (2) strong monotonic relationship is high and significant
        double s = Stats.Spearman(x, y);
                Assert.True(s > 0.9);
        double p = Stats.CorrelationPValue(s, x.Length);
                Assert.True(p < 0.05);
        
        // (3) Benjamini-Hochberg FDR marks the strong finding significant
        var fdr = BenjaminiHochberg.Adjust(new List<double> { p, 0.40, 0.85 }, 0.05);
                Assert.True(fdr.Count == 3);
        var top = fdr.First(z => System.Math.Abs(z.PValue - p) < 1e-12);
                Assert.True(top.Significant);
        
        // (4) bootstrap stable AND byte-identical across runs (determinism, fixed seed)
        System.Func<IReadOnlyList<double>, IReadOnlyList<double>, double> stat = (a, b) => Stats.Spearman(a, b);
        var b1 = Bootstrap.Stability(x, y, stat, iterations: 1000, seed: 20260603UL);
        var b2 = Bootstrap.Stability(x, y, stat, iterations: 1000, seed: 20260603UL);
                Assert.True(b1.Stable);
                Assert.True(b1.SignConsistency >= 0.95);
                Assert.True(b1.Lower > 0 || b1.Upper < 0);
                Assert.True(b1.Lower == b2.Lower && b1.Upper == b2.Upper && b1.SignConsistency == b2.SignConsistency);
        
        // (5) finding survives stratification (sign + magnitude hold across adequate strata)
        var strata = new List<StratumEffect> { new StratumEffect("A", 0.82, 30), new StratumEffect("B", 0.78, 26) };
        var verdict = Stratification.Evaluate(0.80, strata);
                Assert.True(verdict.Survives);
    }
}