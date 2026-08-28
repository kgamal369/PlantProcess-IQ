using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// P3-01 loader. Reads aligned feature/outcome vectors from the v6 feature store
/// (ml_feature_values / ml_outcome_values + definitions). The window is measured
/// relative to the MAX observed timestamp so a static demo dataset is not excluded.
/// Freshness is reported as 0.0 (Ready) and deferred to the live ingestion path.
/// </summary>
public sealed class NpgsqlFeatureVectorLoader : IFeatureVectorLoader
{
    private readonly NpgsqlDataSource _dataSource;
    public NpgsqlFeatureVectorLoader(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest req, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        // outcome type
        string? otype = null;
        await using (var c = new NpgsqlCommand(
            "SELECT outcome_type FROM ppiq_meta.ml_outcome_definitions WHERE lower(outcome_key)=lower(@k) AND is_deleted=false ORDER BY version DESC LIMIT 1", conn))
        {
            c.Parameters.AddWithValue("k", req.OutcomeKey);
            otype = (await c.ExecuteScalarAsync(ct)) as string;
        }
        var outVar = MapOutcome(otype);

        // outcomes (window relative to max observed)
        var outcomes = new List<OutcomeSample>();
        var heats = new HashSet<string>();
        await using (var c = new NpgsqlCommand(
            @"SELECT effective_sample_key, numeric_value, category_value, heat_id
              FROM ppiq_plant.ml_outcome_values
              WHERE lower(outcome_key)=lower(@k) AND grain=@g
                AND (@w >= 3650 OR observed_at_utc >= COALESCE(
                     (SELECT max(observed_at_utc) FROM ppiq_plant.ml_outcome_values WHERE lower(outcome_key)=lower(@k) AND grain=@g),
                     'epoch'::timestamptz) - make_interval(days => @w))", conn))
        {
            c.Parameters.AddWithValue("k", req.OutcomeKey);
            c.Parameters.AddWithValue("g", req.Grain);
            c.Parameters.AddWithValue("w", req.WindowDays);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var key = r.GetString(0);
                double val = r.IsDBNull(1) ? double.NaN : r.GetDouble(1);
                string? cat = r.IsDBNull(2) ? null : r.GetString(2);
                string? heat = r.IsDBNull(3) ? null : r.GetString(3);
                outcomes.Add(new OutcomeSample(key, val, cat, heat));
                if (heat != null) heats.Add(heat);
            }
        }

        // feature definition types
        var ftype = new Dictionary<string, VariableType>(StringComparer.OrdinalIgnoreCase);
        await using (var c = new NpgsqlCommand(
            "SELECT lower(feature_key), value_type FROM ppiq_meta.ml_feature_definitions WHERE is_deleted=false ORDER BY version DESC", conn))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var fk = r.GetString(0); var vt = r.GetString(1);
                if (!ftype.ContainsKey(fk)) ftype[fk] = MapFeature(vt);
            }
        }

        // feature values
        var grouped = new Dictionary<string, List<FeatureSample>>(StringComparer.OrdinalIgnoreCase);
        await using (var c = new NpgsqlCommand(
            @"SELECT feature_key, numeric_value, category_value, boolean_value, effective_sample_key
              FROM ppiq_plant.ml_feature_values
              WHERE grain=@g AND missingness_flag=false
                AND (@w >= 3650 OR observed_at_utc >= COALESCE(
                     (SELECT max(observed_at_utc) FROM ppiq_plant.ml_feature_values WHERE grain=@g),
                     'epoch'::timestamptz) - make_interval(days => @w))", conn))
        {
            c.Parameters.AddWithValue("g", req.Grain);
            c.Parameters.AddWithValue("w", req.WindowDays);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var fk = r.GetString(0);
                double? num = r.IsDBNull(1) ? (double?)null : r.GetDouble(1);
                string? cat = r.IsDBNull(2) ? null : r.GetString(2);
                bool? boo = r.IsDBNull(3) ? (bool?)null : r.GetBoolean(3);
                var key = r.GetString(4);
                string? catVal = cat ?? (boo.HasValue ? (boo.Value ? "true" : "false") : null);
                (grouped.TryGetValue(fk, out var lst) ? lst : grouped[fk] = new List<FeatureSample>())
                    .Add(new FeatureSample(key, num, catVal));
            }
        }

        var features = new List<FeatureSeries>();
        foreach (var kv in grouped)
        {
            var type = ftype.TryGetValue(kv.Key, out var t)
                ? t
                : (kv.Value.Any(s => s.Numeric.HasValue) ? VariableType.Numeric : VariableType.Categorical);
            if (type == VariableType.Numeric && kv.Value.All(s => !s.Numeric.HasValue)) continue; // datetime/unsupported
            features.Add(new FeatureSeries(kv.Key, type, kv.Value));
        }

        // strata from shift/grade-like categorical features
        var strata = new Dictionary<string, string>();
        var stratumFeature = features.FirstOrDefault(f =>
            f.Type != VariableType.Numeric &&
            (f.FeatureKey.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 ||
             f.FeatureKey.IndexOf("grade", StringComparison.OrdinalIgnoreCase) >= 0));
        if (stratumFeature != null)
            foreach (var s in stratumFeature.Samples)
                if (s.Category != null) strata[s.SampleKey] = s.Category;

        // completeness = share of outcome samples that have at least one feature sample
        var featureKeys = new HashSet<string>(features.SelectMany(f => f.Samples.Select(s => s.SampleKey)));
        var outcomeKeys = outcomes.Select(o => o.SampleKey).ToHashSet();
        double completeness = outcomeKeys.Count == 0 ? 0.0 : outcomeKeys.Count(k => featureKeys.Contains(k)) / (double)outcomeKeys.Count;

        return new AdvancedDataset(req.OutcomeKey, outVar, outcomes, features, strata, heats.Count, 0.0, completeness);
    }

    private static VariableType MapOutcome(string? t) => (t ?? "continuous").ToLowerInvariant() switch
    {
        "binary" => VariableType.Binary,
        "multinomial" or "ordinal" => VariableType.Categorical,
        _ => VariableType.Numeric // continuous, count, rate, duration
    };

    private static VariableType MapFeature(string t) => t.ToLowerInvariant() switch
    {
        "categorical" or "text" => VariableType.Categorical,
        "boolean" => VariableType.Binary,
        _ => VariableType.Numeric // numeric, datetime(handled later)
    };
}
