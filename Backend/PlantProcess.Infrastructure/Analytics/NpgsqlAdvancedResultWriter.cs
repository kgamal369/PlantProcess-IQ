using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// P3-05 writer. Persists the run header (ml_correlation_compute_runs) and each finding
/// (ml_correlation_results_v2) transactionally, tenant-scoped, with a resolvable
/// provenance handle and the honesty caveat carried in evidence_json.
/// </summary>
public sealed class NpgsqlAdvancedResultWriter : IAdvancedResultWriter
{
    private readonly NpgsqlDataSource _dataSource;
    public NpgsqlAdvancedResultWriter(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<Guid> WriteAsync(AdvancedAnalysisRequest req, AdvancedAnalysisRunResult result, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // PPIQ-V1: set app.current_tenant so RLS WITH CHECK passes under the least-privilege ppiq_app runtime role.
        await using (var tctx = new NpgsqlCommand("SELECT set_config('app.current_tenant', @t, false)", conn, (NpgsqlTransaction)tx))
        {
            tctx.Parameters.AddWithValue("t", req.TenantId.ToString());
            await tctx.ExecuteNonQueryAsync(ct);
        }

        var status = !result.CanRun ? "Blocked" : (result.Findings.Count > 0 ? "Ok" : "NoData");
        var reqJson = JsonSerializer.Serialize(new { tenantId = req.TenantId, filters = req.Filters, fdrQ = req.FdrQ, correlationId = req.CorrelationId });
        var outType = result.OutcomeType.ToString().ToLowerInvariant();

        await using (var c = new NpgsqlCommand(
            @"INSERT INTO ppiq_meta.ml_correlation_compute_runs
              (id, engine_key, target_outcome_key, grain, window_days, status, completed_at_utc, duration_ms, message, request_json, tenant_id)
              VALUES (@id, @ek, @ok, @g, @w, @st, now(), 0, @msg, @rj::jsonb, @tid)", conn, (NpgsqlTransaction)tx))
        {
            c.Parameters.AddWithValue("id", result.RunId);
            c.Parameters.AddWithValue("ek", result.Engine);
            c.Parameters.AddWithValue("ok", result.OutcomeKey);
            c.Parameters.AddWithValue("g", result.Grain);
            c.Parameters.AddWithValue("w", result.WindowDays);
            c.Parameters.AddWithValue("st", status);
            c.Parameters.AddWithValue("msg", (object?)result.Message ?? DBNull.Value);
            c.Parameters.AddWithValue("rj", reqJson);
            c.Parameters.AddWithValue("tid", req.TenantId);
            await c.ExecuteNonQueryAsync(ct);
        }

        foreach (var f in result.Findings)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                provenanceHandle = f.ProvenanceHandle,
                honestyCaveat = f.HonestyCaveat,
                significant = f.Significant,
                survivesStratification = f.SurvivesStratification,
                stratificationReason = f.StratificationReason,
                stabilityConsistency = f.StabilityConsistency,
                excludedRecords = f.ExcludedRecords,
                rationale = f.MethodRationale,
                readiness = result.Readiness.ToString()
            });

            await using var c = new NpgsqlCommand(
                @"INSERT INTO ppiq_plant.ml_correlation_results_v2
                  (id, compute_run_id, feature_key, feature_grain, outcome_key, outcome_type, method,
                   coefficient, effect_size, effect_size_type, p_value, q_value, ci_low, ci_high,
                   sample_size, effective_n, stratum, stability_score, is_stable, evidence_json, tenant_id)
                  VALUES (gen_random_uuid(), @run, @fk, @fg, @ok, @ot, @m,
                          @co, @es, @est, @p, @q, @cl, @ch,
                          @n, @en, NULL, @ss, @stable, @ev::jsonb, @tid)", conn, (NpgsqlTransaction)tx);
            c.Parameters.AddWithValue("run", result.RunId);
            c.Parameters.AddWithValue("fk", f.FeatureKey);
            c.Parameters.AddWithValue("fg", result.Grain);
            c.Parameters.AddWithValue("ok", result.OutcomeKey);
            c.Parameters.AddWithValue("ot", outType);
            c.Parameters.AddWithValue("m", f.Method.ToString());
            c.Parameters.AddWithValue("co", f.EffectSize);
            c.Parameters.AddWithValue("es", f.EffectSize);
            c.Parameters.AddWithValue("est", f.Method.ToString());
            c.Parameters.AddWithValue("p", f.PValue);
            c.Parameters.AddWithValue("q", f.QValue);
            c.Parameters.AddWithValue("cl", f.StabilityLower);
            c.Parameters.AddWithValue("ch", f.StabilityUpper);
            c.Parameters.AddWithValue("n", f.SampleSize);
            c.Parameters.AddWithValue("en", f.SampleSize);
            c.Parameters.AddWithValue("ss", f.StabilityConsistency);
            c.Parameters.AddWithValue("stable", f.IsStable);
            c.Parameters.AddWithValue("ev", evidence);
            c.Parameters.AddWithValue("tid", req.TenantId);
            await c.ExecuteNonQueryAsync(ct);
        }

        foreach (var ex in result.Excluded)
        {
            var exEvidence = System.Text.Json.JsonSerializer.Serialize(new { excluded = true, reason = ex.Reason });
            await using var ce = new NpgsqlCommand(
                @"INSERT INTO ppiq_plant.ml_correlation_results_v2
                  (id, compute_run_id, feature_key, feature_grain, outcome_key, outcome_type, method, sample_size, effective_n, evidence_json, tenant_id)
                  VALUES (gen_random_uuid(), @run, @fk, @fg, @ok, @ot, 'NotApplicable', 0, 0, @ev::jsonb, @tid)", conn, (NpgsqlTransaction)tx);
            ce.Parameters.AddWithValue("run", result.RunId);
            ce.Parameters.AddWithValue("fk", ex.FeatureKey);
            ce.Parameters.AddWithValue("fg", result.Grain);
            ce.Parameters.AddWithValue("ok", result.OutcomeKey);
            ce.Parameters.AddWithValue("ot", outType);
            ce.Parameters.AddWithValue("ev", exEvidence);
            ce.Parameters.AddWithValue("tid", req.TenantId);
            await ce.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return result.RunId;
    }
}

