using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PlantProcess.Application.Integration.Security;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

/// <summary>
/// P6-04 / PPIQ-WF-012 — compiles the widget-script grammar (source, aggregate measures,
/// group_by, filter, having, sort, limit) into PARAMETERIZED, read-only SQL over an approved
/// dataset, validated by <see cref="SafeSqlValidator"/>, with a clamped row-limit and a &lt;=30s
/// timeout. Deterministic; user-supplied values are ALWAYS parameterized, identifiers are
/// strictly whitelisted, and the emitted SQL is re-checked by SafeSqlValidator.
/// </summary>
public static class WidgetScriptSqlCompiler
{
    public const int DefaultRowLimit = 1000;
    public const int MaxRowLimit = 50000;
    public const int MaxTimeoutSeconds = 30;

    private static readonly Regex Ident = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex SourceRef = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);
    private static readonly Regex MeasureRe = new(
        @"^(?<agg>avg|mean|sum|count|min|max)\(\s*(?<col>\*|[A-Za-z_][A-Za-z0-9_]*)\s*\)(\s+as\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FilterRe = new(
        @"^(?<col>[A-Za-z_][A-Za-z0-9_]*)\s+(?<op>in|not\s+in|like|=|<>|!=|<=|>=|<|>)\s+(?<val>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HavingRe = new(
        @"^(?<agg>avg|mean|sum|count|min|max)\(\s*(?<col>\*|[A-Za-z_][A-Za-z0-9_]*)\s*\)\s*(?<op>=|<>|!=|<=|>=|<|>)\s*(?<val>-?\d+(\.\d+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static WidgetScriptCompileResult Compile(string? script, int? requestedRowLimit = null, int? requestedTimeoutSeconds = null)
    {
        var rowLimit = Math.Clamp(requestedRowLimit ?? DefaultRowLimit, 1, MaxRowLimit);
        var timeout = Math.Clamp(requestedTimeoutSeconds ?? MaxTimeoutSeconds, 1, MaxTimeoutSeconds);

        if (string.IsNullOrWhiteSpace(script))
            return Fail("MissingValue", "Widget script is required.", rowLimit, timeout);

        var tokens = ParseTokens(script);

        var source = Single(tokens, "source");
        if (string.IsNullOrWhiteSpace(source))
            return Fail("MissingValue", "source is required.", rowLimit, timeout);
        if (!SourceRef.IsMatch(source))
            return Fail("InvalidIdentifier", $"source '{source}' is not a valid identifier.", rowLimit, timeout);

        var measureRaw = Repeated(tokens, "measure", "measureCode");
        if (measureRaw.Count == 0)
            return Fail("MissingValue", "at least one measure is required.", rowLimit, timeout);

        var selectParts = new List<string>();
        var groupCols = new List<string>();
        foreach (var g in Repeated(tokens, "group_by", "groupBy", "dimension", "dimensionCode")
                     .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (!Ident.IsMatch(g))
                return Fail("InvalidIdentifier", $"group_by column '{g}' is not a valid identifier.", rowLimit, timeout);
            if (!groupCols.Contains(g)) groupCols.Add(g);
        }
        selectParts.AddRange(groupCols);

        var measureIndex = 0;
        foreach (var m in measureRaw)
        {
            var match = MeasureRe.Match(m.Trim());
            if (!match.Success)
                return Fail("InvalidGrammar", $"measure '{m}' must be <agg>(<column>) with agg in avg/sum/count/min/max.", rowLimit, timeout);
            var agg = match.Groups["agg"].Value.ToLowerInvariant();
            if (agg == "mean") agg = "avg";
            var col = match.Groups["col"].Value;
            if (col != "*" && !Ident.IsMatch(col))
                return Fail("InvalidIdentifier", $"measure column '{col}' is not a valid identifier.", rowLimit, timeout);
            if (col == "*" && agg != "count")
                return Fail("InvalidGrammar", "'*' is only valid with count().", rowLimit, timeout);
            var alias = match.Groups["alias"].Success ? match.Groups["alias"].Value : $"m{++measureIndex}";
            selectParts.Add($"{agg}({col}) AS {alias}");
        }

        var parameters = new List<WidgetScriptParameter>();
        var whereParts = new List<string>();
        var p = 0;
        foreach (var f in Repeated(tokens, "filter", "where"))
        {
            var match = FilterRe.Match(f.Trim());
            if (!match.Success)
                return Fail("InvalidGrammar", $"filter '{f}' must be '<column> <operator> <value>'.", rowLimit, timeout);
            var col = match.Groups["col"].Value;
            if (!Ident.IsMatch(col))
                return Fail("InvalidIdentifier", $"filter column '{col}' is not a valid identifier.", rowLimit, timeout);
            var op = Regex.Replace(match.Groups["op"].Value.Trim(), @"\s+", " ").ToLowerInvariant();
            var rawVal = match.Groups["val"].Value.Trim();

            if (op is "in" or "not in")
            {
                var items = ParseList(rawVal);
                if (items.Count == 0)
                    return Fail("InvalidGrammar", $"filter '{f}' IN list is empty.", rowLimit, timeout);
                var pname = $"@p{p++}";
                parameters.Add(new WidgetScriptParameter(pname, items.Select(Coerce).ToArray()));
                whereParts.Add(op == "in" ? $"{col} = ANY({pname})" : $"NOT ({col} = ANY({pname}))");
            }
            else
            {
                var pname = $"@p{p++}";
                parameters.Add(new WidgetScriptParameter(pname, Coerce(StripQuotes(rawVal))));
                var sqlOp = op switch { "!=" => "<>", "like" => "LIKE", _ => op.ToUpperInvariant() };
                whereParts.Add($"{col} {sqlOp} {pname}");
            }
        }

        var havingParts = new List<string>();
        foreach (var h in Repeated(tokens, "having"))
        {
            var match = HavingRe.Match(h.Trim());
            if (!match.Success)
                return Fail("InvalidGrammar", $"having '{h}' must be '<agg>(<column>) <op> <number>'.", rowLimit, timeout);
            var agg = match.Groups["agg"].Value.ToLowerInvariant();
            if (agg == "mean") agg = "avg";
            var col = match.Groups["col"].Value;
            if (col != "*" && !Ident.IsMatch(col))
                return Fail("InvalidIdentifier", $"having column '{col}' is not a valid identifier.", rowLimit, timeout);
            var op = match.Groups["op"].Value == "!=" ? "<>" : match.Groups["op"].Value;
            var pname = $"@p{p++}";
            parameters.Add(new WidgetScriptParameter(pname, decimal.Parse(match.Groups["val"].Value, CultureInfo.InvariantCulture)));
            havingParts.Add($"{agg}({col}) {op} {pname}");
        }

        var orderParts = new List<string>();
        foreach (var s in Repeated(tokens, "sort"))
        {
            var bits = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length == 0 || !Ident.IsMatch(bits[0]))
                return Fail("InvalidIdentifier", $"sort column '{s}' is not a valid identifier.", rowLimit, timeout);
            var dir = bits.Length > 1 && bits[1].Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            orderParts.Add($"{bits[0]} {dir}");
        }

        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(string.Join(", ", selectParts));
        sb.Append(" FROM ").Append(source);
        if (whereParts.Count > 0) sb.Append(" WHERE ").Append(string.Join(" AND ", whereParts));
        if (groupCols.Count > 0) sb.Append(" GROUP BY ").Append(string.Join(", ", groupCols));
        if (havingParts.Count > 0) sb.Append(" HAVING ").Append(string.Join(" AND ", havingParts));
        if (orderParts.Count > 0) sb.Append(" ORDER BY ").Append(string.Join(", ", orderParts));
        sb.Append(" LIMIT ").Append(rowLimit);
        var sql = sb.ToString();

        var safety = SafeSqlValidator.Validate(sql);
        if (!safety.IsValid)
            return new WidgetScriptCompileResult(false, sql, parameters, rowLimit, timeout, "UnsafeSql",
                "Compiled SQL failed SafeSqlValidator: " + string.Join("; ", safety.Errors));

        return new WidgetScriptCompileResult(true, sql, parameters, rowLimit, timeout, null, null);
    }

    private static WidgetScriptCompileResult Fail(string code, string msg, int rowLimit, int timeout) =>
        new(false, null, Array.Empty<WidgetScriptParameter>(), rowLimit, timeout, code, msg);

    private static List<(string Key, string Value)> ParseTokens(string script)
    {
        var result = new List<(string, string)>();
        foreach (var rawLine in script.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (key.Length > 0) result.Add((key, val));
        }
        return result;
    }

    private static string? Single(IEnumerable<(string Key, string Value)> tokens, string key) =>
        tokens.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static List<string> Repeated(IEnumerable<(string Key, string Value)> tokens, params string[] keys) =>
        tokens.Where(t => keys.Any(k => t.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
              .Select(t => t.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    private static List<string> ParseList(string raw)
    {
        var inner = raw.Trim().TrimStart('[', '(').TrimEnd(']', ')');
        return inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(StripQuotes).ToList();
    }

    private static string StripQuotes(string v)
    {
        v = v.Trim();
        if (v.Length >= 2 && ((v[0] == '\'' && v[^1] == '\'') || (v[0] == '"' && v[^1] == '"')))
            return v[1..^1];
        return v;
    }

    private static object Coerce(string v)
    {
        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        if (bool.TryParse(v, out var b)) return b;
        return v;
    }
}

public sealed record WidgetScriptParameter(string Name, object Value);

public sealed record WidgetScriptCompileResult(
    bool IsValid,
    string? Sql,
    IReadOnlyList<WidgetScriptParameter> Parameters,
    int RowLimit,
    int TimeoutSeconds,
    string? ErrorCode,
    string? Error);