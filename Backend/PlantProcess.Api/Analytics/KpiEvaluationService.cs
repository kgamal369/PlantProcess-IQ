using Npgsql;
using PlantProcess.Application.Integration.Security;

namespace PlantProcess.Api.Analytics;

public enum KpiEvalErrorCode { None, KpiNotFound, ViewNotResolved, InvalidExpression, QueryFailed }
public enum KpiSeverityLevel { Ok, Warning, Critical, NoTarget }

public sealed record KpiEvaluationOutcome(
    bool Success, KpiEvalErrorCode ErrorCode, string? ErrorMessage,
    string KpiCode, double? Value, string? Unit,
    double? TargetValue, double? WarningAt, double? CriticalAt, string? AlertDirection,
    KpiSeverityLevel Severity, bool AlertRaised,
    string Formula, string Dataset, IReadOnlyList<string> Filters,
    string TimeWindow, DateTimeOffset RefreshedAtUtc, int SampleSize);

/// <summary>T-025: evaluates a KPI's measure against its source view and the caller tenant's target.</summary>
public sealed class KpiEvaluationService
{
    private readonly NpgsqlDataSource _ds;
    public KpiEvaluationService(NpgsqlDataSource ds) => _ds = ds;

    public async Task<KpiEvaluationOutcome> EvaluateAsync(Guid tenantId, string kpiCode, CancellationToken ct)
    {
        string valueExpr; string? unit; string? filterExpr; Guid? viewDefId;

        await using (var cmd = _ds.CreateCommand(
            "SELECT value_expression, unit, filter_expression, schema_view_definition_id " +
            "FROM kpi_definitions WHERE kpi_code = @c AND is_deleted = false AND is_active = true LIMIT 1"))
        {
            cmd.Parameters.AddWithValue("c", kpiCode);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return Fail(KpiEvalErrorCode.KpiNotFound, $"KPI '{kpiCode}' not found or inactive.", kpiCode);
            valueExpr  = r.GetString(0);
            unit       = await r.IsDBNullAsync(1, ct) ? null : r.GetString(1);
            filterExpr = await r.IsDBNullAsync(2, ct) ? null : r.GetString(2);
            viewDefId  = await r.IsDBNullAsync(3, ct) ? (Guid?)null : r.GetGuid(3);
        }

        if (viewDefId is null)
            return Fail(KpiEvalErrorCode.ViewNotResolved, "KPI has no schema view; a source relation is required to evaluate.", kpiCode);

        string relation;
        await using (var cmd = _ds.CreateCommand(
            "SELECT physical_schema, physical_view_name FROM schema_view_definitions WHERE id = @id LIMIT 1"))
        {
            cmd.Parameters.AddWithValue("id", viewDefId.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return Fail(KpiEvalErrorCode.ViewNotResolved, "Schema view definition not found.", kpiCode);
            var schema = await r.IsDBNullAsync(0, ct) ? "public" : r.GetString(0);
            var view   = r.GetString(1);
            relation = $"\"{schema.Replace("\"", "")}\".\"{view.Replace("\"", "")}\"";
        }

        var filterClause = string.IsNullOrWhiteSpace(filterExpr) ? "" : $" WHERE {filterExpr}";
        var sql = $"SELECT ({valueExpr})::double precision AS kpi_value FROM {relation}{filterClause}";

        var safety = SafeSqlValidator.Validate(sql);
        if (!safety.IsValid)
            return Fail(KpiEvalErrorCode.InvalidExpression, "KPI expression failed SQL safety validation.", kpiCode);

        double? value;
        try
        {
            await using var cmd = _ds.CreateCommand(sql);
            cmd.CommandTimeout = 15;
            var scalar = await cmd.ExecuteScalarAsync(ct);
            value = scalar is null or DBNull ? (double?)null : Convert.ToDouble(scalar);
        }
        catch (PostgresException ex)
        {
            return Fail(KpiEvalErrorCode.QueryFailed, $"KPI query failed: {ex.MessageText}", kpiCode);
        }

        double? target = null, warnAt = null, critAt = null;
        var direction = "BelowTargetIsBad";
        await using (var cmd = _ds.CreateCommand(
            "SELECT target_value, warning_at, critical_at, alert_direction FROM kpi_targets " +
            "WHERE tenant_id = @t AND kpi_code = @c AND is_active = true " +
            "AND (effective_to_utc IS NULL OR effective_to_utc > now()) ORDER BY effective_from_utc DESC LIMIT 1"))
        {
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("c", kpiCode);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                target    = r.GetDouble(0);
                warnAt    = await r.IsDBNullAsync(1, ct) ? null : r.GetDouble(1);
                critAt    = await r.IsDBNullAsync(2, ct) ? null : r.GetDouble(2);
                direction = await r.IsDBNullAsync(3, ct) ? "BelowTargetIsBad" : r.GetString(3);
            }
        }

        var severity = (target is not null && value is not null)
            ? Classify(value.Value, warnAt, critAt, direction)
            : KpiSeverityLevel.NoTarget;

        var alertRaised = false;
        if (severity is KpiSeverityLevel.Warning or KpiSeverityLevel.Critical)
        {
            await using var cmd = _ds.CreateCommand(
                "INSERT INTO kpi_evaluation_alerts (tenant_id, kpi_code, evaluated_value, target_value, severity, message) " +
                "VALUES (@t, @c, @v, @tg, @s, @m)");
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("c", kpiCode);
            cmd.Parameters.AddWithValue("v", (object?)value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tg", (object?)target ?? DBNull.Value);
            cmd.Parameters.AddWithValue("s", severity.ToString());
            cmd.Parameters.AddWithValue("m", $"KPI '{kpiCode}' = {value} breached {severity} threshold (target {target}).");
            await cmd.ExecuteNonQueryAsync(ct);
            alertRaised = true;
        }

        var filters = string.IsNullOrWhiteSpace(filterExpr) ? Array.Empty<string>() : new[] { filterExpr! };
        return new KpiEvaluationOutcome(true, KpiEvalErrorCode.None, null, kpiCode, value, unit,
            target, warnAt, critAt, direction, severity, alertRaised,
            valueExpr, relation, filters, "current", DateTimeOffset.UtcNow, 1);
    }

    private static KpiSeverityLevel Classify(double value, double? warnAt, double? critAt, string direction)
    {
        bool Worse(double a, double b) => direction == "AboveTargetIsBad" ? a >= b : a <= b;
        if (critAt is not null && Worse(value, critAt.Value)) return KpiSeverityLevel.Critical;
        if (warnAt is not null && Worse(value, warnAt.Value)) return KpiSeverityLevel.Warning;
        return KpiSeverityLevel.Ok;
    }

    private static KpiEvaluationOutcome Fail(KpiEvalErrorCode code, string msg, string kpiCode) =>
        new(false, code, msg, kpiCode, null, null, null, null, null, null,
            KpiSeverityLevel.NoTarget, false, "", "", Array.Empty<string>(), "current", DateTimeOffset.UtcNow, 0);
}