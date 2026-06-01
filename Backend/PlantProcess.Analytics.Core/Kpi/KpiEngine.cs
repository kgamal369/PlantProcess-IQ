using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Primitives;
namespace PlantProcess.Analytics.Core.Kpi;
public enum KpiKind { Formula, SqlView, MappedMeasure }
public enum AlertDirection { AboveTargetIsBad, BelowTargetIsBad }
public enum KpiSeverity { Ok, Warning, Critical }
public sealed record KpiThreshold(double? WarningAt, double? CriticalAt, AlertDirection Direction);
public sealed record KpiDefinition(
string Code,
string Name,
KpiKind Kind,
string? Expression,
string? SqlView,
string? MeasureCode,
string? Unit,
IReadOnlyDictionary<string, double>? TenantTargets,
KpiThreshold? Threshold);
public sealed record KpiResult(
string Code,
double Value,
double? Target,
KpiSeverity Severity,
bool AlertRaised,
AnalysisMetadata Metadata,
string? Message = null);
/// <summary>Minimal SELECT-only guard mirroring SafeSqlValidator intent for SQL-view KPIs.</summary>
public static class BasicSqlGuard
{
private static readonly string[] Forbidden =
{ "insert", "update", "delete", "drop", "alter", "truncate", "grant", "revoke", "exec", "execute", "call", "copy", "merge", "create", "pg_", "information_schema" };
public static void Validate(string? sql)
{
    if (string.IsNullOrWhiteSpace(sql)) throw new KpiFormulaException("KPI SQL view text is empty.");
    var trimmed = sql.Trim();
    var lower = trimmed.ToLowerInvariant();
    if (!(lower.StartsWith("select") || lower.StartsWith("with")))
        throw new KpiFormulaException("KPI SQL view must be a read-only SELECT/WITH query.");
    if (trimmed.Contains(';')) throw new KpiFormulaException("KPI SQL view must be a single statement (no semicolons).");
    foreach (var bad in Forbidden)
        if (lower.Contains(bad)) throw new KpiFormulaException($"KPI SQL view contains a forbidden token: '{bad}'.");
}
}
public sealed class KpiEngine
{
private readonly ExpressionEvaluator _evaluator = new();
/// <summary>Formula KPI: evaluates the expression over named inputs.</summary>
public KpiResult EvaluateFormula(
    KpiDefinition def, string tenantId, IReadOnlyDictionary<string, double> inputs,
    string dataset, IReadOnlyList<string> filters, string timeWindow, DateTimeOffset refreshedAtUtc, int sampleSize)
{
    if (def.Kind != KpiKind.Formula) throw new KpiFormulaException($"KPI '{def.Code}' is not a formula KPI.");
    double value = _evaluator.Evaluate(def.Expression, inputs);
    return Finalize(def, tenantId, value, def.Expression ?? "<formula>", dataset, filters, timeWindow, refreshedAtUtc, sampleSize);
}

/// <summary>SQL-view or mapped-measure KPI: the measured value is computed by the integration layer (validated here).</summary>
public KpiResult EvaluateMeasured(
    KpiDefinition def, string tenantId, double measuredValue,
    string dataset, IReadOnlyList<string> filters, string timeWindow, DateTimeOffset refreshedAtUtc, int sampleSize)
{
    string formula;
    if (def.Kind == KpiKind.SqlView) { BasicSqlGuard.Validate(def.SqlView); formula = $"SQL:{def.SqlView}"; }
    else if (def.Kind == KpiKind.MappedMeasure) { formula = $"MEASURE:{def.MeasureCode}"; }
    else throw new KpiFormulaException($"KPI '{def.Code}' is not a measured KPI.");
    return Finalize(def, tenantId, measuredValue, formula, dataset, filters, timeWindow, refreshedAtUtc, sampleSize);
}

private static KpiResult Finalize(
    KpiDefinition def, string tenantId, double value, string formula,
    string dataset, IReadOnlyList<string> filters, string timeWindow, DateTimeOffset refreshedAtUtc, int sampleSize)
{
    double? target = null;
    if (def.TenantTargets != null && def.TenantTargets.TryGetValue(tenantId, out var t)) target = t;

    var severity = KpiSeverity.Ok;
    if (def.Threshold is { } th)
    {
        bool above = th.Direction == AlertDirection.AboveTargetIsBad;
        if (th.CriticalAt is { } crit && Breached(value, crit, above)) severity = KpiSeverity.Critical;
        else if (th.WarningAt is { } warn && Breached(value, warn, above)) severity = KpiSeverity.Warning;
    }

    var meta = new AnalysisMetadata(formula, dataset, filters, timeWindow, refreshedAtUtc, sampleSize, def.Unit);
    return new KpiResult(def.Code, value, target, severity, severity != KpiSeverity.Ok, meta);
}

private static bool Breached(double value, double bound, bool aboveIsBad) => aboveIsBad ? value >= bound : value <= bound;
}