using System.Text;
using System.Text.RegularExpressions;

namespace PlantProcessIQ.Application.Sql;

// ---------- typed error taxonomy the HMI renders precisely ----------
public enum SqlViewErrorKind { NoSuchView, NoSuchColumn, InvalidAggregateForType, AmbiguousJoinKey, NotReadOnly, PlanScansTooMuch }

public abstract record SqlViewError(SqlViewErrorKind Kind, string Message);
public sealed record NoSuchView(string View)
    : SqlViewError(SqlViewErrorKind.NoSuchView, $"No such table or view: '{View}'.");
public sealed record NoSuchColumn(string Column, string? View = null)
    : SqlViewError(SqlViewErrorKind.NoSuchColumn, View is null ? $"No such column: '{Column}'." : $"No such column: '{View}.{Column}'.");
public sealed record InvalidAggregateForType(string Aggregate, string Column, string Type)
    : SqlViewError(SqlViewErrorKind.InvalidAggregateForType, $"Aggregate {Aggregate}() is not valid for column '{Column}' of type {Type}.");
public sealed record AmbiguousJoinKey(string Left, string Right)
    : SqlViewError(SqlViewErrorKind.AmbiguousJoinKey, $"No shared join key between '{Left}' and '{Right}'.");
public sealed record NotReadOnly(string Reason)
    : SqlViewError(SqlViewErrorKind.NotReadOnly, $"Statement is not read-only: {Reason}.");
public sealed record PlanScansTooMuch(string Table, long EstRows, long Cap)
    : SqlViewError(SqlViewErrorKind.PlanScansTooMuch, $"Sequential scan on '{Table}' estimates {EstRows} rows (cap {Cap}).");

public sealed class SqlViewValidation
{
    public List<SqlViewError> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

// ---------- catalogue + execution ports ----------
public enum CatalogType { Text, Numeric, Integer, Boolean, Timestamp, Other }
public sealed record CatalogColumn(string Name, CatalogType Type);
public sealed record CatalogRelation(string Schema, string Name, IReadOnlyList<CatalogColumn> Columns)
{
    public string Qualified => $"{Schema}.{Name}";
    public CatalogColumn? Column(string n) => Columns.FirstOrDefault(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase));
}
public interface ICatalog { Task<CatalogRelation?> ResolveAsync(string relation, CancellationToken ct = default); }

public sealed record ExplainOutcome(bool Ok, IReadOnlyList<(string Table, long Rows)> SeqScans);
public interface IExplainRunner { Task<ExplainOutcome> ExplainAsync(string readOnlySql, int statementTimeoutMs, CancellationToken ct = default); }

/// INTEGRATE: adapt to your existing SafeSqlValidator.
public interface ISafeSqlValidator { bool IsReadOnly(string sql, out string reason); }
public interface ISqlViewStore { Task PublishAsync(string viewName, string createOrReplaceSql, CancellationToken ct = default); }

public sealed record RowCapOptions(int RowCap = 50_000, int StatementTimeoutMs = 5_000);

// ---------- aggregate validity by column type ----------
public static class AggregateRules
{
    public static bool IsValid(string agg, CatalogType t) => agg.ToUpperInvariant() switch
    {
        "COUNT" => true,
        "SUM" or "AVG" or "STDDEV" or "VARIANCE" => t is CatalogType.Numeric or CatalogType.Integer,
        "MIN" or "MAX" => t is CatalogType.Numeric or CatalogType.Integer or CatalogType.Timestamp or CatalogType.Text,
        _ => true
    };
}

// ---------- visual model + raw round-trip ----------
public sealed record SelectItem(string Column, string? Aggregate = null, string? Alias = null, string? Relation = null);
public sealed record JoinSpec(string Relation, string LeftColumn, string RightColumn, string Kind = "INNER");
public sealed record FilterSpec(string Column, string Op, string ParamName);

public sealed class SqlViewModel
{
    public required string From { get; init; }
    public List<SelectItem> Select { get; init; } = new();
    public List<JoinSpec> Joins { get; init; } = new();
    public List<FilterSpec> Where { get; init; } = new();
    public List<string> GroupBy { get; init; } = new();

    public string ToSql()
    {
        string Col(string? rel, string c) => rel is null ? Q(c) : $"{rel}.{Q(c)}";
        var cols = Select.Count == 0
            ? new List<string> { "*" }
            : Select.Select(s => (s.Aggregate is null ? Col(s.Relation, s.Column) : $"{s.Aggregate.ToUpperInvariant()}({Col(s.Relation, s.Column)})")
                               + (s.Alias is null ? "" : $" AS {Q(s.Alias)}")).ToList();

        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(string.Join(", ", cols)).Append(" FROM ").Append(From);
        foreach (var j in Joins)
            sb.Append($" {j.Kind} JOIN {j.Relation} ON {j.Relation}.{Q(j.RightColumn)} = {From}.{Q(j.LeftColumn)}");
        if (Where.Count > 0)
            sb.Append(" WHERE ").Append(string.Join(" AND ", Where.Select(w => $"{Q(w.Column)} {w.Op} @{w.ParamName}")));
        if (GroupBy.Count > 0)
            sb.Append(" GROUP BY ").Append(string.Join(", ", GroupBy.Select(Q)));
        return sb.ToString();
    }

    /// Parses the structured SQL ToSql() emits back into a model (visual<->raw round-trip for builder-authored views).
    /// Free-form hand-edited SQL is validated by SafeSql + catalogue + EXPLAIN but shown in raw mode rather than re-parsed.
    public static SqlViewModel FromSql(string sql)
    {
        var m = Regex.Match(sql.Trim(),
            @"^SELECT\s+(?<cols>.+?)\s+FROM\s+(?<from>[^\s]+)(?<joins>(?:\s+(?:INNER|LEFT|RIGHT)\s+JOIN\s+[^\s]+\s+ON\s+[^\s]+\s*=\s*[^\s]+)*)" +
            @"(?:\s+WHERE\s+(?<where>.+?))?(?:\s+GROUP\s+BY\s+(?<group>.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) throw new FormatException("SQL is not in the builder-generated form (open it in raw mode).");

        var model = new SqlViewModel { From = m.Groups["from"].Value.Trim() };

        foreach (var raw in SplitTop(m.Groups["cols"].Value))
        {
            var part = raw.Trim();
            string? alias = null;
            var am = Regex.Match(part, @"\s+AS\s+""?(?<a>[^""]+)""?$", RegexOptions.IgnoreCase);
            if (am.Success) { alias = am.Groups["a"].Value; part = part[..am.Index].Trim(); }
            var ag = Regex.Match(part, @"^(?<agg>[A-Za-z]+)\((?<inner>.+)\)$");
            if (ag.Success)
                model.Select.Add(new SelectItem(Unq(LastSeg(ag.Groups["inner"].Value)), ag.Groups["agg"].Value.ToUpperInvariant(), alias, RelOf(ag.Groups["inner"].Value)));
            else
                model.Select.Add(new SelectItem(Unq(LastSeg(part)), null, alias, RelOf(part)));
        }

        foreach (Match j in Regex.Matches(m.Groups["joins"].Value,
                     @"(?<kind>INNER|LEFT|RIGHT)\s+JOIN\s+(?<rel>[^\s]+)\s+ON\s+(?<l>[^\s]+)\s*=\s*(?<r>[^\s]+)", RegexOptions.IgnoreCase))
            model.Joins.Add(new JoinSpec(j.Groups["rel"].Value, Unq(LastSeg(j.Groups["r"].Value)), Unq(LastSeg(j.Groups["l"].Value)), j.Groups["kind"].Value.ToUpperInvariant()));

        if (m.Groups["group"].Success)
            foreach (var g in SplitTop(m.Groups["group"].Value)) model.GroupBy.Add(Unq(g.Trim()));

        return model;
    }

    private static IEnumerable<string> SplitTop(string s)
    {
        int depth = 0, start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') depth--;
            else if (s[i] == ',' && depth == 0) { yield return s[start..i]; start = i + 1; }
        }
        yield return s[start..];
    }
    private static string LastSeg(string id) => id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
    private static string? RelOf(string id) => id.Contains('.') ? id[..id.LastIndexOf('.')] : null;
    private static string Unq(string id) => id.Trim().Trim('"');
    private static string Q(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";
}

// ---------- structural validator (resolve identifiers, aggregates, join keys) ----------
public sealed class SqlViewValidator
{
    private readonly ICatalog _catalog;
    public SqlViewValidator(ICatalog catalog) => _catalog = catalog;

    public async Task<SqlViewValidation> ValidateAsync(SqlViewModel m, CancellationToken ct = default)
    {
        var v = new SqlViewValidation();
        var relations = new Dictionary<string, CatalogRelation>(StringComparer.OrdinalIgnoreCase);

        async Task<CatalogRelation?> Resolve(string rel)
        {
            if (relations.TryGetValue(rel, out var cached)) return cached;
            var r = await _catalog.ResolveAsync(rel, ct);
            if (r is null) { v.Errors.Add(new NoSuchView(rel)); return null; }
            relations[rel] = r; relations[r.Name] = r;
            return r;
        }

        var fromRel = await Resolve(m.From);
        foreach (var j in m.Joins) await Resolve(j.Relation);

        foreach (var s in m.Select)
        {
            var rel = s.Relation is not null && relations.TryGetValue(s.Relation, out var rr) ? rr : fromRel;
            if (rel is null) continue;
            var col = rel.Column(s.Column);
            if (col is null) { v.Errors.Add(new NoSuchColumn(s.Column, rel.Name)); continue; }
            if (s.Aggregate is not null && !AggregateRules.IsValid(s.Aggregate, col.Type))
                v.Errors.Add(new InvalidAggregateForType(s.Aggregate.ToUpperInvariant(), s.Column, col.Type.ToString()));
        }

        foreach (var j in m.Joins)
        {
            relations.TryGetValue(j.Relation, out var right);
            if (fromRel is null || right is null) continue;
            var lc = fromRel.Column(j.LeftColumn);
            var rc = right.Column(j.RightColumn);
            if (lc is null || rc is null || lc.Type != rc.Type)
                v.Errors.Add(new AmbiguousJoinKey(fromRel.Name, right.Name));
        }
        return v;
    }
}

// ---------- publish pipeline: SafeSql -> structural -> EXPLAIN guard -> publish ----------
public sealed class SqlViewPublishService
{
    private readonly ISafeSqlValidator _safe;
    private readonly SqlViewValidator _validator;
    private readonly IExplainRunner _explain;
    private readonly ISqlViewStore _store;
    private readonly RowCapOptions _caps;

    public SqlViewPublishService(ISafeSqlValidator safe, SqlViewValidator validator, IExplainRunner explain, ISqlViewStore store, RowCapOptions caps)
    { _safe = safe; _validator = validator; _explain = explain; _store = store; _caps = caps; }

    public async Task<SqlViewValidation> ValidateAndPublishAsync(string viewName, SqlViewModel model, bool publish, CancellationToken ct = default)
    {
        var v = new SqlViewValidation();
        string sql = model.ToSql();

        if (!_safe.IsReadOnly(sql, out var reason)) { v.Errors.Add(new NotReadOnly(reason)); return v; }

        var structural = await _validator.ValidateAsync(model, ct);
        v.Errors.AddRange(structural.Errors);
        if (!v.IsValid) return v;

        string capped = ApplyRowCap(sql, _caps.RowCap);
        var plan = await _explain.ExplainAsync(capped, _caps.StatementTimeoutMs, ct);
        foreach (var (tbl, rows) in plan.SeqScans)
            if (rows > _caps.RowCap) v.Errors.Add(new PlanScansTooMuch(tbl, rows, _caps.RowCap));
        if (!v.IsValid) return v;

        if (publish) await _store.PublishAsync(viewName, $"CREATE OR REPLACE VIEW {viewName} AS {capped}", ct);
        return v;
    }

    private static string ApplyRowCap(string sql, int cap) =>
        Regex.IsMatch(sql, @"\blimit\b", RegexOptions.IgnoreCase) ? sql : $"{sql} LIMIT {cap}";
}