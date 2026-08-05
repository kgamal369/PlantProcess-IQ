// M2-31 SCAFFOLD - thin HTTP surface over the EXISTING 540 visual-mapper tables.
// Discovery 21-Jul: the artifact machinery (sessions/joins/dry_runs/versions with
// draft->validated->published->rolled_back) exists in the database but had NO
// endpoints. This file adds the minimal governed surface the canvas needs.
// SAFETY: SQL is built SERVER-side from the saved graph (equality joins over
// cataloged staging tables, LIMIT-bounded, identifiers quoted). No client SQL.
// WIRE-UP: app.MapVisualMapperEndpoints(); + access matrix line:
//   ("/api/prep/visual-mapper", All(), "analysis.execute", false),
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PlantProcess.Api.Endpoints.Prep;

public static class VisualMapperEndpoints
{
    public record JoinSpec(string LeftTable, string LeftColumn, string RightTable, string RightColumn);

    // M1-16. A filter is a WHERE predicate. Op comes from a whitelist and Value
    // is NEVER placed in the SQL string - it is bound as a parameter.
    public record FilterSpec(string Table, string Column, string Op, string? Value);

    // M1-16. A derived column is one arithmetic operation over two column
    // references, or a column and a numeric constant. Alias is quoted on emit.
    public record DerivedSpec(string Alias, string LeftTable, string LeftColumn, string Op,
                              string? RightTable, string? RightColumn, string? Constant);

    // T-033 item 1. A SELECT block projects qualified fields and nothing
    // more. There is deliberately NO alias on this record: naming an output
    // column is a Rename, and ruling 1 of T-033 is "Select, NOT Rename".
    // An alias is a later grammar expansion, not a change to this shape.
    public record SelectSpec(string Table, string Column);

    // Filters, Derived and Selects default to null so every earlier graph
    // deserialises unchanged and compiles to byte-identical SQL.
    public record MapperGraph(string Name, string TargetEntity, string[] Tables, JoinSpec[] Joins,
                              FilterSpec[]? Filters = null, DerivedSpec[]? Derived = null,
                              SelectSpec[]? Selects = null);

    public static IEndpointRouteBuilder MapVisualMapperEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/prep/visual-mapper").RequireAuthorization();

        // catalog: staging tables + typed columns + key candidates (name heuristics)
        g.MapGet("/datasets", async (NpgsqlDataSource ds, IConfiguration cfg) =>
        {
            // Constitution v3 II.6.3: the canvas lists the customer's staging layer.
            // The physical schema name is configuration, not a literal, because
            // Amendment 6 (Part III.16) renames it to ppiq_staging in M2.
            var stagingSchema = cfg["Prep:StagingSchema"] ?? "dump_store";

            // T-034. THE KEY MARKER NOW COMES FROM DECLARED CONSTRAINTS.
            //
            // It used to come from a name list holding four column names of the
            // emulated plant, written into the product. T-034 says nothing in this
            // tree may be a hardcoded table or column name, so the primary-key and
            // unique constraints are read instead of guessed at.
            //
            // Staged CSV loads often carry no constraints at all. For a table that
            // declares NONE, the fallback is a STRUCTURAL pattern - see
            // LooksLikeKey - which describes a shape and names no customer's
            // column. It is applied PER TABLE, so a table that does declare its
            // keys is never second-guessed by a pattern.
            const string columnSql = @"
SELECT c.table_name, c.column_name, c.data_type, c.is_nullable,
       (k.column_name IS NOT NULL) AS is_declared_key
FROM information_schema.columns c
LEFT JOIN (
    SELECT tc.table_schema, tc.table_name, kcu.column_name
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
      ON kcu.constraint_name = tc.constraint_name
     AND kcu.table_schema = tc.table_schema
    WHERE tc.table_schema = $1 AND tc.constraint_type IN ('PRIMARY KEY', 'UNIQUE')
) k ON k.table_schema = c.table_schema
   AND k.table_name = c.table_name
   AND k.column_name = c.column_name
WHERE c.table_schema = $1
ORDER BY c.table_name, c.ordinal_position;";

            // reltuples is the planner's estimate, and it is -1 on a table that has
            // never been analysed. That is reported as UNKNOWN, not as zero rows:
            // "0 rows" is a claim about the customer's data, "not analysed yet" is
            // a claim about the catalogue, and they are not the same sentence.
            const string rowCountSql = @"
SELECT c.relname, c.reltuples::bigint
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = $1 AND c.relkind IN ('r', 'p', 'm', 'v');";

            var approxRows = new Dictionary<string, long?>();
            await using (var rc = ds.CreateCommand(rowCountSql))
            {
                rc.Parameters.AddWithValue(stagingSchema);
                await using var rr = await rc.ExecuteReaderAsync();
                while (await rr.ReadAsync())
                {
                    var estimate = rr.GetInt64(1);
                    approxRows[rr.GetString(0)] = estimate < 0 ? null : estimate;
                }
            }

            var byTable = new Dictionary<string, List<ColumnFacts>>();
            var declaresKeys = new HashSet<string>();
            await using (var cmd = ds.CreateCommand(columnSql))
            {
                cmd.Parameters.AddWithValue(stagingSchema);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var t = r.GetString(0);
                    var col = r.GetString(1);
                    var ty = r.GetString(2);
                    var nullable = string.Equals(r.GetString(3), "YES", StringComparison.OrdinalIgnoreCase);
                    var declared = r.GetBoolean(4);
                    if (declared) { declaresKeys.Add(t); }
                    if (!byTable.TryGetValue(t, out var list)) byTable[t] = list = new();
                    list.Add(new ColumnFacts(col, ty, nullable, declared));
                }
            }

            return Results.Ok(byTable.Select(kv => new
            {
                table = kv.Key,
                source = stagingSchema,
                approxRowCount = approxRows.TryGetValue(kv.Key, out var n) ? n : null,
                columns = kv.Value.Select(c => new
                {
                    name = c.Name,
                    sqlType = c.SqlType,
                    isNullable = c.IsNullable,
                    isKeyCandidate = declaresKeys.Contains(kv.Key) ? c.DeclaredKey : LooksLikeKey(c.Name),
                }),
            }));
        });

        g.MapPost("/sessions", async (NpgsqlDataSource ds, HttpContext ctx, JsonElement body) =>
        {
            // T-032. The previous statement wrote a column called session_name.
            // It exists in no migration and in no database - a live check on
            // 04-Aug found ZERO sessions ever created, so this call had never
            // once succeeded. The table already owns the concept: display_name
            // is the human name and source_code is the stable identifier, so
            // the endpoint is aligned to the table rather than a column added
            // to preserve a stale statement.
            var name = body.TryGetProperty("name", out var n) ? n.GetString() ?? "canvas-session" : "canvas-session";
            var tenant = TenantId(ctx);
            // UNIQUE(tenant_id, source_code) means a per-name code would refuse
            // the second visit to the canvas, because the shell sends the same
            // default definition name every time. The code is generated.
            var sourceCode = NewSourceCode(name);
            await using var cmd = ds.CreateCommand(
                "INSERT INTO public.ppiq_visual_mapper_sessions (tenant_id, source_code, display_name, source_kind, status) " +
                "VALUES ($1,$2,$3,'generic_relational','draft') RETURNING id;");
            cmd.Parameters.AddWithValue(tenant);
            cmd.Parameters.AddWithValue(sourceCode);
            cmd.Parameters.AddWithValue(name);
            var id = (Guid)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { sessionId = id });
        });

        g.MapPost("/sessions/{id:guid}/graph", async (Guid id, NpgsqlDataSource ds, MapperGraph graph) =>
        {
            // store the whole graph on the session as jsonb draft (versions snapshot it on publish)
            await using var cmd = ds.CreateCommand(
                "UPDATE public.ppiq_visual_mapper_sessions SET draft_definition = $2::jsonb, updated_at_utc = now() WHERE id = $1;");
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(JsonSerializer.Serialize(graph));
            var n = await cmd.ExecuteNonQueryAsync();
            return n == 1 ? Results.Ok(new { ok = true }) : Results.NotFound();
        });

        g.MapPost("/sessions/{id:guid}/dry-run", async (Guid id, NpgsqlDataSource ds, IConfiguration cfg) =>
        {
            var graph = await LoadGraph(ds, id);
            if (graph is null) return Results.BadRequest(new { message = "no graph saved for session" });
            // Same configuration key the catalogue query uses, so the panel and
            // the generated query can never target different schemas again.
            var (sql, err, prms) = BuildSafeSelect(graph, cfg["Prep:StagingSchema"] ?? "dump_store");
            if (err is not null)
            {
                await RecordDryRun(ds, id, "rejected_by_safe_sql", 0, err);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "rejected_by_safe_sql", rowCount = 0, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = err });
            }
            try
            {
                var cols = new List<string>(); var rows = new List<object[]>();
                await using (var cmd = ds.CreateCommand(sql!))
                {
                    // M1-16: filter values arrive here as bound parameters, never
                    // as text inside the statement.
                    foreach (var p in prms ?? new List<object>()) cmd.Parameters.AddWithValue(p);
                    await using var r = await cmd.ExecuteReaderAsync();
                    for (var i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
                    while (await r.ReadAsync() && rows.Count < 50)
                    {
                        var row = new object[r.FieldCount];
                        for (var i = 0; i < r.FieldCount; i++) row[i] = r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
                        rows.Add(row);
                    }
                }
                var dr = await RecordDryRun(ds, id, "succeeded", rows.Count, null);
                return Results.Ok(new { dryRunId = dr, status = "succeeded", rowCount = rows.Count, columns = cols, rows, message = (string?)null, sql });
            }
            catch (Exception ex)
            {
                await RecordDryRun(ds, id, "failed", 0, ex.Message);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "failed", rowCount = 0, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = ex.Message });
            }
        });

        g.MapPost("/sessions/{id:guid}/publish", async (Guid id, NpgsqlDataSource ds, HttpContext ctx) =>
        {
            var graphJson = await LoadGraphJson(ds, id);
            if (graphJson is null) return Results.BadRequest(new { message = "no graph saved" });
            await using var cmd = ds.CreateCommand(@"
INSERT INTO public.ppiq_visual_mapper_versions (tenant_id, session_id, version_number, version_status, mapping_definition, published_by)
SELECT s.tenant_id, s.id,
       COALESCE((SELECT MAX(version_number) FROM public.ppiq_visual_mapper_versions v WHERE v.session_id = s.id), 0) + 1,
       'published', $2::jsonb, $3
FROM public.ppiq_visual_mapper_sessions s WHERE s.id = $1
RETURNING id, version_number;");
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(graphJson);
            cmd.Parameters.AddWithValue(ctx.User?.Identity?.Name ?? "canvas");
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            return Results.Ok(new { versionId = r.GetGuid(0), versionNumber = r.GetInt32(1) });
        });

        return app;
    }

    private static Guid TenantId(HttpContext ctx)
        => Guid.TryParse(ctx.User?.FindFirst("tenant_id")?.Value, out var t) ? t : Guid.Empty;

    private static async Task<string?> LoadGraphJson(NpgsqlDataSource ds, Guid id)
    {
        await using var cmd = ds.CreateCommand("SELECT draft_definition::text FROM public.ppiq_visual_mapper_sessions WHERE id = $1;");
        cmd.Parameters.AddWithValue(id);
        return await cmd.ExecuteScalarAsync() as string;
    }
    private static async Task<MapperGraph?> LoadGraphJson2(string? j)
        => j is null ? null : JsonSerializer.Deserialize<MapperGraph>(j, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    private static async Task<MapperGraph?> LoadGraph(NpgsqlDataSource ds, Guid id)
        => await LoadGraphJson2(await LoadGraphJson(ds, id));

    /// T-032. A stable, unique source_code for a new authoring session. The
    /// display name is the human label; this is the identifier the UNIQUE
    /// constraint governs, so it carries a suffix and two sessions may share a
    /// name without colliding.
    private static string NewSourceCode(string displayName)
    {
        var slug = System.Text.RegularExpressions.Regex
            .Replace(displayName.ToLowerInvariant(), "[^a-z0-9]+", "_")
            .Trim('_');
        if (slug.Length == 0) { slug = "canvas_session"; }
        if (slug.Length > 40) { slug = slug.Substring(0, 40); }
        return slug + "_" + Guid.NewGuid().ToString("n").Substring(0, 8);
    }

    private static async Task<Guid> RecordDryRun(NpgsqlDataSource ds, Guid sessionId, string status, int rows, string? message)
    {
        // T-032. THREE separate contradictions with the table lived in the old
        // statement: row_count and error_message exist on no version of
        // ppiq_visual_mapper_dry_runs, and the status it wrote - "succeeded" -
        // is not one of the four the CHECK constraint allows. The table has
        // total_rows, mapped_rows, safe_sql_passed and a details jsonb.
        //
        // THE WIRE STATUS DOES NOT CHANGE. The client tests for "succeeded",
        // so the mapping to the persisted vocabulary happens here and nowhere
        // else - a rename on the wire would break the authoring shell.
        var persisted = status switch
        {
            "succeeded" => "passed",
            "rejected_by_safe_sql" => "rejected_by_safe_sql",
            _ => "failed"
        };
        await using var cmd = ds.CreateCommand(@"
INSERT INTO public.ppiq_visual_mapper_dry_runs
       (tenant_id, session_id, status, safe_sql_passed, total_rows, mapped_rows, details)
SELECT tenant_id, id, $2, $5, $3, $3, $4::jsonb
FROM public.ppiq_visual_mapper_sessions WHERE id = $1 RETURNING id;");
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(persisted);
        cmd.Parameters.AddWithValue((long)rows);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(new { message }));
        cmd.Parameters.AddWithValue(persisted == "passed");
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    // T-034. What the catalogue knows about one column, before the response
    // shape is built. Kept as a record so the key decision below reads as one
    // expression instead of four parallel dictionaries.
    private sealed record ColumnFacts(string Name, string SqlType, bool IsNullable, bool DeclaredKey);

    // T-034. A SHAPE, NOT A NAME. Used only for a table that declares no key of
    // its own. No customer column name appears here, and none may be added:
    // T034CatalogueHasNoPlantLiteralsTests fails the build if one is.
    private static bool LooksLikeKey(string column) =>
        column.Equals("id", StringComparison.OrdinalIgnoreCase)
        || column.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
        || column.EndsWith("_no", StringComparison.OrdinalIgnoreCase);

    private static bool Ident(string? s)
        => s is not null && System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-zA-Z0-9_]+$");

    // Whitelists. Anything outside them is refused with a named reason and the
    // dry run is recorded as rejected_by_safe_sql, exactly as an illegal
    // identifier already is. This is the honest-refusal contract applied to
    // predicates rather than to identifiers.
    private static readonly string[] FilterOps =
        { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL" };
    private static readonly string[] MathOps = { "+", "-", "*", "/" };

    /// Server-side SQL from the graph: staging-only identifiers, equality joins,
    /// whitelisted predicates with bound values, whitelisted arithmetic, LIMIT.
    private static (string? sql, string? err, List<object>? prms) BuildSafeSelect(MapperGraph g, string schema)
    {
        if (g.Tables.Length == 0) return (null, "graph has no tables", null);
        if (!Ident(schema)) return (null, $"illegal schema identifier '{schema}'", null);
        foreach (var t in g.Tables)
            if (!Ident(t)) return (null, $"illegal table identifier '{t}'", null);
        foreach (var j in g.Joins)
            foreach (var c in new[] { j.LeftColumn, j.RightColumn })
                if (!Ident(c)) return (null, $"illegal column identifier '{c}'", null);

        // Alias map is built first so the SELECT list can reference it.
        var alias = new Dictionary<string, string> { [g.Tables[0]] = "t0" };
        var i = 1;
        foreach (var t in g.Tables.Skip(1)) { alias[t] = $"t{i}"; i++; }

        var prms = new List<object>();

        // ---- SELECT list: the projection, plus one column per derived expression
        // T-033 item 1. THREE STATES, AND THE MIDDLE ONE IS THE POINT.
        //
        //   Selects is null       no Select block is on the board. The
        //                         projection stays SELECT *, so every graph
        //                         saved before T-033 compiles to the same
        //                         statement it compiled to before.
        //   Selects is empty      a Select block IS on the board with
        //                         nothing chosen. Emitting SELECT * here
        //                         would return the opposite of what the
        //                         author asked for, so it is refused with a
        //                         sentence rather than defaulted.
        //   Selects has entries   project exactly those qualified fields.
        //
        // Ruling 2: a field whose table is not on the board is refused. The
        // table is NEVER inferred from anything else in the graph.
        var select = new StringBuilder();
        if (g.Selects is null)
        {
            select.Append("SELECT *");
        }
        else if (g.Selects.Length == 0)
        {
            return (null, "the Select block has no columns chosen. Choose at least one column, or remove the block.", null);
        }
        else
        {
            var projected = new List<string>();
            foreach (var sel in g.Selects)
            {
                if (!alias.ContainsKey(sel.Table)) return (null, $"selected column references table '{sel.Table}' which is not on the board", null);
                if (!Ident(sel.Column)) return (null, $"illegal column identifier '{sel.Column}'", null);
                projected.Add($"{alias[sel.Table]}.\"{sel.Column}\"");
            }
            select.Append("SELECT ").Append(string.Join(", ", projected));
        }
        foreach (var d in g.Derived ?? Array.Empty<DerivedSpec>())
        {
            if (!Ident(d.Alias)) return (null, $"illegal derived alias '{d.Alias}'", null);
            if (!alias.ContainsKey(d.LeftTable)) return (null, $"derived column references table '{d.LeftTable}' which is not on the board", null);
            if (!Ident(d.LeftColumn)) return (null, $"illegal column identifier '{d.LeftColumn}'", null);
            if (!MathOps.Contains(d.Op)) return (null, $"operator '{d.Op}' is not permitted in a derived column", null);

            var left = $"{alias[d.LeftTable]}.\"{d.LeftColumn}\"";
            string right;
            if (!string.IsNullOrWhiteSpace(d.RightColumn))
            {
                var rt = string.IsNullOrWhiteSpace(d.RightTable) ? d.LeftTable : d.RightTable!;
                if (!alias.ContainsKey(rt)) return (null, $"derived column references table '{rt}' which is not on the board", null);
                if (!Ident(d.RightColumn)) return (null, $"illegal column identifier '{d.RightColumn}'", null);
                right = $"{alias[rt]}.\"{d.RightColumn}\"";
            }
            else if (double.TryParse(d.Constant, System.Globalization.NumberStyles.Any,
                                     System.Globalization.CultureInfo.InvariantCulture, out var cv))
            {
                // A numeric constant is emitted in invariant form. It cannot carry
                // a quote or an identifier, so it is safe without a parameter.
                right = cv.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return (null, $"derived column '{d.Alias}' needs a second column or a numeric constant", null);
            }

            // Division guards against divide-by-zero rather than failing the run.
            if (d.Op == "/") select.Append(", (").Append(left).Append(" / NULLIF(").Append(right).Append(", 0)) AS \"").Append(d.Alias).Append('"');
            else select.Append(", (").Append(left).Append(' ').Append(d.Op).Append(' ').Append(right).Append(") AS \"").Append(d.Alias).Append('"');
        }

        // ---- FROM and JOIN
        var sb = new StringBuilder();
        sb.Append(select).Append(" FROM \"").Append(schema).Append("\".\"").Append(g.Tables[0]).Append("\" t0");
        // T-032. A JOIN MAY ONLY REFERENCE TABLES ALREADY IN THE FROM CLAUSE.
        //
        // The previous filter tested alias.ContainsKey on both sides of every
        // join, but the alias map is built for EVERY table BEFORE this loop
        // starts, so that test was always true and filtered nothing.
        //
        // With three tables wired t0-t1 and t1-t2, the ON clause emitted while
        // joining t1 contained "t1.x = t2.y" - and t2 was not in the query yet.
        // PostgreSQL refused with 42P01, missing FROM-clause entry for table
        // "t2". Two tables always worked, which is why this survived: the path
        // had never been run, and a live check found ZERO sessions ever created.
        //
        // `emitted` tracks what is genuinely in the FROM clause so far.
        // T-032. THE PLANNER WORKS FROM A FRONTIER, NOT FROM LIST ORDER.
        //
        // Two things are being satisfied here and they are not the same thing.
        //
        // THE SCOPE INVARIANT. An ON clause may reference only aliases already
        // in the FROM clause plus the alias this JOIN introduces. Breaking it
        // produced 42P01, missing FROM-clause entry for table "t2", because an
        // earlier version filtered joins on alias.ContainsKey - and the alias
        // map is built for EVERY table before any SQL is emitted, so that test
        // was always true and filtered nothing.
        //
        // THE REACHABILITY INVARIANT. Which table is emitted next is decided by
        // CONNECTIVITY, never by position in g.Tables. The board sends tables in
        // the order the author dropped them, so a legal graph wired A-B and B-C
        // can arrive as [A, C, B]. Walking the list in order would reach C,
        // find no edge back to {A}, and refuse a graph that is perfectly valid.
        //
        // The loop below takes, on each pass, any pending table with an edge to
        // something already emitted, in either direction - LeftTable or
        // RightTable, it makes no difference. Only when NO pending table can be
        // reached is the graph genuinely disconnected, and then it is refused
        // with a sentence rather than compiled into SQL that cannot run.
        var emitted = new HashSet<string> { g.Tables[0] };
        var pending = new List<string>(g.Tables.Skip(1));
        while (pending.Count > 0)
        {
            string? next = null;
            var nextJoins = Array.Empty<JoinSpec>();
            foreach (var candidate in pending)
            {
                var edges = g.Joins
                    .Where(j => (j.LeftTable == candidate && emitted.Contains(j.RightTable))
                             || (j.RightTable == candidate && emitted.Contains(j.LeftTable)))
                    .ToArray();
                if (edges.Length > 0) { next = candidate; nextJoins = edges; break; }
            }
            if (next is null)
            {
                return (null, $"table '{pending[0]}' has no join reaching the rest of the board. Wire it to a table that is already connected, or remove it.", null);
            }
            sb.Append(" JOIN \"").Append(schema).Append("\".\"").Append(next).Append("\" ").Append(alias[next]).Append(" ON ");
            sb.Append(string.Join(" AND ", nextJoins.Select(j =>
                $"{alias[j.LeftTable]}.\"{j.LeftColumn}\" = {alias[j.RightTable]}.\"{j.RightColumn}\"")));
            emitted.Add(next);
            pending.Remove(next);
        }

        // ---- WHERE: whitelisted operators, values ALWAYS bound
        var preds = new List<string>();
        foreach (var f in g.Filters ?? Array.Empty<FilterSpec>())
        {
            if (!alias.ContainsKey(f.Table)) return (null, $"filter references table '{f.Table}' which is not on the board", null);
            if (!Ident(f.Column)) return (null, $"illegal column identifier '{f.Column}'", null);
            var op = (f.Op ?? string.Empty).Trim().ToUpperInvariant();
            if (op is "=" or "<>" or ">" or ">=" or "<" or "<=") { /* symbols keep their case */ }
            if (!FilterOps.Contains(op)) return (null, $"operator '{f.Op}' is not permitted in a filter", null);

            var colRef = $"{alias[f.Table]}.\"{f.Column}\"";
            if (op is "IS NULL" or "IS NOT NULL")
            {
                preds.Add($"{colRef} {op}");
                continue;
            }
            if (f.Value is null) return (null, $"filter on '{f.Column}' needs a value for operator '{op}'", null);

            // Bound as a number when it reads as one, so a comparison against a
            // numeric column works; otherwise as text.
            object val = double.TryParse(f.Value, System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out var nv)
                         ? nv : f.Value;
            prms.Add(val);
            preds.Add($"{colRef} {op} ${prms.Count}");
        }
        if (preds.Count > 0) sb.Append(" WHERE ").Append(string.Join(" AND ", preds));

        sb.Append(" LIMIT 50;");
        return (sb.ToString(), null, prms);
    }
}