using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using PlantProcessIQ.Application.Sql;

namespace PlantProcessIQ.Infrastructure.Sql;

/// Resolves tables/views and column types from information_schema.
public sealed class NpgsqlCatalog : ICatalog
{
    private readonly string _connString;
    public NpgsqlCatalog(string connString) => _connString = connString;

    public async Task<CatalogRelation?> ResolveAsync(string relation, CancellationToken ct = default)
    {
        var (schema, name) = Split(relation);
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        const string sql = @"
            SELECT c.column_name, c.data_type
            FROM information_schema.columns c
            WHERE c.table_schema = @s AND c.table_name = @n
            ORDER BY c.ordinal_position;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("s", schema);
        cmd.Parameters.AddWithValue("n", name);

        var cols = new List<CatalogColumn>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                cols.Add(new CatalogColumn(r.GetString(0), Map(r.GetString(1))));

        return cols.Count == 0 ? null : new CatalogRelation(schema, name, cols);
    }

    private static (string schema, string name) Split(string rel)
    {
        int i = rel.IndexOf('.');
        return i < 0 ? ("public", rel) : (rel[..i], rel[(i + 1)..]);
    }

    private static CatalogType Map(string t) => t switch
    {
        "integer" or "bigint" or "smallint" => CatalogType.Integer,
        "numeric" or "real" or "double precision" or "money" => CatalogType.Numeric,
        "boolean" => CatalogType.Boolean,
        var x when x.StartsWith("timestamp") || x == "date" => CatalogType.Timestamp,
        "text" or "character varying" or "character" or "citext" => CatalogType.Text,
        _ => CatalogType.Other
    };
}

/// Runs EXPLAIN (FORMAT JSON) under a statement timeout and reports sequential scans for the row-cap guard.
public sealed class NpgsqlExplainRunner : IExplainRunner
{
    private readonly string _connString;
    public NpgsqlExplainRunner(string connString) => _connString = connString;

    public async Task<ExplainOutcome> ExplainAsync(string readOnlySql, int statementTimeoutMs, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using (var to = new NpgsqlCommand($"SET statement_timeout = {statementTimeoutMs}", conn))
            await to.ExecuteNonQueryAsync(ct);

        await using var cmd = new NpgsqlCommand("EXPLAIN (FORMAT JSON) " + readOnlySql, conn);
        var json = (string?)await cmd.ExecuteScalarAsync(ct) ?? "[]";

        var seq = new List<(string, long)>();
        using var doc = JsonDocument.Parse(json);
        WalkPlan(doc.RootElement[0].GetProperty("Plan"), seq);
        return new ExplainOutcome(true, seq);
    }

    private static void WalkPlan(JsonElement plan, List<(string, long)> seq)
    {
        if (plan.TryGetProperty("Node Type", out var nt) && nt.GetString() == "Seq Scan")
        {
            string tbl = plan.TryGetProperty("Relation Name", out var rn) ? rn.GetString() ?? "?" : "?";
            long rows = plan.TryGetProperty("Plan Rows", out var pr) ? pr.GetInt64() : 0;
            seq.Add((tbl, rows));
        }
        if (plan.TryGetProperty("Plans", out var kids))
            foreach (var k in kids.EnumerateArray()) WalkPlan(k, seq);
    }
}

/// Minimal read-only checker. INTEGRATE: swap for your existing SafeSqlValidator.
public sealed class DefaultSafeSqlValidator : ISafeSqlValidator
{
    private static readonly string[] Forbidden =
        { "insert","update","delete","drop","alter","truncate","create","grant","revoke","copy","merge" };

    public bool IsReadOnly(string sql, out string reason)
    {
        var lower = sql.ToLowerInvariant();
        if (lower.Contains(';')) { reason = "multiple statements are not allowed"; return false; }
        foreach (var f in Forbidden)
            if (Regex.IsMatch(lower, $@"(^|\W){Regex.Escape(f)}(\W|$)"))
            { reason = $"contains forbidden token '{f}'"; return false; }
        if (!lower.TrimStart().StartsWith("select")) { reason = "must start with SELECT"; return false; }
        reason = "";
        return true;
    }
}