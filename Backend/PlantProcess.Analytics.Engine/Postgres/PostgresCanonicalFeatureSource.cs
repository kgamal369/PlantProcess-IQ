using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Readiness;
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Analytics.Engine.Postgres;

/// <summary>
/// Reads canonical features and the outcome from ml_feature_values / ml_outcome_values, paired on
/// effective_sample_key over the request window/grain - the same pairing the v6 SQL function uses.
/// Numeric and boolean features are analyzed; categorical/text/datetime are skipped for a numeric outcome.
/// </summary>
public sealed class PostgresCanonicalFeatureSource : ICanonicalFeatureSource
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly double _freshnessCadenceDays;

    public PostgresCanonicalFeatureSource(NpgsqlDataSource dataSource, double freshnessCadenceDays = 7.0)
    {
        _dataSource = dataSource;
        _freshnessCadenceDays = freshnessCadenceDays <= 0 ? 7.0 : freshnessCadenceDays;
    }

    public async Task<FeatureMatrix> LoadAsync(CorrelationComputeRequest request, CancellationToken cancellationToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var grain = string.IsNullOrWhiteSpace(request.Grain) ? "coil" : request.Grain.Trim();
        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 3650 : request.WindowDays, 1, 3650);
        var outcomeKey = request.OutcomeKey.Trim();

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1) one outcome value per sample
        var sampleKeys = new List<string>();
        var outcome = new List<double>();
        var heatBySample = new Dictionary<string, string?>();
        DateTime? maxObserved = null;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT o.effective_sample_key AS k,
                       avg(o.numeric_value)   AS v,
                       max(o.heat_id)         AS heat,
                       max(o.observed_at_utc) AS obs
                FROM public.ml_outcome_values o
                JOIN public.ml_outcome_definitions od
                  ON lower(od.outcome_key) = lower(@outcomeKey) AND od.is_deleted = false
                WHERE lower(o.outcome_key) = lower(@outcomeKey)
                  AND (@grain = 'generic' OR o.grain = @grain)
                  AND o.numeric_value IS NOT NULL
                  AND o.observed_at_utc >= now() - make_interval(days => @windowDays)
                GROUP BY o.effective_sample_key
                ORDER BY o.effective_sample_key;";
            cmd.Parameters.AddWithValue("outcomeKey", outcomeKey);
            cmd.Parameters.AddWithValue("grain", grain);
            cmd.Parameters.AddWithValue("windowDays", windowDays);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                if (r.IsDBNull(1)) continue;
                var key = r.GetString(0);
                sampleKeys.Add(key);
                outcome.Add(r.GetDouble(1));
                heatBySample[key] = r.IsDBNull(2) ? null : r.GetString(2);
                if (!r.IsDBNull(3))
                {
                    var obs = r.GetDateTime(3);
                    if (maxObserved is null || obs > maxObserved.Value) maxObserved = obs;
                }
            }
        }

        int n = sampleKeys.Count;
        if (n == 0)
            return new FeatureMatrix(outcomeKey, grain, Array.Empty<double>(), Array.Empty<FeatureColumn>(),
                new ReadinessInput(0, 0, 0.0, double.MaxValue, 0.0), 0);

        var indexBySample = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++) indexBySample[sampleKeys[i]] = i;

        // 2) feature values per (feature_key, sample), numeric + boolean only
        var cells = new Dictionary<string, double?[]>(StringComparer.Ordinal);
        var featureType = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT f.feature_key            AS fk,
                       fd.value_type            AS vt,
                       f.effective_sample_key   AS k,
                       avg(f.numeric_value)     AS num,
                       bool_or(f.boolean_value) AS bln
                FROM public.ml_feature_values f
                JOIN public.ml_feature_definitions fd
                  ON lower(fd.feature_key) = lower(f.feature_key) AND fd.is_deleted = false
                WHERE (@grain = 'generic' OR f.grain = @grain)
                  AND fd.value_type IN ('numeric','boolean')
                  AND f.observed_at_utc >= now() - make_interval(days => @windowDays)
                GROUP BY f.feature_key, fd.value_type, f.effective_sample_key;";
            cmd.Parameters.AddWithValue("grain", grain);
            cmd.Parameters.AddWithValue("windowDays", windowDays);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var fk = r.GetString(0);
                var vt = r.GetString(1);
                var key = r.GetString(2);
                if (!indexBySample.TryGetValue(key, out int idx)) continue;
                if (!cells.TryGetValue(fk, out var arr)) { arr = new double?[n]; cells[fk] = arr; featureType[fk] = vt; }
                bool isBool = string.Equals(vt, "boolean", StringComparison.OrdinalIgnoreCase);
                arr[idx] = isBool
                    ? (r.IsDBNull(4) ? (double?)null : (r.GetBoolean(4) ? 1.0 : 0.0))
                    : (r.IsDBNull(3) ? (double?)null : r.GetDouble(3));
            }
        }

        var columns = cells
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new FeatureColumn(
                kv.Key,
                string.Equals(featureType[kv.Key], "boolean", StringComparison.OrdinalIgnoreCase) ? VariableType.Binary : VariableType.Numeric,
                kv.Value))
            .ToList();

        // readiness derived from the data
        int distinctHeats = heatBySample.Values.Where(h => !string.IsNullOrEmpty(h)).Distinct().Count();
        if (distinctHeats == 0) distinctHeats = n;
        int events = outcome.Count(v => v > 0.0);
        double pEvent = (double)events / n;
        double minority = Math.Min(pEvent, 1.0 - pEvent);
        long totalCells = (long)n * Math.Max(1, columns.Count);
        long filled = columns.Sum(c => (long)c.Values.Count(v => v.HasValue));
        double completeness = columns.Count == 0 ? 0.0 : (double)filled / totalCells;
        double freshness = maxObserved is null
            ? double.MaxValue
            : Math.Max(0.0, (DateTime.UtcNow - DateTime.SpecifyKind(maxObserved.Value, DateTimeKind.Utc)).TotalDays) / _freshnessCadenceDays;

        int excluded = 0;
        for (int i = 0; i < n; i++)
        {
            bool any = false;
            foreach (var c in columns) { if (c.Values[i].HasValue) { any = true; break; } }
            if (!any) excluded++;
        }

        var readiness = new ReadinessInput(distinctHeats, events, minority, freshness, completeness);
        return new FeatureMatrix(outcomeKey, grain, outcome, columns, readiness, excluded);
    }
}