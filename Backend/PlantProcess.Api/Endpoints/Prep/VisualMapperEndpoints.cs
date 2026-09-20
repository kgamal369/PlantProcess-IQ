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
using PlantProcess.Application.Definitions.Transformations;
namespace PlantProcess.Api.Endpoints.Prep;

public static class VisualMapperEndpoints
{

    public static IEndpointRouteBuilder MapVisualMapperEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/prep/visual-mapper").RequireAuthorization();

        // catalog: staging tables + typed columns + key candidates (name heuristics)
        g.MapGet("/datasets", async (NpgsqlDataSource ds, IConfiguration cfg) =>
        {
            // Constitution v3 II.6.3: the canvas lists the customer's staging layer.
            // The physical schema name is configuration, not a literal, because
            // Amendment 6 (Part III.16) renames it to ppiq_staging in M2.
            var stagingSchema = cfg["Prep:StagingSchema"] ?? "ppiq_staging";

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
                "INSERT INTO ppiq_meta.ppiq_visual_mapper_sessions (tenant_id, source_code, display_name, source_kind, status) " +
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
                "UPDATE ppiq_meta.ppiq_visual_mapper_sessions SET draft_definition = $2::jsonb, updated_at_utc = now() WHERE id = $1;");
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
            var (sql, err, prms) = TransformationSafeSelect.BuildSafeSelect(graph, cfg["Prep:StagingSchema"] ?? "ppiq_staging");
            if (err is not null)
            {
                await RecordDryRun(ds, id, "rejected_by_safe_sql", 0, err);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "rejected_by_safe_sql", rowCount = 0, previewTruncated = false, plannerCost = (double?)null, estimatedRows = (long?)null, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = err });
            }
            try
            {
                // T-035. THE PLANNER'S ESTIMATE, taken before the preview runs.
                // Plain EXPLAIN: it plans the statement and executes NOTHING, so
                // it costs the customer's database almost nothing and cannot
                // change anything. It is an ESTIMATE - not a runtime, not a
                // price - and the interface labels it as one.
                var (plannerCost, estimatedRows) = await TryExplain(ds, sql!, prms);

                var cols = new List<string>(); var rows = new List<object[]>();
                var truncated = false;
                await using (var cmd = ds.CreateCommand(sql!))
                {
                    // M1-16: filter values arrive here as bound parameters, never
                    // as text inside the statement.
                    foreach (var p in prms ?? new List<object>()) cmd.Parameters.AddWithValue(p);
                    await using var r = await cmd.ExecuteReaderAsync();
                    for (var i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
                    while (await r.ReadAsync())
                    {
                        // THE PREVIEW STOPS AT 50 AND SAYS SO. Before T-035 the
                        // reported count was the number of rows read, capped at
                        // 50, and was presented as the row count - so a query
                        // returning four thousand rows reported fifty. The cap
                        // stays; the claim is now truthful about being a cap.
                        if (rows.Count >= 50) { truncated = true; break; }
                        var row = new object[r.FieldCount];
                        for (var i = 0; i < r.FieldCount; i++) row[i] = r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
                        rows.Add(row);
                    }
                }
                var dr = await RecordDryRun(ds, id, "succeeded", rows.Count, null);
                return Results.Ok(new { dryRunId = dr, status = "succeeded", rowCount = rows.Count, previewTruncated = truncated, plannerCost, estimatedRows, columns = cols, rows, message = (string?)null, sql });
            }
            catch (Exception ex)
            {
                // T-035. THE RAW EXCEPTION NEVER LEAVES THE SERVER.
                //
                // This used to return ex.Message straight to the browser, so a
                // Postgres error - its SQLSTATE, its internal wording, sometimes
                // a fragment of the statement - was rendered in the Job Log. The
                // real text is still RECORDED against the dry run, where support
                // can read it; what the engineer sees is a sentence about their
                // definition.
                await RecordDryRun(ds, id, "failed", 0, ex.Message);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "failed", rowCount = 0, previewTruncated = false, plannerCost = (double?)null, estimatedRows = (long?)null, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = SafeDatabaseMessage(ex) });
            }
        });

        g.MapPost("/sessions/{id:guid}/publish", async (
            Guid id,
            NpgsqlDataSource ds,
            HttpContext ctx,
            [Microsoft.AspNetCore.Mvc.FromServices] PlantProcess.Application.Definitions.ICanonicalIdentityResolver identity,
            [Microsoft.AspNetCore.Mvc.FromServices] PlantProcess.Application.Definitions.Canvas.ICanvasDefinitionLifecycle lifecycle,
            CancellationToken ct) =>
        {
            // T-244. CONVERGED ONTO THE CANONICAL DEFINITION LIFECYCLE.
            //
            // The session's stable source_code is the tenant-scoped canonical
            // definition code: it survives reload and is independent of any
            // version number. The session id stays what it always was - the
            // key of the execution projection - and is handed to the lifecycle
            // as a projection handle, never as identity. The projection row is
            // written in the same transaction as the canonical publish.
            var graphJson = await LoadGraphJson(ds, id);
            if (graphJson is null) return Results.BadRequest(new { message = "no graph saved" });

            string? sourceCode;
            await using (var lookup = ds.CreateCommand("SELECT source_code FROM ppiq_meta.ppiq_visual_mapper_sessions WHERE id = $1;"))
            {
                lookup.Parameters.AddWithValue(id);
                sourceCode = await lookup.ExecuteScalarAsync(ct) as string;
            }
            if (string.IsNullOrWhiteSpace(sourceCode)) return Results.NotFound();

            var resolved = await CanvasDefinitionEndpoints.ResolveIdentityAsync(ctx.User, identity, ct);
            if (resolved is null) return Results.Forbid();

            // T-253. The governed output target is the one the AUTHOR declared in the
            // graph they saved, read back from that graph and carried forward exactly.
            // Not defaulted here, not taken from page context, never inferred. The
            // lifecycle then validates it against the canonical projection-target set
            // and refuses by typed code when it is missing or ineligible.
            var graphTarget = PlantProcess.Application.Definitions.Canvas.CanvasDefinitionContent
                .TryReadGraphTargetEntity(graphJson);

            var saved = await lifecycle.SaveGraphAsync(
                new PlantProcess.Application.Definitions.Canvas.CanvasGraphSave(
                    resolved.Value.TenantId, resolved.Value.OwnerId, sourceCode, sourceCode, graphJson, graphTarget),
                ct);
            if (saved.IsFailure) return CanvasDefinitionEndpoints.Refusal(saved.Error!);

            var published = await lifecycle.PublishAsync(
                resolved.Value.TenantId,
                sourceCode,
                saved.Value!.VersionNumber,
                new PlantProcess.Application.Definitions.Canvas.CanvasProjectionHandles(
                    id, null, null, ctx.User?.Identity?.Name ?? "canvas"),
                ct);
            if (published.IsFailure) return CanvasDefinitionEndpoints.Refusal(published.Error!);

            return Results.Ok(new
            {
                versionId = published.Value!.VersionId,
                versionNumber = published.Value.VersionNumber,
                definitionId = published.Value.DefinitionId,
                definitionCode = published.Value.DefinitionCode,
            });
        });

        return app;
    }

    private static Guid TenantId(HttpContext ctx)
        => Guid.TryParse(ctx.User?.FindFirst("tenant_id")?.Value, out var t) ? t : Guid.Empty;

    private static async Task<string?> LoadGraphJson(NpgsqlDataSource ds, Guid id)
    {
        await using var cmd = ds.CreateCommand("SELECT draft_definition::text FROM ppiq_meta.ppiq_visual_mapper_sessions WHERE id = $1;");
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
INSERT INTO ppiq_meta.ppiq_visual_mapper_dry_runs
       (tenant_id, session_id, status, safe_sql_passed, total_rows, mapped_rows, details)
SELECT tenant_id, id, $2, $5, $3, $3, $4::jsonb
FROM ppiq_meta.ppiq_visual_mapper_sessions WHERE id = $1 RETURNING id;");
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

    // T-035. The planner's estimate for a statement, without running it.
    //
    // Plain EXPLAIN, never the ANALYZE form: the statement is planned and not
    // executed, so this cannot alter anything and costs the customer's database
    // almost nothing. Returns nulls rather than throwing - a preview that works
    // must not be lost because the estimate could not be obtained.
    private static async Task<(double? cost, long? rows)> TryExplain(
        NpgsqlDataSource ds, string sql, List<object>? prms)
    {
        try
        {
            await using var cmd = ds.CreateCommand("EXPLAIN (FORMAT JSON) " + sql);
            foreach (var p in prms ?? new List<object>()) cmd.Parameters.AddWithValue(p);
            var raw = (await cmd.ExecuteScalarAsync())?.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return (null, null);
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return (null, null);
            if (!doc.RootElement[0].TryGetProperty("Plan", out var plan)) return (null, null);
            double? cost = plan.TryGetProperty("Total Cost", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : null;
            long? rows = plan.TryGetProperty("Plan Rows", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt64() : null;
            return (cost, rows);
        }
        catch
        {
            return (null, null);
        }
    }

    // T-035. What the engineer is told when the database refuses the statement.
    //
    // Section 5.2.8 asks for a message written for a plant engineer, and the
    // PPIQ-T09 architecture test forbids a stack trace and the load-failure
    // phrases it enumerates - which is why this comment describes them instead
    // of quoting one. Each sentence names something in THEIR definition and says what
    // to do about it. The SQLSTATE decides which sentence; the database's own
    // wording is never passed through, because it describes the generated
    // statement rather than the board that produced it.
    // T-036 reuses this on the authored-SQL execution path. Internal rather than
    // private for that reason and no other: one sanitiser, one set of sentences.
    internal static string SafeDatabaseMessage(Exception ex)
    {
        var state = (ex as PostgresException)?.SqlState ?? "";
        return state switch
        {
            "42P01" => "A table used by this definition is no longer in the staging schema. Reopen the page to refresh the schema list, then put the table back on the board.",
            "42703" => "A column used by this definition is no longer in its table. Reopen the page to refresh the schema list, then choose the column again.",
            "42883" or "42804" or "42P08" => "Two columns in this definition cannot be compared, because their types do not match. Check the join and the filters, and compare columns of the same type.",
            "22P02" or "22003" or "22007" => "A filter value does not suit the type of the column it compares. Check the value on the filter block.",
            "57014" => "The preview was stopped because it ran too long. Narrow it with a filter and try again.",
            "53300" or "53400" => "The database is refusing new work at the moment. Wait a little and run the preview again.",
            _ => "The preview did not run. The definition was safe to compile, so this is a problem in the database rather than on the board. The full reason is recorded against this dry run.",
        };
    }

}