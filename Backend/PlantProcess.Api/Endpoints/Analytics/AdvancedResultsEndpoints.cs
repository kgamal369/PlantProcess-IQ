using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>T-034 (backend): surfaces the section 6.2 advanced-analysis result shape per finding.</summary>
public static class AdvancedResultsEndpoints
{
    private const string Caveat = "This is a diagnostic association, not a guaranteed root cause.";

    public static IEndpointRouteBuilder MapAdvancedResultsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/advanced")
            .WithTags("Analytics Advanced")
            .RequireAuthorization();
        group.MapGet("/readiness", async (string outcomeKey, string? grain, int? windowDays, System.Guid? tenantId, PlantProcess.Application.Analytics.Advanced.IAnalysisReadinessService readiness, System.Threading.CancellationToken ct) =>
            Results.Ok(await readiness.EvaluateAsync(new PlantProcess.Application.Analytics.Advanced.AdvancedAnalysisRequest(outcomeKey, string.IsNullOrWhiteSpace(grain) ? "coil" : grain!, windowDays ?? 3650, tenantId ?? PlantProcess.Application.Analytics.Advanced.AdvancedDefaults.DemoTenant), ct)));

        // PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES: canonical API surface for HMI readiness badges.
        group.MapGet("/readiness/gates", async (
            string outcomeKey,
            string? grain,
            int? windowDays,
            System.Guid? tenantId,
            IAnalysisReadinessService readiness,
            System.Threading.CancellationToken ct) =>
        {
            var request = new AdvancedAnalysisRequest(
                outcomeKey,
                string.IsNullOrWhiteSpace(grain) ? "coil" : grain!,
                windowDays ?? 3650,
                tenantId ?? AdvancedDefaults.DemoTenant);

            var dto = await readiness.EvaluateAsync(request, ct);
            return Results.Ok(AdvancedReadinessGateProjector.Project(dto));
        });

        group.MapGet("/runs", async (NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await ReadAsync(ds,
                "SELECT id, engine_key, target_outcome_key, grain, window_days, status, completed_at_utc, duration_ms, message " +
                "FROM public.ml_correlation_compute_runs ORDER BY completed_at_utc DESC NULLS LAST LIMIT 50", null, ct)));

        group.MapGet("/results", async (Guid? runId, string? outcomeKey, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            Guid? resolvedRun = runId;
            if (resolvedRun is null)
            {
                await using var rc = ds.CreateCommand(
                    "SELECT id FROM public.ml_correlation_compute_runs WHERE engine_key IN ('dotnet-analytics-core-v1','managed-stat-v1','ppiql-deterministic-core-v1') " +
                    (string.IsNullOrWhiteSpace(outcomeKey) ? "" : "AND target_outcome_key = @o ") +
                    "ORDER BY completed_at_utc DESC NULLS LAST LIMIT 1");
                if (!string.IsNullOrWhiteSpace(outcomeKey)) rc.Parameters.AddWithValue("o", outcomeKey!);
                if (await rc.ExecuteScalarAsync(ct) is Guid g) resolvedRun = g;
            }
            if (resolvedRun is null)
                return Results.Ok(new { runId = (Guid?)null, honestyCaveat = Caveat, results = Array.Empty<object>() });

            var rows = await ReadAsync(ds,
                "SELECT r.feature_key, r.feature_grain, r.outcome_key, r.outcome_type, r.method, " +
                "r.effect_size, r.q_value, r.sample_size, r.ci_low, r.ci_high, r.stability_score, r.is_stable, " +
                "r.stratum, r.evidence_json, run.window_days " +
                "FROM public.ml_correlation_results_v2 r " +
                "JOIN public.ml_correlation_compute_runs run ON run.id = r.compute_run_id " +
                "WHERE r.compute_run_id = @run ORDER BY abs(coalesce(r.effect_size, 0)) DESC",
                ("run", (object)resolvedRun.Value), ct);

            var shaped = rows.Select(row =>
            {
                var sampleSize = row.TryGetValue("sample_size", out var ss) && ss is not null ? Convert.ToInt32(ss) : 0;
                var method = (row.TryGetValue("method", out var m) ? m as string : null) ?? "NotApplicable";
                var stratum = row.TryGetValue("stratum", out var st) ? st : null;
                var renderable = !string.Equals(method, "NotApplicable", StringComparison.OrdinalIgnoreCase) && sampleSize > 0;
                return new
                {
                    findingId = row.GetValueOrDefault("feature_key"),
                    method,
                    effectSize = row.GetValueOrDefault("effect_size"),
                    qValue = row.GetValueOrDefault("q_value"),
                    sampleSize,
                    outcomeKey = row.GetValueOrDefault("outcome_key"),
                    outcomeType = row.GetValueOrDefault("outcome_type"),
                    grain = row.GetValueOrDefault("feature_grain"),
                    stabilityLower = row.GetValueOrDefault("ci_low"),
                    stabilityUpper = row.GetValueOrDefault("ci_high"),
                    stabilityConsistency = row.GetValueOrDefault("stability_score"),
                    isStable = row.GetValueOrDefault("is_stable"),
                    stratum,
                    survivesStratification = EvidenceBool(row, "survivesStratification", true),
                    significant = EvidenceBool(row, "significant", false),
                    provenanceHandle = EvidenceString(row, "provenanceHandle"),
                    windowDays = row.GetValueOrDefault("window_days"),
                    evidence = row.GetValueOrDefault("evidence_json")?.ToString(),
                    honestyCaveat = Caveat,
                    isRenderable = renderable
                };
            });

            return Results.Ok(new { runId = resolvedRun, honestyCaveat = Caveat, results = shaped });
        });

        return app;
    }

    private static bool EvidenceBool(System.Collections.Generic.Dictionary<string, object?> row, string key, bool fallback)
    {
        try { var t = row.GetValueOrDefault("evidence_json")?.ToString(); if (string.IsNullOrWhiteSpace(t)) return fallback;
              using var d = System.Text.Json.JsonDocument.Parse(t);
              return d.RootElement.TryGetProperty(key, out var v) && v.ValueKind != System.Text.Json.JsonValueKind.Null ? v.GetBoolean() : fallback; }
        catch { return fallback; }
    }
    private static string? EvidenceString(System.Collections.Generic.Dictionary<string, object?> row, string key)
    {
        try { var t = row.GetValueOrDefault("evidence_json")?.ToString(); if (string.IsNullOrWhiteSpace(t)) return null;
              using var d = System.Text.Json.JsonDocument.Parse(t);
              return d.RootElement.TryGetProperty(key, out var v) ? v.GetString() : null; }
        catch { return null; }
    }

    private static async Task<List<Dictionary<string, object?>>> ReadAsync(
        NpgsqlDataSource ds, string sql, (string Name, object Value)? p, CancellationToken ct)
    {


        var rows = new List<Dictionary<string, object?>>();
        await using var cmd = ds.CreateCommand(sql);
        if (p.HasValue) cmd.Parameters.AddWithValue(p.Value.Name, p.Value.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}




