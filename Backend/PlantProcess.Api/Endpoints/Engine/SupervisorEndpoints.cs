using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace PlantProcess.Api.Endpoints.Engine;

/// <summary>
/// M1-05 Supervisor v0 (journey step 14, honest minimal). A real review job that
/// reads ml_correlation_results_v2 + run history and writes ONE supervisor report
/// to the knowledge base via ppiq_ml_upsert_kb_item. It NEVER changes a job
/// configuration automatically - automatic tuning is the M2 keystone.
/// </summary>
public static class SupervisorEndpoints
{
    public static IEndpointRouteBuilder MapSupervisorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/supervisor").WithTags("Supervisor").RequireAuthorization();

        group.MapPost("/run", async ([FromServices] NpgsqlDataSource ds, [FromServices] PlantProcess.Api.Observability.IJobLogService jobLog, CancellationToken ct) =>
        {
            var report = await GenerateReportAsync(ds, ct);
            await jobLog.WriteAsync("SUPERVISOR", "Supervisor review", null, "Info", "Supervisor review completed", report, ct);
            return Results.Ok(report);
        });

        group.MapGet("/reports", async ([FromServices] NpgsqlDataSource ds, CancellationToken ct) =>
        {
            var reports = await ListReportsAsync(ds, ct);
            return Results.Ok(reports);
        });

        return app;
    }

    private static async Task<object> GenerateReportAsync(NpgsqlDataSource ds, CancellationToken ct)
    {
        await using var conn = await ds.OpenConnectionAsync(ct);

        Guid? runId = null;
        var windowDays = 0;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT id, window_days FROM public.ml_correlation_compute_runs " +
                "WHERE lower(status) IN ('completed','succeeded','success') " +
                "ORDER BY coalesce(completed_at_utc, started_at_utc) DESC LIMIT 1";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                runId = r.GetGuid(0);
                windowDays = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            }
        }

        var total = 0;
        var significant = 0;
        var topDrivers = new List<string>();
        if (runId.HasValue)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT feature_key, outcome_key, effect_size, q_value FROM public.ml_correlation_results_v2 " +
                "WHERE compute_run_id = @run AND coalesce(method,'NotApplicable') <> 'NotApplicable' AND coalesce(sample_size,0) > 0 " +
                "ORDER BY abs(coalesce(effect_size,0)) DESC";
            cmd.Parameters.AddWithValue("run", runId.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                total++;
                var q = r.IsDBNull(3) ? (double?)null : r.GetDouble(3);
                if (q.HasValue && q.Value < 0.05) significant++;
                if (topDrivers.Count < 3)
                {
                    var feat = r.IsDBNull(0) ? "?" : r.GetString(0);
                    var outc = r.IsDBNull(1) ? "?" : r.GetString(1);
                    var eff = r.IsDBNull(2) ? 0d : r.GetDouble(2);
                    var qtxt = q.HasValue ? q.Value.ToString("0.####") : "n/a";
                    topDrivers.Add($"{feat} -> {outc} (effect {eff:0.###}, q {qtxt})");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Supervisor review generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.");
        if (runId.HasValue)
        {
            sb.AppendLine($"Latest analysis run covered a {windowDays}-day window and produced {total} evaluated associations, of which {significant} were significant (q < 0.05).");
            if (topDrivers.Count > 0)
            {
                sb.AppendLine("Top associations:");
                foreach (var d in topDrivers) sb.AppendLine("  - " + d);
            }
            sb.AppendLine(significant == 0
                ? "Recommendation: no significant drivers this cycle; widen the observation window or import more history before the next run."
                : "Recommendation: keep the current window; the strongest association is stable enough to review with a process engineer.");
        }
        else
        {
            sb.AppendLine("No completed analysis run found yet. Run an analysis job first, then re-run the supervisor.");
        }
        sb.AppendLine("NOTE (v0): this report is a read-only review. No job configuration was changed automatically; automatic tuning is a later release.");

        var title = $"Supervisor report {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        var body = sb.ToString();
        var itemKey = $"supervisor-report-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        var reportId = Guid.Empty;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT public.ppiq_ml_upsert_kb_item(@key, 'SUPERVISOR_REPORT', @title, @body, '[]'::jsonb)";
            cmd.Parameters.AddWithValue("key", itemKey);
            cmd.Parameters.AddWithValue("title", title);
            cmd.Parameters.AddWithValue("body", body);
            var scalar = await cmd.ExecuteScalarAsync(ct);
            if (scalar is Guid g) reportId = g;
        }

        return new
        {
            id = reportId,
            itemKey,
            title,
            body,
            findings = total,
            significant
        };
    }

    private static async Task<List<object>> ListReportsAsync(NpgsqlDataSource ds, CancellationToken ct)
    {
        var list = new List<object>();
        await using var conn = await ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, item_key, title, body, created_at_utc FROM public.ml_knowledge_base_items " +
            "WHERE item_type = 'SUPERVISOR_REPORT' AND is_deleted = false " +
            "ORDER BY created_at_utc DESC LIMIT 20";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new
            {
                id = r.GetGuid(0),
                itemKey = r.GetString(1),
                title = r.GetString(2),
                body = r.GetString(3),
                createdAtUtc = r.GetDateTime(4)
            });
        }
        return list;
    }
}