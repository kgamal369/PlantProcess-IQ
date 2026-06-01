using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Analytics.Engine.Postgres;

/// <summary>Persists a managed run + its findings to ml_correlation_compute_runs / ml_correlation_results_v2.</summary>
public sealed class PostgresAnalysisFindingSink : IAnalysisFindingSink
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresAnalysisFindingSink(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task WriteAsync(Guid computeRunId, CorrelationComputeRequest request, IReadOnlyList<AnalysisFinding> findings, CancellationToken cancellationToken)
    {
        var grain = string.IsNullOrWhiteSpace(request.Grain) ? "coil" : request.Grain.Trim();
        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 3650 : request.WindowDays, 1, 3650);
        var outcomeKey = request.OutcomeKey.Trim();
        string status = findings.Count > 0 ? "Success" : "NoData";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await using (var runCmd = conn.CreateCommand())
        {
            runCmd.Transaction = tx;
            runCmd.CommandText = @"
                INSERT INTO public.ml_correlation_compute_runs
                    (id, engine_key, target_outcome_key, grain, window_days, status, completed_at_utc, duration_ms, message, request_json)
                VALUES
                    (@id, @engine, @outcome, @grain, @window, @status, now(), 0, @message,
                     jsonb_build_object('outcomeKey', @outcome, 'grain', @grain, 'windowDays', @window, 'engine', @engine));";
            runCmd.Parameters.AddWithValue("id", computeRunId);
            runCmd.Parameters.AddWithValue("engine", "managed-stat-v1");
            runCmd.Parameters.AddWithValue("outcome", outcomeKey);
            runCmd.Parameters.AddWithValue("grain", grain);
            runCmd.Parameters.AddWithValue("window", windowDays);
            runCmd.Parameters.AddWithValue("status", status);
            runCmd.Parameters.AddWithValue("message", $"Managed statistical engine. Findings={findings.Count}.");
            await runCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var f in findings)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO public.ml_correlation_results_v2
                    (compute_run_id, feature_key, feature_grain, outcome_key, outcome_type, method,
                     coefficient, effect_size, effect_size_type, p_value, q_value,
                     ci_low, ci_high, sample_size, effective_n, stability_score, is_stable, evidence_json)
                VALUES
                    (@run, @fk, @grain, @outcome, @otype, @method,
                     @coef, @effect, @etype, @p, @q,
                     @cilow, @cihigh, @n, @en, @stab, @isstable,
                     jsonb_build_object(
                        'honestFraming', 'statistical association, not guaranteed root cause',
                        'significant', @sig,
                        'method', @method,
                        'findingStatus', @fstatus));";
            cmd.Parameters.AddWithValue("run", computeRunId);
            cmd.Parameters.AddWithValue("fk", f.ParameterCode);
            cmd.Parameters.AddWithValue("grain", grain);
            cmd.Parameters.AddWithValue("outcome", outcomeKey);
            cmd.Parameters.AddWithValue("otype", "continuous");
            cmd.Parameters.AddWithValue("method", MethodName(f.Method));
            cmd.Parameters.AddWithValue("coef", f.EffectSize);
            cmd.Parameters.AddWithValue("effect", Math.Abs(f.EffectSize));
            cmd.Parameters.AddWithValue("etype", EffectType(f.Method));
            cmd.Parameters.AddWithValue("p", NaNToDb(f.PValue));
            cmd.Parameters.AddWithValue("q", NaNToDb(f.QValue));
            cmd.Parameters.AddWithValue("cilow", f.Significant ? (object)f.StabilityLower : DBNull.Value);
            cmd.Parameters.AddWithValue("cihigh", f.Significant ? (object)f.StabilityUpper : DBNull.Value);
            cmd.Parameters.AddWithValue("n", f.SampleSize);
            cmd.Parameters.AddWithValue("en", f.SampleSize);
            cmd.Parameters.AddWithValue("stab", f.Significant ? (object)f.StabilityConsistency : DBNull.Value);
            cmd.Parameters.AddWithValue("isstable", (object)f.Stable);
            cmd.Parameters.AddWithValue("sig", f.Significant);
            cmd.Parameters.AddWithValue("fstatus", f.Significant ? "EvidenceForReview" : "NotSignificant");
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static object NaNToDb(double v) => double.IsNaN(v) ? DBNull.Value : v;

    private static string MethodName(AnalysisMethod m) => m switch
    {
        AnalysisMethod.Spearman => "managed_spearman",
        AnalysisMethod.PointBiserial => "managed_point_biserial",
        AnalysisMethod.MutualInformation => "managed_mutual_information",
        AnalysisMethod.CramersV => "managed_cramers_v",
        AnalysisMethod.LassoVif => "managed_lasso_vif",
        _ => "managed_unknown"
    };

    private static string EffectType(AnalysisMethod m) => m switch
    {
        AnalysisMethod.Spearman => "abs_spearman_rho",
        AnalysisMethod.PointBiserial => "abs_point_biserial_r",
        AnalysisMethod.MutualInformation => "normalized_mutual_information",
        AnalysisMethod.CramersV => "cramers_v",
        AnalysisMethod.LassoVif => "lasso_standardized_beta",
        _ => "effect"
    };
}