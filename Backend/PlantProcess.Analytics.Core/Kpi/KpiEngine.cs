using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
/// <summary>
/// Read-only SQL validator for KPI SQL-view definitions.
/// PPIQ-T001/T006: replaces the old substring guard with token-boundary validation.
/// </summary>
public static class SafeSqlValidator
{
private static readonly string[] ForbiddenTokens =
[
    "insert", "update", "delete", "drop", "alter", "truncate",
    "grant", "revoke", "exec", "execute", "call", "copy", "merge",
    "create", "vacuum", "analyze", "pg_catalog", "information_schema",
    "pg_read_file", "pg_sleep", "dblink", "xp_cmdshell", "openrowset"
];

public static void Validate(string? sql)
{
    if (string.IsNullOrWhiteSpace(sql))
        throw new KpiFormulaException("KPI SQL view text is empty.");

    var withoutComments = StripSqlComments(sql).Trim();
    var trimmed = RemoveSingleTrailingSemicolon(withoutComments).Trim();

    if (trimmed.Contains(';'))
        throw new KpiFormulaException("KPI SQL view must be a single SELECT/WITH statement.");

    var lowered = Regex.Replace(trimmed.ToLowerInvariant(), @"\s+", " ");

    if (!(lowered.StartsWith("select ") || lowered == "select" || lowered.StartsWith("with ")))
        throw new KpiFormulaException("KPI SQL view must be a read-only SELECT/WITH query.");

    foreach (var token in ForbiddenTokens)
    {
        if (ContainsForbiddenToken(lowered, token))
            throw new KpiFormulaException($"KPI SQL view contains a forbidden token: '{token}'.");
    }
}

private static bool ContainsForbiddenToken(string loweredSql, string token)
{
    var escaped = Regex.Escape(token);
    var pattern = $@"(^|[^a-z0-9_]){escaped}([^a-z0-9_]|$|[ \t\r\n]*\()";

    return Regex.IsMatch(loweredSql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

private static string StripSqlComments(string sql)
{
    var noLine = Regex.Replace(sql, @"--.*?$", string.Empty, RegexOptions.Multiline);
    return Regex.Replace(noLine, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
}

private static string RemoveSingleTrailingSemicolon(string sql)
{
    var trimmed = sql.TrimEnd();

    if (!trimmed.EndsWith(';'))
        return sql;

    var without = trimmed[..^1];

    return without.Contains(';') ? sql : without;
}
}

/// <summary>
/// PPIQ_REALIZATION_T004_BASIC_SQL_GUARD
/// Minimal SELECT-only guard for SQL-view KPIs.
/// This is intentionally conservative: KPI SQL views must be single-statement read-only SELECT/WITH queries.
/// </summary>
/// <summary>
/// PPIQ_REALIZATION_T004_BASIC_SQL_GUARD
/// Minimal SELECT-only guard for SQL-view KPIs.
/// This guard is independent of Application layer to avoid circular dependencies.
/// It is intentionally conservative:
/// - read-only SELECT/WITH only
/// - no multi-statement semicolon outside string literals
/// - forbidden SQL verbs matched by token boundary, not substring
/// - comments stripped with a literal-aware parser, not Regex
/// </summary>
public static class BasicSqlGuard
{
    private static readonly string[] ForbiddenTokens =
    {
        "insert",
        "update",
        "delete",
        "drop",
        "alter",
        "truncate",
        "grant",
        "revoke",
        "exec",
        "execute",
        "call",
        "copy",
        "merge",
        "create"
    };

    private static readonly string[] ForbiddenFragments =
    {
        "pg_",
        "information_schema"
    };

    public static void Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new KpiFormulaException("KPI SQL view text is empty.");

        var stripped = StripSqlComments(sql);
        var trimmed = stripped.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (!(lower.StartsWith("select") || lower.StartsWith("with")))
            throw new KpiFormulaException("KPI SQL view must be a read-only SELECT/WITH query.");

        if (ContainsSemicolonOutsideLiteral(trimmed))
            throw new KpiFormulaException("KPI SQL view must be a single statement (no semicolons).");

        foreach (var token in ForbiddenTokens)
        {
            if (ContainsSqlToken(lower, token))
                throw new KpiFormulaException($"KPI SQL view contains a forbidden token: '{token}'.");
        }

        foreach (var fragment in ForbiddenFragments)
        {
            if (lower.Contains(fragment, StringComparison.Ordinal))
                throw new KpiFormulaException($"KPI SQL view contains a forbidden fragment: '{fragment}'.");
        }
    }

    private static string StripSqlComments(string sql)
    {
        var output = new System.Text.StringBuilder(sql.Length);
        var i = 0;
        var blockDepth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracketIdentifier = false;

        while (i < sql.Length)
        {
            var current = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (blockDepth > 0)
            {
                if (current == '/' && next == '*')
                {
                    blockDepth++;
                    i += 2;
                    continue;
                }

                if (current == '*' && next == '/')
                {
                    blockDepth--;
                    i += 2;
                    continue;
                }

                i++;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && !inBracketIdentifier)
            {
                if (current == '-' && next == '-')
                {
                    while (i < sql.Length && sql[i] != '\r' && sql[i] != '\n')
                        i++;

                    output.Append(' ');
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    blockDepth = 1;
                    i += 2;
                    output.Append(' ');
                    continue;
                }
            }

            if (!inDoubleQuote && !inBracketIdentifier && current == '\'')
            {
                output.Append(current);

                if (inSingleQuote && next == '\'')
                {
                    output.Append(next);
                    i += 2;
                    continue;
                }

                inSingleQuote = !inSingleQuote;
                i++;
                continue;
            }

            if (!inSingleQuote && !inBracketIdentifier && current == '"')
            {
                inDoubleQuote = !inDoubleQuote;
                output.Append(current);
                i++;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && current == '[')
            {
                inBracketIdentifier = true;
                output.Append(current);
                i++;
                continue;
            }

            if (inBracketIdentifier && current == ']')
            {
                inBracketIdentifier = false;
                output.Append(current);
                i++;
                continue;
            }

            output.Append(current);
            i++;
        }

        return output.ToString();
    }

    private static bool ContainsSemicolonOutsideLiteral(string sql)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracketIdentifier = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var current = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (!inDoubleQuote && !inBracketIdentifier && current == '\'')
            {
                if (inSingleQuote && next == '\'')
                {
                    i++;
                    continue;
                }

                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (!inSingleQuote && !inBracketIdentifier && current == '"')
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && current == '[')
            {
                inBracketIdentifier = true;
                continue;
            }

            if (inBracketIdentifier && current == ']')
            {
                inBracketIdentifier = false;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && !inBracketIdentifier && current == ';')
                return true;
        }

        return false;
    }

    private static bool ContainsSqlToken(string lowerSql, string token)
    {
        var index = 0;

        while ((index = lowerSql.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            var beforeOk = index == 0 || !IsIdentifierCharacter(lowerSql[index - 1]);
            var afterIndex = index + token.Length;
            var afterOk = afterIndex >= lowerSql.Length || !IsIdentifierCharacter(lowerSql[afterIndex]);

            if (beforeOk && afterOk)
                return true;

            index += token.Length;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
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
    if (def.Kind == KpiKind.SqlView) { SafeSqlValidator.Validate(def.SqlView); formula = $"SQL:{def.SqlView}"; }
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
