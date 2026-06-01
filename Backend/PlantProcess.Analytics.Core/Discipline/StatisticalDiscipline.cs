using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Common;
namespace PlantProcess.Analytics.Core.Discipline;
public sealed record Finding(string Id, double EffectSize, double PValue, string Method, int SampleSize);
public sealed record FdrItem(int Index, double PValue, double QValue, bool Significant);
public sealed record StratumEffect(string Stratum, double EffectSize, int SampleSize);
public sealed record StratificationVerdict(bool Survives, IReadOnlyList<StratumEffect> Strata, string Reason);
public sealed record BootstrapResult(double PointEstimate, double Lower, double Upper, double SignConsistency, bool Stable);
public static class EffectRanking
{
/// <summary>Rank by effect size (never by p-value); p-value is only a tie-breaker.</summary>
public static IReadOnlyList<Finding> RankByEffect(IEnumerable<Finding> findings) =>
findings.OrderByDescending(f => Math.Abs(f.EffectSize)).ThenBy(f => f.PValue).ToList();
}
public static class BenjaminiHochberg
{
/// <summary>BH-FDR adjusted q-values and significance at level q.</summary>
public static IReadOnlyList<FdrItem> Adjust(IReadOnlyList<double> pValues, double q = 0.05)
{
int m = pValues.Count;
if (m == 0) return Array.Empty<FdrItem>();
var order = Enumerable.Range(0, m).OrderBy(i => pValues[i]).ToArray();
var qval = new double[m];
double running = 1.0;
for (int k = m - 1; k >= 0; k--)
{
int idx = order[k];
double adj = pValues[idx] * m / (k + 1);
running = Math.Min(running, adj);
qval[idx] = Math.Min(1.0, running);
}
int maxK = -1;
for (int k = 0; k < m; k++) if (pValues[order[k]] <= q * (k + 1) / m) maxK = k;
var sig = new HashSet<int>();
for (int k = 0; k <= maxK; k++) sig.Add(order[k]);
var result = new List<FdrItem>(m);
for (int i = 0; i < m; i++) result.Add(new FdrItem(i, pValues[i], qval[i], sig.Contains(i)));
return result;
}
}
public static class Stratification
{
public static StratificationVerdict Evaluate(double overallEffect, IReadOnlyList<StratumEffect> strata, double minStratumSize = 20, double minEffect = 0.2)
{
var adequate = strata.Where(s => s.SampleSize >= minStratumSize).ToList();
if (adequate.Count == 0) return new(false, strata, "No stratum has adequate sample size to confirm the finding.");
int sign = Math.Sign(overallEffect);
bool consistent = adequate.All(s => Math.Sign(s.EffectSize) == sign && Math.Abs(s.EffectSize) >= minEffect);
return consistent
? new(true, strata, "Finding retains sign and magnitude across all adequately-sized strata.")
: new(false, strata, "Does not survive: sign flips or magnitude collapses in at least one adequately-sized stratum.");
}
}
public static class Bootstrap
{
/// <summary>Resampling stability: deterministic seed, percentile CI, sign-consistency, stable iff CI excludes 0 and sign is consistent.</summary>
public static BootstrapResult Stability(
IReadOnlyList<double> x, IReadOnlyList<double> y,
Func<IReadOnlyList<double>, IReadOnlyList<double>, double> statistic,
int iterations = 1000, double ciLevel = 0.95, double stabilityThreshold = 0.95, ulong seed = 20260602UL)
{
int n = x.Count;
double point = statistic(x, y);
var rng = new DeterministicRandom(seed);
var samples = new double[iterations];
int baseSign = Math.Sign(point), match = 0;
for (int b = 0; b < iterations; b++)
{
var xs = new double[n]; var ys = new double[n];
for (int i = 0; i < n; i++) { int idx = rng.NextInt(n); xs[i] = x[idx]; ys[i] = y[idx]; }
double s = statistic(xs, ys);
samples[b] = s;
if (baseSign != 0 && Math.Sign(s) == baseSign) match++;
}
Array.Sort(samples);
double alpha = (1.0 - ciLevel) / 2.0;
double lower = Percentile(samples, alpha), upper = Percentile(samples, 1.0 - alpha);
double consistency = (double)match / iterations;
bool stable = consistency >= stabilityThreshold && (lower > 0 || upper < 0);
return new(point, lower, upper, consistency, stable);
}
private static double Percentile(double[] sorted, double q)
{
    if (q <= 0) return sorted[0];
    if (q >= 1) return sorted[^1];
    double pos = q * (sorted.Length - 1);
    int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
    return sorted[lo] + (pos - lo) * (sorted[hi] - sorted[lo]);
}
}