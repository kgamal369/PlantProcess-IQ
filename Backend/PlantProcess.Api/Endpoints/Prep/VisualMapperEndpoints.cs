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
    public record MapperGraph(string Name, string TargetEntity, string[] Tables, JoinSpec[] Joins);

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
            const string sql = @"
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = $1
ORDER BY table_name, ordinal_position;";
            var byTable = new Dictionary<string, List<object>>();
            await using var cmd = ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue(stagingSchema);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var t = r.GetString(0); var c = r.GetString(1); var ty = r.GetString(2);
                var isKey = c.EndsWith("_id") || c.EndsWith("_no") || c is "id" or "piece_id" or "material_id" or "heat_id" or "coil_id";
                if (!byTable.TryGetValue(t, out var list)) byTable[t] = list = new();
                list.Add(new { name = c, sqlType = ty, isKeyCandidate = isKey });
            }
            return Results.Ok(byTable.Select(kv => new { table = kv.Key, source = stagingSchema, columns = kv.Value }));
        });

        g.MapPost("/sessions", async (NpgsqlDataSource ds, HttpContext ctx, JsonElement body) =>
        {
            var name = body.TryGetProperty("name", out var n) ? n.GetString() ?? "canvas-session" : "canvas-session";
            var tenant = TenantId(ctx);
            await using var cmd = ds.CreateCommand(
                "INSERT INTO public.ppiq_visual_mapper_sessions (tenant_id, session_name, status) VALUES ($1,$2,'draft') RETURNING id;");
            cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(name);
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

        g.MapPost("/sessions/{id:guid}/dry-run", async (Guid id, NpgsqlDataSource ds) =>
        {
            var graph = await LoadGraph(ds, id);
            if (graph is null) return Results.BadRequest(new { message = "no graph saved for session" });
            var (sql, err) = BuildSafeSelect(graph);
            if (err is not null)
            {
                await RecordDryRun(ds, id, "rejected_by_safe_sql", 0, err);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "rejected_by_safe_sql", rowCount = 0, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = err });
            }
            try
            {
                var cols = new List<string>(); var rows = new List<object[]>();
                await using (var cmd = ds.CreateCommand(sql!))
                await using (var r = await cmd.ExecuteReaderAsync())
                {
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

    private static async Task<Guid> RecordDryRun(NpgsqlDataSource ds, Guid sessionId, string status, int rows, string? message)
    {
        await using var cmd = ds.CreateCommand(@"
INSERT INTO public.ppiq_visual_mapper_dry_runs (tenant_id, session_id, status, row_count, error_message)
SELECT tenant_id, id, $2, $3, $4 FROM public.ppiq_visual_mapper_sessions WHERE id = $1 RETURNING id;");
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue(rows);
        cmd.Parameters.AddWithValue((object?)message ?? DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    /// Server-side SQL from the graph: staging-only identifiers, equality joins, LIMIT.
    private static (string? sql, string? err) BuildSafeSelect(MapperGraph g)
    {
        if (g.Tables.Length == 0) return (null, "graph has no tables");
        foreach (var t in g.Tables)
            if (!System.Text.RegularExpressions.Regex.IsMatch(t, "^[a-zA-Z0-9_]+$"))
                return (null, $"illegal table identifier '{t}'");
        foreach (var j in g.Joins)
            foreach (var c in new[] { j.LeftColumn, j.RightColumn })
                if (!System.Text.RegularExpressions.Regex.IsMatch(c, "^[a-zA-Z0-9_]+$"))
                    return (null, $"illegal column identifier '{c}'");

        var sb = new StringBuilder();
        sb.Append("SELECT * FROM staging.\"").Append(g.Tables[0]).Append("\" t0");
        var alias = new Dictionary<string, string> { [g.Tables[0]] = "t0" };
        var i = 1;
        foreach (var t in g.Tables.Skip(1)) { alias[t] = $"t{i}"; i++; }
        foreach (var t in g.Tables.Skip(1))
        {
            var joins = g.Joins.Where(j => j.RightTable == t || j.LeftTable == t)
                .Where(j => alias.ContainsKey(j.LeftTable) && alias.ContainsKey(j.RightTable)).ToArray();
            if (joins.Length == 0) return (null, $"table '{t}' has no join to the graph");
            sb.Append(" JOIN staging.\"").Append(t).Append("\" ").Append(alias[t]).Append(" ON ");
            sb.Append(string.Join(" AND ", joins.Select(j =>
                $"{alias[j.LeftTable]}.\"{j.LeftColumn}\" = {alias[j.RightTable]}.\"{j.RightColumn}\"")));
        }
        sb.Append(" LIMIT 50;");
        return (sb.ToString(), null);
    }
}