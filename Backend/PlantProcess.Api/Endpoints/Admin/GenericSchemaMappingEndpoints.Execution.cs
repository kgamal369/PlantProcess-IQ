using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_EXECUTION_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<IResult> ExecuteMappingAsync(
        string viewCode,
        [FromBody] ExecuteMappingRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(viewCode))
            return ApplicationProblems.Validation("viewCode is required.");

        var started = Stopwatch.StartNew();
        var actor = GetActor(user);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                id,
                view_code,
                target_entity,
                physical_schema,
                physical_view_name,
                sql_text
            FROM ppiq_meta.canonical_schema_views
            WHERE is_deleted = false
              AND is_active = true
              AND lower(view_code) = lower(@view_code)
            LIMIT 1;
            """,
            cancellationToken,
            ("view_code", viewCode));

        if (rows.Count == 0)
            return Results.NotFound(new { message = $"Canonical schema view '{viewCode}' was not found or is inactive." });

        var row = rows[0];
        var id = (Guid)row["id"]!;
        var targetEntity = Convert.ToString(row["target_entity"]) ?? "Unknown";
        var schema = Convert.ToString(row["physical_schema"]) ?? "public";
        var physicalView = Convert.ToString(row["physical_view_name"]) ?? "";
        var sqlText = Convert.ToString(row["sql_text"]) ?? "";

        var executionStatus = "Success";
        var message = "Mapping view validated/refreshed and row count collected.";
        var count = 0;

        try
        {
            var normalizedSql = NormalizeSelectSql(sqlText);
            await CreateOrReplaceViewAsync(db, schema, physicalView, normalizedSql, cancellationToken);
            count = await CountRowsAsync(db, schema, physicalView, cancellationToken);

            await ExecuteNonQueryAsync(
                db,
                """
                UPDATE ppiq_meta.canonical_schema_views
                SET last_executed_at_utc = now(),
                    last_execution_status = @status,
                    last_execution_message = @message,
                    last_execution_row_count = @row_count,
                    updated_at_utc = now()
                WHERE id = @id;
                """,
                cancellationToken,
                ("status", executionStatus),
                ("message", message),
                ("row_count", count),
                ("id", id));
        }
        catch (Exception ex)
        {
            executionStatus = "Failed";
            message = ex.Message;

            await ExecuteNonQueryAsync(
                db,
                """
                UPDATE ppiq_meta.canonical_schema_views
                SET last_executed_at_utc = now(),
                    last_execution_status = @status,
                    last_execution_message = @message,
                    last_execution_row_count = 0,
                    updated_at_utc = now()
                WHERE id = @id;
                """,
                cancellationToken,
                ("status", executionStatus),
                ("message", message),
                ("id", id));
        }

        started.Stop();

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO ppiq_meta.schema_mapping_executions
            (
                canonical_schema_view_id,
                view_code,
                target_entity,
                execution_mode,
                status,
                message,
                row_count,
                duration_ms,
                executed_by,
                started_at_utc,
                completed_at_utc,
                details_json
            )
            VALUES
            (
                @id,
                @view_code,
                @target_entity,
                @execution_mode,
                @status,
                @message,
                @row_count,
                @duration_ms,
                @executed_by,
                now(),
                now(),
                CAST(@details_json AS jsonb)
            );
            """,
            cancellationToken,
            ("id", id),
            ("view_code", viewCode),
            ("target_entity", targetEntity),
            ("execution_mode", request.ExecutionMode ?? "ValidateAndRefreshView"),
            ("status", executionStatus),
            ("message", message),
            ("row_count", count),
            ("duration_ms", (int)Math.Min(started.ElapsedMilliseconds, int.MaxValue)),
            ("executed_by", actor),
            ("details_json", JsonSerializer.Serialize(new
            {
                request,
                schema,
                physicalView
            })));

        var response = new
        {
            viewCode,
            targetEntity,
            qualifiedName = $"{schema}.{physicalView}",
            status = executionStatus,
            message,
            rowCount = count,
            durationMs = started.ElapsedMilliseconds
        };

        return executionStatus == "Success"
            ? Results.Ok(response)
            : Results.BadRequest(response);
    }

private static async Task<IResult> GetReadinessAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                COUNT(*) FILTER (WHERE is_deleted = false) AS total_views,
                COUNT(*) FILTER (WHERE is_deleted = false AND is_active = true) AS active_views,
                COUNT(*) FILTER (WHERE is_deleted = false AND is_approved = true) AS approved_views,
                COUNT(*) FILTER (WHERE is_deleted = false AND view_kind = 'JoinView') AS join_views,
                COUNT(*) FILTER (WHERE is_deleted = false AND view_kind = 'KpiView') AS kpi_views,
                COUNT(*) FILTER (WHERE is_deleted = false AND last_execution_status = 'Success') AS executed_successfully
            FROM ppiq_meta.canonical_schema_views;
            """,
            cancellationToken);

        var summary = rows.Count == 0 ? new Dictionary<string, object?>() : rows[0];

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            phase = "P02",
            taskRange = "PPIQ-T107..PPIQ-T112",
            readiness = summary,
            checks = new[]
            {
                new { taskId = "PPIQ-T107", name = "canonical_schema_views catalog", status = "Implemented" },
                new { taskId = "PPIQ-T108", name = "SchemaViewResolver endpoint", status = "Implemented" },
                new { taskId = "PPIQ-T109", name = "Schema mapping UI surface", status = "Implemented" },
                new { taskId = "PPIQ-T110", name = "Cross-source join authoring", status = "Implemented" },
                new { taskId = "PPIQ-T111", name = "KPI-as-view authoring", status = "Implemented" },
                new { taskId = "PPIQ-T112", name = "Mapping execution service and log", status = "Implemented" }
            }
        });
    }

private static async Task CreateOrReplaceViewAsync(
        PlantProcessDbContext db,
        string schema,
        string viewName,
        string selectSql,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSelectSql(selectSql);

        await ExecuteNonQueryAsync(
            db,
            $"""
            CREATE SCHEMA IF NOT EXISTS {QuoteIdentifier(schema)};
            CREATE OR REPLACE VIEW {QuoteIdentifier(schema)}.{QuoteIdentifier(viewName)} AS
            {normalized};
            """,
            cancellationToken);
    }

private static async Task<IReadOnlyList<PreviewColumn>> PreviewSchemaOnlyAsync(
        PlantProcessDbContext db,
        string sqlText,
        CancellationToken cancellationToken)
    {
        var preview = await PreviewRowsAsync(db, sqlText, 0, cancellationToken);
        return preview.columns;
    }

private static async Task<int> CountRowsAsync(
        PlantProcessDbContext db,
        string schema,
        string viewName,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT(*) FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(viewName)}";

        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 60;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }
}
