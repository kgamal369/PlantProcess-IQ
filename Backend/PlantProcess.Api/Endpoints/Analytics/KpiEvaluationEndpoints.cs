using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using PlantProcess.Api.Analytics;

namespace PlantProcess.Api.Endpoints.Analytics;

public sealed record KpiTargetRequest(double TargetValue, string? AlertDirection, double? WarningAt, double? CriticalAt, string? Unit);

/// <summary>T-025: KPI evaluation + per-tenant targets + alert history.</summary>
public static class KpiEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapKpiEvaluationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kpis")
            .WithTags("KPI Evaluation")
            .RequireAuthorization();

        group.MapPost("/{kpiCode}/evaluate", async (string kpiCode, ClaimsPrincipal user, KpiEvaluationService svc, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId))
                return Results.BadRequest(new { error = "no_tenant", message = "No tenant_id claim on the caller." });

            var outcome = await svc.EvaluateAsync(tenantId, kpiCode, ct);
            if (!outcome.Success)
            {
                var status = outcome.ErrorCode == KpiEvalErrorCode.KpiNotFound ? StatusCodes.Status404NotFound
                           : outcome.ErrorCode == KpiEvalErrorCode.QueryFailed ? StatusCodes.Status422UnprocessableEntity
                           : StatusCodes.Status400BadRequest;
                return Results.Json(new { error = outcome.ErrorCode.ToString(), message = outcome.ErrorMessage, kpiCode }, statusCode: status);
            }

            return Results.Ok(new
            {
                kpiCode = outcome.KpiCode,
                value = outcome.Value,
                unit = outcome.Unit,
                severity = outcome.Severity.ToString(),
                alertRaised = outcome.AlertRaised,
                target = outcome.TargetValue,
                warningAt = outcome.WarningAt,
                criticalAt = outcome.CriticalAt,
                alertDirection = outcome.AlertDirection,
                metadata = new
                {
                    formula = outcome.Formula,
                    dataset = outcome.Dataset,
                    filters = outcome.Filters,
                    timeWindow = outcome.TimeWindow,
                    refreshedAtUtc = outcome.RefreshedAtUtc,
                    sampleSize = outcome.SampleSize
                }
            });
        });

        group.MapGet("/{kpiCode}/target", async (string kpiCode, ClaimsPrincipal user, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });
            await using var cmd = ds.CreateCommand(
                "SELECT target_value, alert_direction, warning_at, critical_at, unit, effective_from_utc " +
                "FROM kpi_targets WHERE tenant_id = @t AND kpi_code = @c AND is_active = true LIMIT 1");
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("c", kpiCode);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return Results.NotFound(new { kpiCode, message = "No active target for this tenant." });
            return Results.Ok(new
            {
                kpiCode,
                targetValue = r.GetDouble(0),
                alertDirection = r.GetString(1),
                warningAt = await r.IsDBNullAsync(2, ct) ? (double?)null : r.GetDouble(2),
                criticalAt = await r.IsDBNullAsync(3, ct) ? (double?)null : r.GetDouble(3),
                unit = await r.IsDBNullAsync(4, ct) ? null : r.GetString(4),
                effectiveFromUtc = r.GetDateTime(5)
            });
        });

        group.MapPut("/{kpiCode}/target", async (string kpiCode, KpiTargetRequest req, ClaimsPrincipal user, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });
            var direction = string.IsNullOrWhiteSpace(req.AlertDirection) ? "BelowTargetIsBad" : req.AlertDirection!.Trim();
            if (direction is not ("AboveTargetIsBad" or "BelowTargetIsBad"))
                return Results.BadRequest(new { error = "invalid_direction", supported = new[] { "AboveTargetIsBad", "BelowTargetIsBad" } });

            await using (var deact = ds.CreateCommand(
                "UPDATE kpi_targets SET is_active = false, updated_at_utc = now() WHERE tenant_id = @t AND kpi_code = @c AND is_active = true"))
            {
                deact.Parameters.AddWithValue("t", tenantId);
                deact.Parameters.AddWithValue("c", kpiCode);
                await deact.ExecuteNonQueryAsync(ct);
            }
            await using (var ins = ds.CreateCommand(
                "INSERT INTO kpi_targets (tenant_id, kpi_code, target_value, alert_direction, warning_at, critical_at, unit) " +
                "VALUES (@t, @c, @v, @d, @w, @cr, @u)"))
            {
                ins.Parameters.AddWithValue("t", tenantId);
                ins.Parameters.AddWithValue("c", kpiCode);
                ins.Parameters.AddWithValue("v", req.TargetValue);
                ins.Parameters.AddWithValue("d", direction);
                ins.Parameters.AddWithValue("w", (object?)req.WarningAt ?? DBNull.Value);
                ins.Parameters.AddWithValue("cr", (object?)req.CriticalAt ?? DBNull.Value);
                ins.Parameters.AddWithValue("u", (object?)req.Unit ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync(ct);
            }
            return Results.Ok(new { kpiCode, targetValue = req.TargetValue, alertDirection = direction });
        });

        group.MapGet("/{kpiCode}/alerts", async (string kpiCode, ClaimsPrincipal user, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });
            var list = new List<object>();
            await using var cmd = ds.CreateCommand(
                "SELECT evaluated_value, target_value, severity, message, created_at_utc " +
                "FROM kpi_evaluation_alerts WHERE tenant_id = @t AND kpi_code = @c ORDER BY created_at_utc DESC LIMIT 50");
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("c", kpiCode);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new
                {
                    evaluatedValue = await r.IsDBNullAsync(0, ct) ? (double?)null : r.GetDouble(0),
                    targetValue = await r.IsDBNullAsync(1, ct) ? (double?)null : r.GetDouble(1),
                    severity = r.GetString(2),
                    message = await r.IsDBNullAsync(3, ct) ? null : r.GetString(3),
                    createdAtUtc = r.GetDateTime(4)
                });
            return Results.Ok(list);
        });

        return app;
    }

    private static bool TryTenant(ClaimsPrincipal user, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var raw = user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tenant");
        return Guid.TryParse(raw, out tenantId);
    }
}