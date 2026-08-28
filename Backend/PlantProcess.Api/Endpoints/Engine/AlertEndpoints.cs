using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace PlantProcess.Api.Endpoints.Engine;

/// <summary>
/// M1-06 4th low-code UI (plant-data-log / threshold alerting), backend v0.
/// Rule CRUD + on-demand evaluation (delegates to ppiq_evaluate_alert_rules) +
/// log read. Routing/chemistry rule types and email/webhook delivery are M2.
/// </summary>
public static class AlertEndpoints
{
    private static readonly HashSet<string> AllowedComparators = new(StringComparer.Ordinal)
    {
        ">", ">=", "<", "<=", "="
    };

    public sealed record CreateAlertRuleRequest(
        string RuleName,
        string ParameterCode,
        string Comparator,
        double LimitValue,
        string? Severity);

    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/alerts").WithTags("Alerts").RequireAuthorization();

        group.MapPost("/rules", async (CreateAlertRuleRequest req, [Microsoft.AspNetCore.Mvc.FromServices] NpgsqlDataSource ds, CancellationToken ct) =>
        {
            if (req is null || string.IsNullOrWhiteSpace(req.RuleName))
                return Results.BadRequest(new { error = "rule_name is required" });
            if (string.IsNullOrWhiteSpace(req.ParameterCode))
                return Results.BadRequest(new { error = "parameter_code is required" });
            if (!AllowedComparators.Contains(req.Comparator ?? string.Empty))
                return Results.BadRequest(new { error = "comparator must be one of > >= < <= =" });

            var severity = string.IsNullOrWhiteSpace(req.Severity) ? "Warning" : req.Severity!.Trim();

            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO ppiq_meta.alert_rules (rule_name, parameter_code, comparator, limit_value, severity, is_active) " +
                "VALUES (@name, @code, @cmp, @limit, @sev, true) RETURNING id";
            cmd.Parameters.AddWithValue("name", req.RuleName.Trim());
            cmd.Parameters.AddWithValue("code", req.ParameterCode.Trim());
            cmd.Parameters.AddWithValue("cmp", req.Comparator);
            cmd.Parameters.AddWithValue("limit", req.LimitValue);
            cmd.Parameters.AddWithValue("sev", severity);
            var id = await cmd.ExecuteScalarAsync(ct);
            return Results.Ok(new
            {
                id = id is Guid g ? g : Guid.Empty,
                ruleName = req.RuleName.Trim(),
                parameterCode = req.ParameterCode.Trim(),
                comparator = req.Comparator,
                limitValue = req.LimitValue,
                severity
            });
        });

        group.MapGet("/rules", async ([Microsoft.AspNetCore.Mvc.FromServices] NpgsqlDataSource ds, CancellationToken ct) =>
        {
            var rules = new List<object>();
            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT id, rule_name, parameter_code, comparator, limit_value, severity, is_active, created_at_utc " +
                "FROM ppiq_meta.alert_rules ORDER BY created_at_utc DESC";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                rules.Add(new
                {
                    id = r.GetGuid(0),
                    ruleName = r.GetString(1),
                    parameterCode = r.GetString(2),
                    comparator = r.GetString(3),
                    limitValue = r.GetDouble(4),
                    severity = r.GetString(5),
                    isActive = r.GetBoolean(6),
                    createdAtUtc = r.GetDateTime(7)
                });
            }
            return Results.Ok(rules);
        });

        group.MapDelete("/rules/{id:guid}", async (Guid id, [Microsoft.AspNetCore.Mvc.FromServices] NpgsqlDataSource ds, CancellationToken ct) =>
        {
            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ppiq_meta.alert_rules WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            return Results.Ok(new { deleted = affected });
        });

        group.MapPost("/evaluate", async ([Microsoft.AspNetCore.Mvc.FromServices] NpgsqlDataSource ds, [Microsoft.AspNetCore.Mvc.FromServices] PlantProcess.Api.Observability.IJobLogService jobLog, CancellationToken ct) =>
        {
            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT public.ppiq_evaluate_alert_rules()";
            var scalar = await cmd.ExecuteScalarAsync(ct);
            var logged = scalar is int i ? i : Convert.ToInt32(scalar ?? 0);
            await jobLog.WriteAsync("ALERT_EVAL", "Alert evaluation", null, "Info", logged + " breach(es) logged", new { logged }, ct);
            return Results.Ok(new { logged });
        });

        group.MapGet("/log", async ([Microsoft.AspNetCore.Mvc.FromServices] NpgsqlDataSource ds, CancellationToken ct) =>
        {
            var rows = new List<object>();
            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT l.id, r.rule_name, l.parameter_code, l.material_code, l.observed_value, " +
                "       l.comparator, l.limit_value, l.severity, l.message, l.logged_at_utc " +
                "FROM ppiq_plant.plant_data_log l " +
                "JOIN ppiq_meta.alert_rules r ON r.id = l.alert_rule_id " +
                "ORDER BY l.logged_at_utc DESC LIMIT 200";
            await using var rr = await cmd.ExecuteReaderAsync(ct);
            while (await rr.ReadAsync(ct))
            {
                rows.Add(new
                {
                    id = rr.GetGuid(0),
                    ruleName = rr.GetString(1),
                    parameterCode = rr.GetString(2),
                    materialCode = rr.IsDBNull(3) ? null : rr.GetString(3),
                    observedValue = rr.IsDBNull(4) ? (double?)null : rr.GetDouble(4),
                    comparator = rr.GetString(5),
                    limitValue = rr.GetDouble(6),
                    severity = rr.GetString(7),
                    message = rr.GetString(8),
                    loggedAtUtc = rr.GetDateTime(9)
                });
            }
            return Results.Ok(rows);
        });

        return app;
    }
}