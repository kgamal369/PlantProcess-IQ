using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class TwoStageImportEndpoints
{
    public static IEndpointRouteBuilder MapTwoStageImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/two-stage-import")
            .WithTags("Admin - Two Stage Import")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/overview", GetOverviewAsync)
            .WithSummary("Get Phase 03 two-stage import overview");

        group.MapGet("/source-tables", GetSourceTablesAsync)
            .WithSummary("Get dump-copy source table registry");

        group.MapGet("/runs", GetRunsAsync)
            .WithSummary("Get recent two-stage import runs");

        group.MapPost("/stage1/run", RunStage1Async)
            .WithSummary("Run Stage 1 delta import for one registry or all registries");

        group.MapPost("/stage2/run", RunStage2Async)
            .WithSummary("Run Stage 2 canonical refresh for one registry or all registries");

        group.MapPost("/run-full-cycle", RunFullCycleAsync)
            .WithSummary("Run Stage 1 then Stage 2 for all active registries");

        group.MapPost("/provision-baseline", ProvisionBaselineAsync)
            .WithSummary("Re-run Phase 03 baseline provisioning function");

        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        if (!await RelationExistsAsync(connection, "public.source_table_dump_registry", cancellationToken))
        {
            return Results.Ok(new
            {
                isReady = false,
                generatedAtUtc = DateTime.UtcNow,
                message = "Phase 03 SQL foundation is not installed yet. Apply Backend/database/scripts/130_phase03_two_stage_delta_import_architecture.sql.",
                sourceTables = Array.Empty<object>(),
                recentRuns = Array.Empty<object>(),
                jobs = Array.Empty<object>(),
                summary = Array.Empty<object>()
            });
        }

        var sourceTables = await ReadRowsAsync(
            connection,
            """
            SELECT
                id::text,
                source_system_code,
                source_schema_name,
                source_table_name,
                dump_schema_name,
                dump_table_name,
                primary_key_columns,
                last_index_column,
                last_index_value_text,
                last_index_value_type,
                source_column_count,
                source_shape_hash,
                stage1_status,
                stage2_status,
                last_stage1_inserted_rows,
                last_stage2_canonical_rows,
                last_synced_at_utc,
                last_error,
                import_cycle_minutes,
                hmi_refresh_seconds,
                is_active
            FROM public.source_table_dump_registry
            WHERE is_deleted = false
            ORDER BY source_schema_name, source_table_name;
            """,
            cancellationToken);

        var recentRuns = await ReadRowsAsync(
            connection,
            """
            SELECT
                id::text,
                registry_id::text,
                run_kind,
                run_status,
                source_schema_name,
                source_table_name,
                dump_schema_name,
                dump_table_name,
                started_at_utc,
                completed_at_utc,
                duration_ms,
                inserted_rows,
                canonical_rows,
                last_index_before,
                last_index_after,
                message,
                failure_reason
            FROM public.two_stage_import_runs
            ORDER BY started_at_utc DESC
            LIMIT 20;
            """,
            cancellationToken);

        var jobs = await ReadRowsAsync(
            connection,
            """
            SELECT
                id::text,
                job_code,
                job_name,
                job_type,
                job_category,
                stage_key,
                schedule_expression,
                is_enabled,
                last_run_status,
                last_run_started_at_utc,
                last_run_completed_at_utc,
                last_run_duration_ms,
                last_failure_reason,
                last_success_row_count,
                last_failed_row_count,
                consecutive_failure_count,
                last_timeout_seconds
            FROM public.job_definitions
            WHERE is_deleted = false
              AND (
                    job_category IS NOT NULL
                    OR job_type IN ('MlParamsVsDefects', 'MlParamsVsDowntime', 'MlParamsVsKpis', 'MlWeeklyFull')
                    OR job_code ILIKE '%ML%'
                    OR job_code ILIKE '%STAGE%'
                    OR job_code ILIKE '%DELTA%'
                    OR job_code ILIKE '%CANONICAL%'
              )
            ORDER BY
                CASE
                    WHEN stage_key = 'Stage1DeltaImport' THEN 1
                    WHEN stage_key = 'Stage2CanonicalRefresh' THEN 2
                    WHEN job_type ILIKE 'Ml%' THEN 3
                    ELSE 9
                END,
                job_code;
            """,
            cancellationToken);

        var summary = await ReadRowsAsync(
            connection,
            """
            SELECT 'Registered source tables' AS metric, count(*)::text AS value
            FROM public.source_table_dump_registry
            WHERE is_deleted = false
            UNION ALL
            SELECT 'Active source tables', count(*)::text
            FROM public.source_table_dump_registry
            WHERE is_deleted = false AND is_active = true
            UNION ALL
            SELECT 'Dump tables', count(*)::text
            FROM information_schema.tables
            WHERE table_schema = 'dump_store'
            UNION ALL
            SELECT 'Recent runs', count(*)::text
            FROM public.two_stage_import_runs
            WHERE started_at_utc >= now() - interval '24 hours'
            UNION ALL
            SELECT 'Failed recent runs', count(*)::text
            FROM public.two_stage_import_runs
            WHERE started_at_utc >= now() - interval '24 hours'
              AND run_status <> 'Ok';
            """,
            cancellationToken);

        return Results.Ok(new
        {
            isReady = true,
            generatedAtUtc = DateTime.UtcNow,
            message = "Phase 03 two-stage import runtime is ready. Stage 1 preserves source-shaped dump copies; Stage 2 refreshes the generic/canonical schema.",
            sourceTables,
            recentRuns,
            jobs,
            summary
        });
    }

    private static async Task<IResult> GetSourceTablesAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        if (!await RelationExistsAsync(connection, "public.source_table_dump_registry", cancellationToken))
            return Results.NotFound(new { message = "Phase 03 registry table is not installed." });

        var rows = await ReadRowsAsync(
            connection,
            """
            SELECT *
            FROM public.source_table_dump_registry
            WHERE is_deleted = false
            ORDER BY source_schema_name, source_table_name;
            """,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> GetRunsAsync(
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var maxRows = Math.Clamp(take ?? 50, 1, 200);

        var rows = await ReadRowsAsync(
            connection,
            $"""
            SELECT
                id::text,
                registry_id::text,
                run_kind,
                run_status,
                source_system_code,
                source_schema_name,
                source_table_name,
                dump_schema_name,
                dump_table_name,
                started_at_utc,
                completed_at_utc,
                duration_ms,
                inserted_rows,
                skipped_existing_rows,
                canonical_rows,
                last_index_before,
                last_index_after,
                requested_by,
                message,
                failure_reason,
                result_json
            FROM public.two_stage_import_runs
            ORDER BY started_at_utc DESC
            LIMIT {maxRows};
            """,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> RunStage1Async(
        RunTwoStageImportRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var rows = request.RegistryId.HasValue
            ? await ReadRowsAsync(
                connection,
                """
                SELECT *
                FROM public.ppiq_run_stage1_delta_import(
                    CAST(@registryId AS uuid),
                    @requestedBy,
                    @maxRows,
                    @timeoutSeconds);
                """,
                cancellationToken,
                command =>
                {
                    AddParameter(command, "registryId", request.RegistryId.Value);
                    AddParameter(command, "requestedBy", request.RequestedBy ?? "Admin UI");
                    AddParameter(command, "maxRows", request.MaxRows ?? 50000);
                    AddParameter(command, "timeoutSeconds", request.TimeoutSeconds ?? 120);
                })
            : await ReadRowsAsync(
                connection,
                """
                SELECT *
                FROM public.ppiq_run_stage1_delta_import_all(
                    @requestedBy,
                    @maxRows,
                    @timeoutSeconds);
                """,
                cancellationToken,
                command =>
                {
                    AddParameter(command, "requestedBy", request.RequestedBy ?? "Admin UI");
                    AddParameter(command, "maxRows", request.MaxRows ?? 50000);
                    AddParameter(command, "timeoutSeconds", request.TimeoutSeconds ?? 120);
                });

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            stage = "Stage1DeltaImport",
            rows
        });
    }

    private static async Task<IResult> RunStage2Async(
        RunTwoStageImportRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var rows = request.RegistryId.HasValue
            ? await ReadRowsAsync(
                connection,
                """
                SELECT *
                FROM public.ppiq_run_stage2_canonical_refresh(
                    CAST(@registryId AS uuid),
                    @requestedBy,
                    @maxMinutes);
                """,
                cancellationToken,
                command =>
                {
                    AddParameter(command, "registryId", request.RegistryId.Value);
                    AddParameter(command, "requestedBy", request.RequestedBy ?? "Admin UI");
                    AddParameter(command, "maxMinutes", request.MaxMinutes ?? 1);
                })
            : await ReadRowsAsync(
                connection,
                """
                SELECT *
                FROM public.ppiq_run_stage2_canonical_refresh_all(
                    @requestedBy,
                    @maxMinutes);
                """,
                cancellationToken,
                command =>
                {
                    AddParameter(command, "requestedBy", request.RequestedBy ?? "Admin UI");
                    AddParameter(command, "maxMinutes", request.MaxMinutes ?? 1);
                });

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            stage = "Stage2CanonicalRefresh",
            rows
        });
    }

    private static async Task<IResult> RunFullCycleAsync(
        RunTwoStageImportRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var historyId = await StartFullCycleHistoryAsync(connection, request, cancellationToken);

        var rows = await ReadRowsAsync(
            connection,
            """
            SELECT *
            FROM public.ppiq_run_two_stage_full_cycle(
                @requestedBy,
                @maxRows,
                @timeoutSeconds,
                @maxMinutes);
            """,
            cancellationToken,
            command =>
            {
                AddParameter(command, "requestedBy", request.RequestedBy ?? "Admin UI");
                AddParameter(command, "maxRows", request.MaxRows ?? 50000);
                AddParameter(command, "timeoutSeconds", request.TimeoutSeconds ?? 120);
                AddParameter(command, "maxMinutes", request.MaxMinutes ?? 1);
            });

        await FinishFullCycleHistoryAsync(connection, historyId, rows, cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            stage = "TwoStageFullCycle",
            rows
        });
    }

    private static async Task<IResult> ProvisionBaselineAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            SELECT public.ppiq_register_dump_source('MELTSHOP_PG', 'src_meltshop_pg', 'heats', ARRAY['heat_no'], 'source_updated_at_utc', 2, 30);
            SELECT public.ppiq_register_dump_source('MELTSHOP_PG', 'src_meltshop_pg', 'lf_treatment', ARRAY['treatment_id'], 'source_updated_at_utc', 2, 30);
            SELECT public.ppiq_register_dump_source('CASTER_ORACLE', 'src_caster_oracle_shape', 'cast_sequence', ARRAY['sequence_no'], 'last_update_ts', 2, 30);
            SELECT public.ppiq_register_dump_source('CASTER_ORACLE', 'src_caster_oracle_shape', 'cast_pieces', ARRAY['piece_id'], 'last_update_ts', 2, 30);
            SELECT public.ppiq_register_dump_source('HSM_ORACLE', 'src_hsm_oracle_shape', 'hsm_coils', ARRAY['coil_id'], 'last_update_ts', 2, 30);
            SELECT public.ppiq_register_dump_source('HSM_ORACLE', 'src_hsm_oracle_shape', 'hsm_pass_measurements', ARRAY['measurement_id'], 'last_update_ts', 2, 30);
            SELECT public.ppiq_register_dump_source('PICKLING_MSSQL', 'src_pkl_mssql_shape', 'pickle_orders', ARRAY['order_id'], 'modified_at_utc', 2, 30);
            SELECT public.ppiq_register_dump_source('PICKLING_MSSQL', 'src_pkl_mssql_shape', 'qa_lab_results', ARRAY['lab_result_id'], 'modified_at_utc', 2, 30);
            SELECT public.ppiq_register_dump_source('INSPECTION_MYSQL', 'src_inspection_mysql_shape', 'parsytec_surface_defects', ARRAY['defect_row_id'], 'updated_at_utc', 2, 30);
            SELECT public.ppiq_register_dump_source('INSPECTION_MYSQL', 'src_inspection_mysql_shape', 'downtime_events', ARRAY['downtime_id'], 'updated_at_utc', 2, 30);
            """,
            cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            message = "Phase 03 baseline dump-source registry was provisioned."
        });
    }

    private static async Task<Guid> StartFullCycleHistoryAsync(
        DbConnection connection,
        RunTwoStageImportRequest request,
        CancellationToken cancellationToken)
    {
        var value = await ExecuteScalarAsync(
            connection,
            """
            SELECT public.ppiq_start_phase3_job_history(
                'PPIQ_TWO_STAGE_FULL_CYCLE',
                'Two-Stage Import Full Cycle',
                'Custom',
                'TwoStageFullCycle',
                'TwoStageFullCycle',
                'Admin UI',
                @requestedBy,
                NULL,
                @timeoutSeconds,
                jsonb_build_object('maxRows', @maxRows, 'maxMinutes', @maxMinutes)
            );
            """,
            cancellationToken,
            command =>
            {
                AddParameter(command, "requestedBy", request.RequestedBy ?? "Admin UI");
                AddParameter(command, "maxRows", request.MaxRows ?? 50000);
                AddParameter(command, "maxMinutes", request.MaxMinutes ?? 1);
                AddParameter(command, "timeoutSeconds", request.TimeoutSeconds ?? 120);
            });

        return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value)!);
    }

    private static async Task FinishFullCycleHistoryAsync(
        DbConnection connection,
        Guid historyId,
        IReadOnlyList<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken)
    {
        var hasFailure = rows.Any(x =>
            x.TryGetValue("status", out var status) &&
            !string.Equals(Convert.ToString(status), "Ok", StringComparison.OrdinalIgnoreCase));

        var affectedRows = rows.Sum(x =>
        {
            if (!x.TryGetValue("affected_rows", out var value) || value is null || value is DBNull)
                return 0L;

            return Convert.ToInt64(value);
        });

        await ExecuteNonQueryAsync(
            connection,
            """
            SELECT public.ppiq_finish_phase3_job_history(
                CAST(@historyId AS uuid),
                'PPIQ_TWO_STAGE_FULL_CYCLE',
                @status,
                @message,
                @failure,
                0,
                0,
                @affectedRows,
                NULL,
                NULL,
                jsonb_build_object('affectedRows', @affectedRows)
            );
            """,
            cancellationToken,
            command =>
            {
                AddParameter(command, "historyId", historyId);
                AddParameter(command, "status", hasFailure ? "Failed" : "Ok");
                AddParameter(command, "message", hasFailure ? "Two-stage full cycle completed with one or more failed source tables." : "Two-stage full cycle completed successfully.");
                AddParameter(command, "failure", hasFailure ? "One or more source tables failed. Check two_stage_import_runs." : DBNull.Value);
                AddParameter(command, "affectedRows", affectedRows);
            });
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static async Task<bool> RelationExistsAsync(
        DbConnection connection,
        string relationName,
        CancellationToken cancellationToken)
    {
        var value = await ExecuteScalarAsync(
            connection,
            "SELECT to_regclass(@relationName);",
            cancellationToken,
            command => AddParameter(command, "relationName", relationName));

        return value is not null && value is not DBNull;
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        Action<DbCommand>? bind = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command);

        var rows = new List<Dictionary<string, object?>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[ToCamelCase(reader.GetName(i))] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task<object?> ExecuteScalarAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        Action<DbCommand>? bind = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        Action<DbCommand>? bind = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return value;

        static string UpperFirst(string x) =>
            x.Length == 0 ? x : char.ToUpperInvariant(x[0]) + x[1..];

        var first = parts[0].ToLowerInvariant();
        return first + string.Concat(parts.Skip(1).Select(x => UpperFirst(x.ToLowerInvariant())));
    }

    private sealed record RunTwoStageImportRequest(
        Guid? RegistryId,
        string? RequestedBy,
        int? MaxRows,
        int? TimeoutSeconds,
        int? MaxMinutes);
}