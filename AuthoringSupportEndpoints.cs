using System.Data;
using System.Text.Json;
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

        var methods = app.MapGroup("/api/analysis/methods")
            .WithTags("Analysis - method catalogue")
            .RequireAuthorization();

        methods.MapGet("/", GetMethodsAsync);

        return app;
    }

    public sealed record RunSqlRequest(string? Sql, int? RowLimit, int? TimeoutMs);

    /// <summary>
    /// Mirrors DryRunResult on the wire so the debug log and the preview table
    /// need no new branch: status, rowCount, columns, rows, message.
    /// </summary>
    public sealed record RunSqlResponse(
        string Status,
        int RowCount,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<object?>> Rows,
        string Message,
        string? ErrorCode,
        string? Sql,
        int AppliedRowLimit);

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
        var rows = new List<IReadOnlyList<object?>>();

        try
        {
            await using var exec = conn.CreateCommand();
            exec.CommandText =
                $"SET LOCAL statement_timeout = {Math.Clamp(timeoutMs, 250, 10000)}; " +
                $"SELECT * FROM ({normalized}) __ppiq_authored LIMIT {appliedLimit};";
            await using var reader = await exec.ExecuteReaderAsync(ct);

            for (var i = 0; i < reader.FieldCount; i++) { columns.Add(reader.GetName(i)); }

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
            return Results.Ok(new RunSqlResponse(
                Status: "failed",
                RowCount: 0,
                Columns: Array.Empty<string>(),
                Rows: Array.Empty<IReadOnlyList<object?>>(),
                Message: "The statement passed validation and then failed on execution: " + ex.MessageText,
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
            AppliedRowLimit: appliedLimit));
    }

    public sealed record MethodDto(
        string Code,
        string DisplayName,
        string Group,
        string AppliesTo,
        string Rationale,
        bool IsImplemented);

    private static Task<IResult> GetMethodsAsync()
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

        return Task.FromResult(Results.Ok(new
        {
            // Stated on the wire so a client can show it and nobody has to read
            // this file to learn it.
            source = "code",
            note = "Projected from AnalysisMethod and MethodSelector. Becomes registry-driven "
                 + "when ml_method_definitions lands; this contract does not change.",
            methods = catalogue,
        }));
    }
}
