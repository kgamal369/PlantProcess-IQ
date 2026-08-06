using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Prep;

/// <summary>
/// PREREQUISITES for M1-19 (SQL editor) and M1-20 (method palette).
///
/// M1-19. The safe-SQL validator ALREADY EXISTS as
/// public.ppiq_resolve_safe_sql(text, integer, integer) and already enforces
/// every control Authoring Layer Specification 12.1 requires: SELECT/WITH only,
/// no write/DDL/control statement, no forbidden function, exactly one
/// statement, no CROSS JOIN, a clamped row ceiling and a clamped statement
/// timeout - and it EXPLAINs the statement against the real schema so an
/// unknown view or column is refused BY NAME rather than at run time.
///
/// What it does not do is return rows. It validates. So the editor's "Run and
/// see what came back" needs one endpoint that calls the validator FIRST and
/// executes only on its approval. That is this file. Nothing bypasses the
/// validator, which is the whole constraint the task states.
///
/// It is exposed here rather than reused where it already sits, because the
/// existing caller is mounted under /admin/p03p04/completion behind the data
/// manager policy. That route carries a phase token, which Chapter B.10 forbids,
/// and an authoring surface must not require an administrative role.
///
/// M1-20. The method catalogue also already exists, as
/// PlantProcess.Analytics.Core.Methods.AnalysisMethod plus MethodSelector,
/// which chooses deterministically by variable-pair shape and records WHY.
/// Five methods are implemented. What does not exist is a way for a client to
/// ASK what they are, so the palette has nothing to populate from. This
/// projects the catalogue over HTTP.
///
/// HONEST LIMIT, and it is the difference between this and the acceptance
/// criterion: this endpoint reads a C# enum. Adding a method still means
/// editing code, so "adding a method to the registry makes it appear in the
/// palette with no code change" is NOT yet satisfied. It is satisfied when the
/// enum is replaced by ml_method_definitions and this handler reads that table
/// instead - at which point the client contract below does not change, which is
/// the point of shipping the contract first.
/// </summary>
public static class AuthoringSupportEndpoints
{
    public static IEndpointRouteBuilder MapAuthoringSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var sql = app.MapGroup("/api/prep/sql")
            .WithTags("Prep - SQL authoring")
            .RequireAuthorization();

        sql.MapPost("/run", RunAsync);
        // M1-19: both modes produce the same artifact class - a named, versioned,
        // saved definition. This is the SQL body of that artifact.
        sql.MapPost("/versions", SaveSqlVersionAsync);

        // The group carries the parent path and the child carries the leaf.
        // MapGroup("/api/analysis/methods") + MapGet("/") builds the pattern
        // "/api/analysis/methods/" WITH a trailing slash, which a GET to
        // "/api/analysis/methods" does not match. That is a 404 that looks
        // like a missing endpoint and is a routing mistake.
        var analysis = app.MapGroup("/api/analysis")
            .WithTags("Analysis - method catalogue")
            .RequireAuthorization();

        analysis.MapGet("/methods", GetMethods);

        return app;
    }

    public sealed record RunSqlRequest(string? Sql, int? RowLimit, int? TimeoutMs);

    /// <summary>
    /// Mirrors DryRunResult on the wire so the debug log and the preview table
    /// need no new branch: status, rowCount, columns, rows, message.
    /// </summary>
    // T-036. The returned column list gains its DATABASE TYPE, taken from the
    // reader's own metadata. The browser must never infer a SQL type from a
    // sample JavaScript value - "3" is a text column as often as it is an
    // integer one, and guessing would put a wrong type in front of an engineer
    // deciding whether a column can be joined.
    public sealed record AuthoredColumn(string Name, string DatabaseType);

    public sealed record RunSqlResponse(
        string Status,
        int RowCount,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<object?>> Rows,
        string Message,
        string? ErrorCode,
        string? Sql,
        int AppliedRowLimit,
        // Defaulted so the refusal and failure paths - which have no columns to
        // describe - are untouched, and no existing caller of Columns breaks.
        IReadOnlyList<AuthoredColumn>? ColumnDetails = null);

    private static async Task<IResult> RunAsync(
        [FromBody] RunSqlRequest request,
        [FromServices] PlantProcessDbContext db,
        CancellationToken ct)
    {
        var statement = request.Sql ?? string.Empty;
        var rowLimit = request.RowLimit ?? 100;
        var timeoutMs = request.TimeoutMs ?? 3000;

        // STEP 1 - the existing validator. Never skipped, never inlined here,
        // never re-implemented: a second implementation of a governance rule is
        // exactly what Constitution II.7.6 forbids.
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); }

        bool isValid = false;
        string errorCode = "Unknown";
        string message = "The validator returned no verdict.";
        string normalized = statement;
        int appliedLimit = rowLimit;

        await using (var check = conn.CreateCommand())
        {
            check.CommandText =
                "SELECT is_valid, error_code, message, normalized_sql, applied_row_limit " +
                "FROM public.ppiq_resolve_safe_sql(@sql, @rowLimit, @timeoutMs);";
            check.Parameters.Add(new NpgsqlParameter("sql", statement));
            check.Parameters.Add(new NpgsqlParameter("rowLimit", rowLimit));
            check.Parameters.Add(new NpgsqlParameter("timeoutMs", timeoutMs));

            await using var reader = await check.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                isValid = !reader.IsDBNull(0) && reader.GetBoolean(0);
                errorCode = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                message = reader.IsDBNull(2) ? message : reader.GetString(2);
                normalized = reader.IsDBNull(3) ? statement : reader.GetString(3);
                appliedLimit = reader.IsDBNull(4) ? rowLimit : reader.GetInt32(4);
            }
        }

        if (!isValid)
        {
            // Refused BY NAME. "Invalid query" is not acceptable output from a
            // product that sells honesty - the validator's own error code and
            // sentence are carried through untouched.
            return Results.Ok(new RunSqlResponse(
                Status: "rejected_by_safe_sql",
                RowCount: 0,
                Columns: Array.Empty<string>(),
                Rows: Array.Empty<IReadOnlyList<object?>>(),
                Message: message,
                ErrorCode: errorCode,
                Sql: normalized,
                AppliedRowLimit: appliedLimit));
        }

        // STEP 2 - execute, only now, and only what the validator normalised.
        // The ceiling is re-applied by the server regardless of any LIMIT the
        // author wrote, per Specification 12.1.
        var columns = new List<string>();
        var columnDetails = new List<AuthoredColumn>();
        var rows = new List<IReadOnlyList<object?>>();

        try
        {
            await using var exec = conn.CreateCommand();
            exec.CommandText =
                $"SET LOCAL statement_timeout = {Math.Clamp(timeoutMs, 250, 10000)}; " +
                $"SELECT * FROM ({normalized}) __ppiq_authored LIMIT {appliedLimit};";
            await using var reader = await exec.ExecuteReaderAsync(ct);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
                columnDetails.Add(new AuthoredColumn(reader.GetName(i), reader.GetDataTypeName(i)));
            }

            while (await reader.ReadAsync(ct))
            {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }
        }
        catch (PostgresException ex)
        {
            // The validator passed it and the database still refused. Say which,
            // and say that it got past validation - that distinction is what
            // tells an engineer whether to change the SQL or report a defect.
            //
            // T-036 HOTFIX. This used to append the database's own text, which
            // is the same raw-exception leak T-035 closed on the dry-run path.
            // The sentence comes from the mechanism T-035 already established -
            // reused, not re-implemented, because a second sanitiser is a second
            // place for the rule to drift.
            return Results.Ok(new RunSqlResponse(
                Status: "failed",
                RowCount: 0,
                Columns: Array.Empty<string>(),
                Rows: Array.Empty<IReadOnlyList<object?>>(),
                Message: "The statement passed validation and then failed when it ran. "
                    + VisualMapperEndpoints.SafeDatabaseMessage(ex),
                ErrorCode: ex.SqlState,
                Sql: normalized,
                AppliedRowLimit: appliedLimit));
        }

        return Results.Ok(new RunSqlResponse(
            Status: "succeeded",
            RowCount: rows.Count,
            Columns: columns,
            Rows: rows,
            Message: message,
            ErrorCode: null,
            Sql: normalized,
            AppliedRowLimit: appliedLimit,
            ColumnDetails: columnDetails));
    }

    // ================================================================== M1-19
    // Saving an authored statement as an immutable version.
    //
    // WHY THIS EXISTS. The dual-mode contract says both modes produce the same
    // artifact class: a named, versioned, saved definition. This path writes
    // ppiq_mapping_versions - version_number, a jsonb definition and a status -
    // which the 21-Jul matrix found matches the specification's immutability
    // rule verbatim.
    //
    // CORRECTED BY T-039. An earlier version of this comment said the GRAPH
    // path already used this table. It does not: the board publish path writes
    // ppiq_visual_mapper_versions, keyed by session_id, while this table is
    // keyed by mapping_code. The code here was always right; the sentence was
    // stale. The two stores therefore carry DIFFERENT identity semantics, which
    // is why T-039 refused to adapt the Transformation kind behind
    // IDefinitionService and left that convergence to M2a.
    //
    // THE FORKED GRAPH TRAVELS WITH IT. When a user forks a visual definition
    // into SQL authoring, the graph is detached - but detached is not deleted.
    // It is stored inside the definition jsonb under forkedFromGraph, so the
    // acceptance line "the graph is still retrievable afterwards" is satisfied
    // by the artifact itself and not by asking the user to remember.
    //
    // NOTHING UNVALIDATED IS EVER STORED. The statement is put through
    // ppiq_resolve_safe_sql before the insert. A definition that could not run
    // is not a definition, and storing one would mean a published version that
    // fails the first time somebody opens it.

    public sealed record SaveSqlVersionRequest(
        string? Code,
        string? DisplayName,
        string? CanonicalEntity,
        string? Sql,
        System.Text.Json.JsonElement? ForkedFromGraph);

    public sealed record SaveSqlVersionResponse(
        bool Saved, int VersionNumber, string? Id, string Message, string? ErrorCode);

    private static async Task<IResult> SaveSqlVersionAsync(
        [FromBody] SaveSqlVersionRequest request,
        [FromServices] PlantProcessDbContext db,
        CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(request.Code) ? "sql_definition" : request.Code!.Trim();
        var statement = request.Sql ?? string.Empty;

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); }

        // Validate before storing. Same function, same rules, no second copy.
        var isValid = false;
        var errorCode = "Unknown";
        var message = "The validator returned no verdict.";
        var normalized = statement;

        await using (var check = conn.CreateCommand())
        {
            check.CommandText =
                "SELECT is_valid, error_code, message, normalized_sql " +
                "FROM public.ppiq_resolve_safe_sql(@sql, 100, 3000);";
            check.Parameters.Add(new NpgsqlParameter("sql", statement));
            await using var r = await check.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                isValid = !r.IsDBNull(0) && r.GetBoolean(0);
                errorCode = r.IsDBNull(1) ? "Unknown" : r.GetString(1);
                message = r.IsDBNull(2) ? message : r.GetString(2);
                normalized = r.IsDBNull(3) ? statement : r.GetString(3);
            }
        }

        if (!isValid)
        {
            return Results.Ok(new SaveSqlVersionResponse(
                false, 0, null,
                "Not saved. A definition that cannot run is not a definition. " + message,
                errorCode));
        }

        var definition = System.Text.Json.JsonSerializer.Serialize(new
        {
            body = "sql",
            sql = normalized,
            forkedFromGraph = request.ForkedFromGraph,
            authoredAtUtc = DateTime.UtcNow,
        });

        int version;
        string? id;
        await using (var insert = conn.CreateCommand())
        {
            // Immutable: a new row per save, version_number derived from what is
            // already there. Nothing is ever updated in place.
            insert.CommandText = @"
                INSERT INTO public.ppiq_mapping_versions
                    (mapping_code, display_name, canonical_entity, environment,
                     version_number, definition, status)
                SELECT @code, @name, @entity, 'authoring',
                       COALESCE(MAX(version_number), 0) + 1, @def::jsonb, 'Published'
                FROM public.ppiq_mapping_versions WHERE mapping_code = @code
                RETURNING version_number, id::text;";
            insert.Parameters.Add(new NpgsqlParameter("code", code));
            insert.Parameters.Add(new NpgsqlParameter("name", (object?)request.DisplayName ?? DBNull.Value));
            insert.Parameters.Add(new NpgsqlParameter("entity", (object?)request.CanonicalEntity ?? DBNull.Value));
            insert.Parameters.Add(new NpgsqlParameter("def", definition));

            await using var r = await insert.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                return Results.Ok(new SaveSqlVersionResponse(false, 0, null, "The insert returned no row.", "NoRow"));
            }
            version = r.GetInt32(0);
            id = r.GetString(1);
        }

        return Results.Ok(new SaveSqlVersionResponse(
            true, version, id,
            "Published version " + version + " of " + code + ". Immutable; the forked graph travels inside it.",
            null));
    }

    public sealed record MethodDto(
        string Code,
        string DisplayName,
        string Group,
        string AppliesTo,
        string Rationale,
        bool IsImplemented);

    private static IResult GetMethods()
    {
        // Projected from MethodSelector's own rules, so the palette cannot
        // offer a method the selector would call NotApplicable, and the
        // rationale a user reads is the same sentence the engine records.
        var catalogue = new List<MethodDto>
        {
            new("Spearman", "Spearman rank correlation", "Correlation",
                "numeric / numeric",
                MethodSelector.Select(VariableType.Numeric, VariableType.Numeric).Rationale, true),

            new("MutualInformation", "Mutual information", "Correlation",
                "numeric / numeric, non-monotonic",
                MethodSelector.Select(VariableType.Numeric, VariableType.Numeric, numericRelationshipNonlinear: true).Rationale, true),

            new("PointBiserial", "Point-biserial correlation", "Correlation",
                "binary / numeric",
                MethodSelector.Select(VariableType.Binary, VariableType.Numeric).Rationale, true),

            new("CramersV", "Cramer's V", "Association",
                "categorical / categorical",
                MethodSelector.Select(VariableType.Categorical, VariableType.Categorical).Rationale, true),

            new("LassoVif", "Lasso screen with VIF", "Multivariate",
                "many or collinear predictors",
                MethodSelector.Select(VariableType.Numeric, VariableType.Numeric, manyCollinearPredictors: true).Rationale, true),
        };

        return Results.Ok(new
        {
            // Stated on the wire so a client can show it and nobody has to read
            // this file to learn it.
            source = "code",
            note = "Projected from AnalysisMethod and MethodSelector. Becomes registry-driven "
                 + "when ml_method_definitions lands; this contract does not change.",
            methods = catalogue,
        });
    }
}