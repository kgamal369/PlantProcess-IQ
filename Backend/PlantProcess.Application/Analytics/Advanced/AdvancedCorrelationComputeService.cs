using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlantProcess.Analytics.Core.Common;
using PlantProcess.Analytics.Core.Contracts;
using PlantProcess.Analytics.Core.Discipline;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using PlantProcess.Analytics.Core.Readiness;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// P3-01..P3-05. Orchestrates the deterministic PlantProcess.Analytics.Core engine over
/// loaded feature/outcome vectors: readiness gate -> method auto-selection + effect sizes
/// -> iterative VIF collinearity screen -> effect ranking + Benjamini-Hochberg FDR ->
/// bootstrap stability + stratification -> persist. Every number carries a provenance
/// handle and the mandatory honesty caveat. Deterministic (fixed seed).
/// </summary>
public sealed class AdvancedCorrelationComputeService : IAdvancedCorrelationService
{
    public const string EngineKey = "dotnet-analytics-core-v1";
    private const int MinPairs = 8;
    private const ulong Seed = 20260603UL;

    private readonly IFeatureVectorLoader _loader;
    private readonly IAdvancedResultWriter _writer;
    private readonly ILogger<AdvancedCorrelationComputeService> _logger;

    public AdvancedCorrelationComputeService(
        IFeatureVectorLoader loader,
        IAdvancedResultWriter writer,
        ILogger<AdvancedCorrelationComputeService> logger)
    {
        _loader = loader;
        _writer = writer;
        _logger = logger;
    }

    private sealed record FeatureCompute(
        string FeatureKey,
        VariableType Type,
        AnalysisMethod Method,
        string Rationale,
        double Effect,
        double PValue,
        int SampleSize,
        double[] X,
        double[] Y,
        string[] Keys,
        Func<IReadOnlyList<double>, IReadOnlyList<double>, double> Stat,
        int Dropped);

    public async Task<AdvancedAnalysisRunResult> ComputeAsync(AdvancedAnalysisRequest request, CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var ds = await _loader.LoadAsync(request, ct);

        // ---- P3-01: readiness gate ----
        var readiness = ReadinessGate.Evaluate(new ReadinessInput(
            ds.IndependentHeats,
            ds.Outcomes.Count,
            MinorityFraction(ds),
            ds.FreshnessFactor,
            ds.RequiredFieldCompleteness));
        var reasons = readiness.Dimensions.Select(d => $"{d.Name}: {d.Reason}").ToList();

        if (!readiness.CanRun)
        {
            var blocked = new AdvancedAnalysisRunResult(runId, request.OutcomeKey, ds.OutcomeType, request.Grain,
                request.WindowDays, readiness.Overall, reasons, false,
                Array.Empty<AdvancedFinding>(), Array.Empty<ExcludedFeature>(), EngineKey,
                "Blocked by the data-readiness gate; analysis refused (honest abstain).");
            await _writer.WriteAsync(request, blocked, ct);
            _logger.LogInformation("Advanced analysis {RunId} BLOCKED for outcome {Outcome}.", runId, request.OutcomeKey);
            return blocked;
        }

        var repr = BuildOutcomeRepr(ds);
        var excluded = new List<ExcludedFeature>();
        var computed = new List<FeatureCompute>();

        // ---- P3-02: per-feature method dispatch + effect size ----
        foreach (var fs in ds.Features)
        {
            var (xs, ys, keys, dropped) = Align(fs, repr);
            int n = xs.Length;
            if (n < MinPairs)
            {
                excluded.Add(new ExcludedFeature(fs.FeatureKey, $"Insufficient paired samples (n={n} < {MinPairs})."));
                continue;
            }

            var choice = ChooseMethod(fs.Type, ds.OutcomeType, xs, ys);
            var (eff, p, stat) = Measure(choice.Method, xs, ys, request.PermutationIterations);
            if (double.IsNaN(eff))
            {
                excluded.Add(new ExcludedFeature(fs.FeatureKey, "Undefined statistic (constant / zero-variance input)."));
                continue;
            }

            computed.Add(new FeatureCompute(fs.FeatureKey, fs.Type, choice.Method, choice.Rationale,
                eff, p, n, xs, ys, keys, stat, dropped));
        }

        // ---- P3-03: iterative VIF collinearity screen (+ optional Lasso note) ----
        IterativeVifExclude(computed, request.VifThreshold, excluded);
        var kept = computed.Where(c => excluded.All(e => e.FeatureKey != c.FeatureKey)).ToList();
        var lassoNote = MaybeLasso(kept);

        // ---- P3-04: rank by EFFECT, then Benjamini-Hochberg FDR ----
        var findings = kept.Select(c => new Finding(c.FeatureKey, c.Effect, c.PValue, c.Method.ToString(), c.SampleSize)).ToList();
        var ranked = EffectRanking.RankByEffect(findings);
        var fdr = BenjaminiHochberg.Adjust(ranked.Select(f => f.PValue).ToList(), request.FdrQ);
        var byKey = kept.ToDictionary(c => c.FeatureKey);

        var results = new List<AdvancedFinding>(ranked.Count);
        for (int i = 0; i < ranked.Count; i++)
        {
            var c = byKey[ranked[i].Id];
            var q = fdr[i];
            var boot = Bootstrap.Stability(c.X, c.Y, c.Stat, request.BootstrapIterations, seed: Seed);
            var (survives, sreason) = EvaluateStrata(c, ds);

            results.Add(new AdvancedFinding(
                c.FeatureKey, c.Method, c.Rationale, c.Effect, q.PValue, q.QValue, q.Significant, c.SampleSize,
                boot.SignConsistency, boot.Lower, boot.Upper, boot.Stable,
                survives, sreason, c.Dropped,
                $"finding:{runId:N}:{c.FeatureKey}",
                AdvancedAnalysisResult.DefaultCaveat));
        }

        var result = new AdvancedAnalysisRunResult(runId, request.OutcomeKey, ds.OutcomeType, request.Grain,
            request.WindowDays, readiness.Overall, reasons, true, results, excluded, EngineKey,
            $"Computed {results.Count} finding(s); {excluded.Count} excluded. {lassoNote}".Trim());

        // ---- P3-05: persist ----
        await _writer.WriteAsync(request, result, ct);
        _logger.LogInformation("Advanced analysis {RunId}: outcome={Outcome} findings={F} excluded={E} readiness={R}",
            runId, request.OutcomeKey, results.Count, excluded.Count, readiness.Overall);
        return result;
    }

    // ----------------------------------------------------------------- helpers

    private static double MinorityFraction(AdvancedDataset ds)
    {
        if (ds.Outcomes.Count == 0) return 0.0;
        if (ds.OutcomeType == VariableType.Binary)
        {
            var g = ds.Outcomes.GroupBy(o => o.Value >= 0.5 ? 1 : 0).Select(x => x.Count()).ToList();
            return g.Count < 2 ? 0.0 : g.Min() / (double)ds.Outcomes.Count;
        }
        if (ds.OutcomeType == VariableType.Categorical)
        {
            var g = ds.Outcomes.GroupBy(o => o.Category ?? "").Select(x => x.Count()).ToList();
            return g.Count < 2 ? 0.0 : g.Min() / (double)ds.Outcomes.Count;
        }
        return 0.5; // class balance not applicable to a continuous outcome
    }

    private static IReadOnlyDictionary<string, double> BuildOutcomeRepr(AdvancedDataset ds)
    {
        var map = new Dictionary<string, double>();
        if (ds.OutcomeType == VariableType.Categorical)
        {
            var codes = CodeMap(ds.Outcomes.Select(o => o.Category ?? ""));
            foreach (var o in ds.Outcomes) map[o.SampleKey] = codes[o.Category ?? ""];
        }
        else if (ds.OutcomeType == VariableType.Binary)
        {
            foreach (var o in ds.Outcomes) map[o.SampleKey] = o.Value >= 0.5 ? 1.0 : 0.0;
        }
        else
        {
            foreach (var o in ds.Outcomes) if (!double.IsNaN(o.Value)) map[o.SampleKey] = o.Value;
        }
        return map;
    }

    private static Dictionary<string, double> CodeMap(IEnumerable<string> cats)
    {
        var map = new Dictionary<string, double>();
        int code = 0;
        foreach (var c in cats.Distinct().OrderBy(x => x, StringComparer.Ordinal))
            map[c] = code++;
        return map;
    }

    private static (double[] xs, double[] ys, string[] keys, int dropped) Align(
        FeatureSeries fs, IReadOnlyDictionary<string, double> repr)
    {
        Dictionary<string, double>? codes = null;
        if (fs.Type is VariableType.Categorical or VariableType.Binary)
            codes = CodeMap(fs.Samples.Where(s => s.Category != null).Select(s => s.Category!));

        var xs = new List<double>(); var ys = new List<double>(); var keys = new List<string>();
        int dropped = 0;
        foreach (var s in fs.Samples)
        {
            if (!repr.TryGetValue(s.SampleKey, out var y)) { dropped++; continue; }
            double? xv = fs.Type switch
            {
                VariableType.Numeric => s.Numeric,
                VariableType.Categorical => s.Category != null && codes!.TryGetValue(s.Category, out var cc) ? cc : (double?)null,
                VariableType.Binary => s.Category != null && codes!.TryGetValue(s.Category, out var bc) ? bc
                                        : (s.Numeric.HasValue ? (s.Numeric.Value >= 0.5 ? 1.0 : 0.0) : (double?)null),
                _ => null
            };
            if (xv is null) { dropped++; continue; }
            xs.Add(xv.Value); ys.Add(y); keys.Add(s.SampleKey);
        }
        return (xs.ToArray(), ys.ToArray(), keys.ToArray(), dropped);
    }

    private static MethodChoice ChooseMethod(VariableType featType, VariableType outType, double[] xs, double[] ys)
    {
        var baseChoice = MethodSelector.Select(featType, outType);
        if (baseChoice.Method == AnalysisMethod.Spearman) // numeric/numeric: test for nonlinearity
        {
            double sp = Math.Abs(Stats.Spearman(xs, ys));
            if (sp < 0.15)
            {
                double mi = MutualInformation.NormalizedNumeric(xs, ys);
                if (mi > 0.2)
                    return MethodSelector.Select(featType, outType, numericRelationshipNonlinear: true);
            }
        }
        return baseChoice;
    }

    private static (double eff, double p, Func<IReadOnlyList<double>, IReadOnlyList<double>, double> stat) Measure(
        AnalysisMethod method, double[] xs, double[] ys, int perms)
    {
        Func<IReadOnlyList<double>, IReadOnlyList<double>, double> stat;
        double eff, p;
        switch (method)
        {
            case AnalysisMethod.Spearman:
                stat = (a, b) => Stats.Spearman(a, b);
                eff = stat(xs, ys); p = Stats.CorrelationPValue(eff, xs.Length); break;
            case AnalysisMethod.PointBiserial:
                stat = (a, b) => Stats.Pearson(a, b); // point-biserial == Pearson with a 0/1 variable
                eff = stat(xs, ys); p = Stats.CorrelationPValue(eff, xs.Length); break;
            case AnalysisMethod.MutualInformation:
                stat = (a, b) => MutualInformation.NormalizedNumeric(a, b);
                eff = stat(xs, ys); p = PermP(xs, ys, stat, perms); break;
            case AnalysisMethod.CramersV:
                stat = (a, b) => CategoricalAssociation.CramersV(ToInts(a), ToInts(b));
                eff = stat(xs, ys); p = PermP(xs, ys, stat, perms); break;
            default:
                stat = (_, _) => double.NaN; eff = double.NaN; p = double.NaN; break;
        }
        return (eff, p, stat);
    }

    private static int[] ToInts(IReadOnlyList<double> a)
    {
        var r = new int[a.Count];
        for (int i = 0; i < a.Count; i++) r[i] = (int)Math.Round(a[i]);
        return r;
    }

    // Deterministic permutation p-value for methods without a closed-form (MI, Cramer's V).
    private static double PermP(double[] x, double[] y, Func<IReadOnlyList<double>, IReadOnlyList<double>, double> stat, int perms)
    {
        double obs = stat(x, y);
        if (double.IsNaN(obs) || perms <= 0) return double.NaN;
        var rng = new DeterministicRandom(Seed);
        var yb = (double[])y.Clone();
        int ge = 0;
        for (int k = 0; k < perms; k++)
        {
            for (int i = yb.Length - 1; i >= 1; i--) { int j = rng.NextInt(i + 1); (yb[i], yb[j]) = (yb[j], yb[i]); }
            double s = stat(x, yb);
            if (!double.IsNaN(s) && s >= obs - 1e-12) ge++;
        }
        return (ge + 1.0) / (perms + 1.0);
    }

    private static void IterativeVifExclude(List<FeatureCompute> computed, double threshold, List<ExcludedFeature> excluded)
    {
        for (int guard = 0; guard < 256; guard++)
        {
            var active = computed.Where(c => c.Type == VariableType.Numeric && excluded.All(e => e.FeatureKey != c.FeatureKey)).ToList();
            if (active.Count < 2) return;

            var maps = active.Select(c =>
            {
                var d = new Dictionary<string, double>(c.Keys.Length);
                for (int i = 0; i < c.Keys.Length; i++) d[c.Keys[i]] = c.X[i];
                return d;
            }).ToList();

            var common = maps.Skip(1).Aggregate(new HashSet<string>(maps[0].Keys), (acc, m) => { acc.IntersectWith(m.Keys); return acc; }).ToList();
            if (common.Count < MinPairs) return;

            var matrix = new double[common.Count][];
            for (int r = 0; r < common.Count; r++)
            {
                var row = new double[active.Count];
                for (int cIdx = 0; cIdx < active.Count; cIdx++) row[cIdx] = maps[cIdx][common[r]];
                matrix[r] = row;
            }

            var vif = VarianceInflation.Compute(matrix, threshold);
            if (vif.Flagged.Length == 0) return;

            // Remove the single worst: highest VIF; tie -> lower |effect| -> larger key (deterministic).
            var pick = vif.Flagged
                .Select(idx => (Feature: active[idx], Vif: vif.Vif[idx]))
                .OrderByDescending(t => t.Vif)
                .ThenBy(t => Math.Abs(t.Feature.Effect))
                .ThenByDescending(t => t.Feature.FeatureKey, StringComparer.Ordinal)
                .First();

            double v = pick.Vif;
            string vText = double.IsPositiveInfinity(v) ? "infinite" : v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            excluded.Add(new ExcludedFeature(pick.Feature.FeatureKey,
                $"Collinear (VIF {vText} >= {threshold:F1}); removed to keep one representative."));
        }
    }

    private static string MaybeLasso(List<FeatureCompute> kept)
    {
        var numeric = kept.Where(c => c.Type == VariableType.Numeric).ToList();
        if (numeric.Count <= 8) return string.Empty;
        try
        {
            var maps = numeric.Select(c => { var d = new Dictionary<string, double>(); for (int i = 0; i < c.Keys.Length; i++) d[c.Keys[i]] = c.X[i]; return d; }).ToList();
            var yMap = new Dictionary<string, double>(); var c0 = numeric[0];
            for (int i = 0; i < c0.Keys.Length; i++) yMap[c0.Keys[i]] = c0.Y[i];
            var common = maps.Skip(1).Aggregate(new HashSet<string>(maps[0].Keys), (acc, m) => { acc.IntersectWith(m.Keys); return acc; });
            common.IntersectWith(yMap.Keys);
            var keys = common.ToList();
            if (keys.Count < MinPairs) return string.Empty;
            var X = new double[keys.Count][]; var Y = new double[keys.Count];
            for (int r = 0; r < keys.Count; r++)
            {
                X[r] = new double[numeric.Count];
                for (int cIdx = 0; cIdx < numeric.Count; cIdx++) X[r][cIdx] = maps[cIdx][keys[r]];
                Y[r] = yMap[keys[r]];
            }
            var lasso = Lasso.Fit(X, Y, lambda: 0.5);
            return $"Lasso screen selected {lasso.SelectedFeatures.Length} of {numeric.Count} numeric predictors.";
        }
        catch { return string.Empty; }
    }

    private static (bool survives, string reason) EvaluateStrata(FeatureCompute c, AdvancedDataset ds)
    {
        if (ds.StrataBySampleKey.Count == 0)
            return (true, "Stratification not evaluated: no stratum dimension available.");

        var groups = new Dictionary<string, List<int>>();
        for (int i = 0; i < c.Keys.Length; i++)
            if (ds.StrataBySampleKey.TryGetValue(c.Keys[i], out var lbl))
                (groups.TryGetValue(lbl, out var lst) ? lst : groups[lbl] = new List<int>()).Add(i);

        if (groups.Count == 0)
            return (true, "Stratification not evaluated: stratum labels not present for these samples.");

        var strata = new List<StratumEffect>();
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            var sx = kv.Value.Select(i => c.X[i]).ToArray();
            var sy = kv.Value.Select(i => c.Y[i]).ToArray();
            double e = c.Stat(sx, sy);
            strata.Add(new StratumEffect(kv.Key, double.IsNaN(e) ? 0.0 : e, kv.Value.Count));
        }

        if (strata.All(s => s.SampleSize < 20))
            return (true, "Stratification not evaluated: no adequately-sized stratum (>=20).");

        var verdict = Stratification.Evaluate(c.Effect, strata);
        return (verdict.Survives, verdict.Reason);
    }
}
