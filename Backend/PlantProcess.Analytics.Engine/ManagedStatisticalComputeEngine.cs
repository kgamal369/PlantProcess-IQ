using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Discipline;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Numerics;
using PlantProcess.Analytics.Core.Readiness;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Analytics.Engine;

/// <summary>
/// Real-methods correlation engine (v4 7.3) sitting behind ICorrelationComputeEngine alongside the SQL engine.
/// Auto-selects a method per parameter, ranks by effect size, controls multiple testing with Benjamini-Hochberg FDR,
/// and bootstraps stability. Computes live from the canonical feature matrix; never reads precomputed rows.
/// </summary>
public sealed class ManagedStatisticalComputeEngine : ICorrelationComputeEngine
{
    private readonly ICanonicalFeatureSource _source;
    private readonly IAnalysisFindingSink _sink;
    private readonly ReadinessThresholds? _thresholds;
    private readonly double _fdrQ;

    public ManagedStatisticalComputeEngine(
        ICanonicalFeatureSource source,
        IAnalysisFindingSink sink,
        ReadinessThresholds? thresholds = null,
        double fdrQ = 0.05)
    {
        _source = source;
        _sink = sink;
        _thresholds = thresholds;
        _fdrQ = fdrQ;
    }

    public string EngineKey => "managed-stat-v1";

    public async Task<CorrelationComputeResult> ComputeAsync(CorrelationComputeRequest request, CancellationToken cancellationToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.OutcomeKey)) throw new ArgumentException("Outcome key is required.", nameof(request));

        var matrix = await _source.LoadAsync(request, cancellationToken);
        var runId = Guid.NewGuid();

        var readiness = ReadinessGate.Evaluate(matrix.Readiness, _thresholds);
        if (!readiness.CanRun)
        {
            await _sink.WriteAsync(runId, request, Array.Empty<AnalysisFinding>(), cancellationToken);
            var blocked = string.Join("; ", readiness.Dimensions.Where(d => d.State == ReadinessState.Blocked).Select(d => d.Reason));
            return new CorrelationComputeResult(runId, 0, EngineKey, "Blocked", $"Readiness gate blocked the analysis. {blocked}");
        }

        var outcome = matrix.Outcome;
        var prelim = new List<(FeatureColumn Col, AnalysisMethod Method, double Effect, double P, int N, double[] Px, double[] Py, bool Binary)>();

        foreach (var col in matrix.Parameters)
        {
            var choice = MethodSelector.Select(col.Type, VariableType.Numeric); // outcome is numeric
            if (!choice.IsApplicable) continue;

            int count = Math.Min(col.Values.Count, outcome.Count);
            var px = new List<double>(count);
            var py = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                var v = col.Values[i];
                if (v.HasValue) { px.Add(v.Value); py.Add(outcome[i]); }
            }
            if (px.Count < 4) continue;

            double effect;
            if (choice.Method == AnalysisMethod.PointBiserial)
            {
                var bin = px.Select(x => (int)Math.Round(x)).ToList();
                effect = Stats.PointBiserial(bin, py);
            }
            else
            {
                effect = Stats.Spearman(px, py);
            }
            if (double.IsNaN(effect)) continue; // zero-variance parameter

            double p = Stats.CorrelationPValue(effect, px.Count);
            prelim.Add((col, choice.Method, effect, double.IsNaN(p) ? 1.0 : p, px.Count, px.ToArray(), py.ToArray(), choice.Method == AnalysisMethod.PointBiserial));
        }

        var fdr = BenjaminiHochberg.Adjust(prelim.Select(x => x.P).ToList(), _fdrQ);

        var findings = new List<AnalysisFinding>(prelim.Count);
        for (int i = 0; i < prelim.Count; i++)
        {
            var item = prelim[i];
            var verdict = fdr[i];

            double lo = 0, hi = 0, cons = 0; bool stable = false;
            if (verdict.Significant)
            {
                Func<IReadOnlyList<double>, IReadOnlyList<double>, double> stat = item.Binary
                    ? (a, b) => { var v = Stats.Pearson(a, b); return double.IsNaN(v) ? 0.0 : v; }
                    : (a, b) => { var v = Stats.Spearman(a, b); return double.IsNaN(v) ? 0.0 : v; };
                var boot = Bootstrap.Stability(item.Px, item.Py, stat, iterations: 500, seed: 20260602UL);
                lo = boot.Lower; hi = boot.Upper; cons = boot.SignConsistency; stable = boot.Stable;
            }

            findings.Add(new AnalysisFinding(
                item.Col.Code, item.Method, item.Effect, item.P, verdict.QValue, verdict.Significant,
                item.N, lo, hi, cons, stable));
        }

        var ranked = findings.OrderByDescending(f => Math.Abs(f.EffectSize)).ToList(); // rank by effect, never p
        await _sink.WriteAsync(runId, request, ranked, cancellationToken);

        int significant = ranked.Count(f => f.Significant);
        string status = readiness.Overall == ReadinessState.Partial ? "Partial" : "Ok";
        string message = $"Managed compute finished. Applicable={ranked.Count}, Significant(q<{_fdrQ})={significant}, Readiness={readiness.Overall}, Excluded={matrix.ExcludedRecords}.";
        return new CorrelationComputeResult(runId, ranked.Count, EngineKey, status, message);
    }
}