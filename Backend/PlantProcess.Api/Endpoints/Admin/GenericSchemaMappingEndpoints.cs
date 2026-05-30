using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

/// <summary>
/// PlantProcess IQ v4 Phase 02 endpoints.
/// Implements PPIQ-T107..T112:
/// - canonical schema-view catalog,
/// - SchemaViewResolver contract,
/// - safe cross-source join authoring,
/// - KPI-as-view authoring,
/// - mapping execution / refresh proof.
/// </summary>
public static class GenericSchemaMappingEndpoints
{
    private static readonly Regex SafeIdentifier = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DangerousSql = new(
        @"\b(insert|update|delete|drop|alter|truncate|grant|revoke|copy|execute|exec|merge|vacuum|analyze|call|do|listen|notify|set|reset|prepare|deallocate|create\s+(?!or\s+replace\s+view)|pg_read_file|pg_sleep|dblink|xp_cmdshell|openrowset|opendatasource)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEndpointRouteBuilder MapGenericSchemaMappingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/schema-mapping")
            .WithTags("Admin - Generic Schema Mapping")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/catalog", GetCatalogAsync)
            .WithName("GetCanonicalSchemaViewCatalog")
            .WithSummary("PPIQ-T107: list canonical schema view catalog");

        group.MapPost("/catalog/register", RegisterCanonicalViewAsync)
            .WithName("RegisterCanonicalSchemaView")
            .WithSummary("PPIQ-T107: register and validate a canonical schema view");

        group.MapPost("/resolve", ResolveSchemaViewAsync)
            .WithName("ResolveCanonicalSchemaView")
            .WithSummary("PPIQ-T108: resolve a widget/mapping target to an approved physical view");

        group.MapPost("/joins/preview", PreviewJoinAsync)
            .WithName("PreviewGenericCrossSourceJoin")
            .WithSummary("PPIQ-T110: preview a cross-source join");

        group.MapPost("/joins/materialize", MaterializeJoinAsync)
            .WithName("MaterializeGenericCrossSourceJoin")
            .WithSummary("PPIQ-T110: materialize a cross-source join as a canonical view");

        group.MapPost("/kpi-views", CreateKpiViewAsync)
            .WithName("CreateGenericKpiView")
            .WithSummary("PPIQ-T111: create KPI-as-view and attach it to equipment/area/process");

        group.MapPost("/execute/{viewCode}", ExecuteMappingAsync)
            .WithName("ExecuteCanonicalSchemaMapping")
            .WithSummary("PPIQ-T112: execute/refresh a saved mapping view and log row counts");

        group.MapGet("/readiness", GetReadinessAsync)
            .WithName("GetGenericSchemaMappingReadiness")
            .WithSummary("PPIQ-T107-T112 readiness summary");

        return app;
    }

    private static async Task<IResult> GetCatalogAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        const string sql = """
        SELECT
            id,
            view_code,
            view_name,
            view_kind,
            target_entity,
            physical_schema,
            physical_view_name,
            sql_text,
            output_schema_json::text,
            mapping_json::text,
            source_dataset_ids::text,
            attached_scope_type,
            attached_scope_code,
            is_registered,
            is_approved,
            is_active,
            is_system_seed,
            last_validated_at_utc,
            last_validation_status,
            last_validation_message,
            last_executed_at_utc,
            last_execution_status,
            last_execution_message,
            last_execution_row_count,
            created_by,
            created_at_utc,
            updated_at_utc
        FROM public.canonical_schema_views
        WHERE is_deleted = false
        ORDER BY
            CASE view_kind
                WHEN 'MappingPreparationView' THEN 1
                WHEN 'JoinView' THEN 2
                WHEN 'KpiView' THEN 3
                ELSE 9
            END,
            view_code;
        """;

        var rows = await QueryAsync(db, sql, cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> RegisterCanonicalViewAsync(
        [FromBody] RegisterCanonicalViewRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        var validation = ValidateRegisterRequest(request);
        if (validation is not null)
            return Results.BadRequest(new { message = validation });

        var physicalSchema = CleanIdentifier(request.PhysicalSchema ?? "public", "physicalSchema");
        var physicalViewName = CleanIdentifier(
            string.IsNullOrWhiteSpace(request.PhysicalViewName)
                ? $"cv_{NormalizeCode(request.ViewCode)}"
                : request.PhysicalViewName!,
            "physicalViewName");

        var selectSql = NormalizeSelectSql(request.SqlText);
        var outputSchema = await PreviewSchemaOnlyAsync(db, selectSql, cancellationToken);

        await CreateOrReplaceViewAsync(db, physicalSchema, physicalViewName, selectSql, cancellationToken);

        var id = await UpsertCatalogAsync(
            db,
            request.ViewCode,
            request.ViewName,
            request.ViewKind,
            request.TargetEntity,
            physicalSchema,
            physicalViewName,
            selectSql,
            JsonSerializer.Serialize(outputSchema),
            string.IsNullOrWhiteSpace(request.MappingJson) ? "{}" : request.MappingJson!,
            string.IsNullOrWhiteSpace(request.SourceDatasetIdsJson) ? "[]" : request.SourceDatasetIdsJson!,
            request.AttachedScopeType,
            request.AttachedScopeCode,
            request.IsSystemSeed,
            GetActor(user),
            "Registered by /admin/schema-mapping/catalog/register.",
            cancellationToken);

        var row = await GetCatalogByIdAsync(db, id, cancellationToken);
        return Results.Created($"/admin/schema-mapping/catalog/{id}", row);
    }

    private static async Task<IResult> ResolveSchemaViewAsync(
        [FromBody] ResolveSchemaViewRequest request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ViewCode) && string.IsNullOrWhiteSpace(request.TargetEntity))
        {
            return Results.BadRequest(new
            {
                message = "Provide either viewCode or targetEntity."
            });
        }

        var sql = """
        SELECT
            id,
            view_code,
            view_name,
            view_kind,
            target_entity,
            physical_schema,
            physical_view_name,
            sql_text,
            output_schema_json::text,
            mapping_json::text,
            source_dataset_ids::text,
            attached_scope_type,
            attached_scope_code,
            is_registered,
            is_approved,
            is_active,
            is_system_seed,
            last_validated_at_utc,
            last_validation_status,
            last_validation_message,
            last_executed_at_utc,
            last_execution_status,
            last_execution_message,
            last_execution_row_count,
            created_by,
            created_at_utc,
            updated_at_utc
        FROM public.canonical_schema_views
        WHERE is_deleted = false
          AND is_active = true
          AND is_approved = true
          AND (
                (@view_code IS NOT NULL AND lower(view_code) = lower(@view_code))
             OR (@target_entity IS NOT NULL AND lower(target_entity) = lower(@target_entity))
          )
        ORDER BY
            CASE WHEN lower(view_code) = lower(COALESCE(@view_code, '')) THEN 0 ELSE 1 END,
            updated_at_utc DESC NULLS LAST,
            created_at_utc DESC
        LIMIT 1;
        """;

        var rows = await QueryAsync(
            db,
            sql,
            cancellationToken,
            ("view_code", EmptyToNull(request.ViewCode)),
            ("target_entity", EmptyToNull(request.TargetEntity)));

        if (rows.Count == 0)
        {
            return Results.NotFound(new
            {
                message = "No active approved canonical schema view matched the resolver request.",
                request.ViewCode,
                request.TargetEntity
            });
        }

        var resolved = rows[0];
        var qualifiedName = $"{resolved["physical_schema"]}.{resolved["physical_view_name"]}";

        return Results.Ok(new
        {
            isResolved = true,
            message = "Schema view resolved.",
            qualifiedName,
            view = resolved
        });
    }

    private static async Task<IResult> PreviewJoinAsync(
        [FromBody] CrossSourceJoinRequest request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        var sql = BuildJoinSql(request, includeLimit: true);
        var preview = await PreviewRowsAsync(db, sql, request.MaxRows ?? 100, cancellationToken);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Cross-source join preview executed.",
            sqlText = sql,
            preview.rowCount,
            preview.columns,
            preview.rows
        });
    }

    private static async Task<IResult> MaterializeJoinAsync(
        [FromBody] MaterializeJoinRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ViewCode))
            return Results.BadRequest(new { message = "viewCode is required." });

        if (string.IsNullOrWhiteSpace(request.ViewName))
            return Results.BadRequest(new { message = "viewName is required." });

        var sql = BuildJoinSql(request.Join, includeLimit: false);
        var physicalName = CleanIdentifier(
            string.IsNullOrWhiteSpace(request.PhysicalViewName)
                ? $"cv_{NormalizeCode(request.ViewCode)}"
                : request.PhysicalViewName!,
            "physicalViewName");

        var schema = CleanIdentifier(request.PhysicalSchema ?? "public", "physicalSchema");
        var outputSchema = await PreviewSchemaOnlyAsync(db, sql, cancellationToken);

        await CreateOrReplaceViewAsync(db, schema, physicalName, sql, cancellationToken);

        var id = await UpsertCatalogAsync(
            db,
            request.ViewCode,
            request.ViewName,
            "JoinView",
            request.TargetEntity ?? "MappingPreparation",
            schema,
            physicalName,
            sql,
            JsonSerializer.Serialize(outputSchema),
            request.MappingJson ?? "{}",
            request.SourceDatasetIdsJson ?? "[]",
            request.AttachedScopeType,
            request.AttachedScopeCode,
            false,
            GetActor(user),
            "Materialized by /admin/schema-mapping/joins/materialize.",
            cancellationToken);

        var row = await GetCatalogByIdAsync(db, id, cancellationToken);
        return Results.Ok(row);
    }

    private static async Task<IResult> CreateKpiViewAsync(
        [FromBody] KpiViewRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ViewCode))
            return Results.BadRequest(new { message = "viewCode is required." });

        if (string.IsNullOrWhiteSpace(request.ViewName))
            return Results.BadRequest(new { message = "viewName is required." });

        if (string.IsNullOrWhiteSpace(request.KpiCode))
            return Results.BadRequest(new { message = "kpiCode is required." });

        if (string.IsNullOrWhiteSpace(request.SqlText))
            return Results.BadRequest(new { message = "sqlText is required." });

        var sql = NormalizeSelectSql(request.SqlText);
        var schema = CleanIdentifier(request.PhysicalSchema ?? "public", "physicalSchema");
        var physicalName = CleanIdentifier(
            string.IsNullOrWhiteSpace(request.PhysicalViewName)
                ? $"cv_{NormalizeCode(request.ViewCode)}"
                : request.PhysicalViewName!,
            "physicalViewName");

        var outputSchema = await PreviewSchemaOnlyAsync(db, sql, cancellationToken);
        await CreateOrReplaceViewAsync(db, schema, physicalName, sql, cancellationToken);

        var id = await UpsertCatalogAsync(
            db,
            request.ViewCode,
            request.ViewName,
            "KpiView",
            "KPI",
            schema,
            physicalName,
            sql,
            JsonSerializer.Serialize(outputSchema),
            request.MappingJson ?? JsonSerializer.Serialize(new
            {
                request.KpiCode,
                request.KpiName,
                request.KpiCategory,
                request.Unit
            }),
            "[]",
            request.AttachedScopeType,
            request.AttachedScopeCode,
            false,
            GetActor(user),
            "KPI-as-view registered by /admin/schema-mapping/kpi-views.",
            cancellationToken);

        await TryInsertKpiDefinitionAsync(db, request, id, cancellationToken);

        var row = await GetCatalogByIdAsync(db, id, cancellationToken);
        return Results.Ok(row);
    }

    private static async Task<IResult> ExecuteMappingAsync(
        string viewCode,
        [FromBody] ExecuteMappingRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(viewCode))
            return Results.BadRequest(new { message = "viewCode is required." });

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
            FROM public.canonical_schema_views
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
                UPDATE public.canonical_schema_views
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
                UPDATE public.canonical_schema_views
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
            INSERT INTO public.schema_mapping_executions
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
            FROM public.canonical_schema_views;
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

    private static async Task EnsureCatalogAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            db,
            """
            CREATE EXTENSION IF NOT EXISTS pgcrypto;

            CREATE TABLE IF NOT EXISTS public.canonical_schema_views
            (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                view_code text NOT NULL,
                view_name text NOT NULL,
                view_kind text NOT NULL,
                target_entity text NOT NULL,
                physical_schema text NOT NULL DEFAULT 'public',
                physical_view_name text NOT NULL,
                sql_text text NOT NULL,
                output_schema_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                mapping_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                source_dataset_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                attached_scope_type text NULL,
                attached_scope_code text NULL,
                is_registered boolean NOT NULL DEFAULT true,
                is_approved boolean NOT NULL DEFAULT false,
                is_active boolean NOT NULL DEFAULT true,
                is_system_seed boolean NOT NULL DEFAULT false,
                last_validated_at_utc timestamptz NULL,
                last_validation_status text NULL,
                last_validation_message text NULL,
                last_executed_at_utc timestamptz NULL,
                last_execution_status text NULL,
                last_execution_message text NULL,
                last_execution_row_count integer NULL,
                created_by text NULL,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NULL,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at_utc timestamptz NULL,
                deleted_reason text NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_schema_views_view_code_active
            ON public.canonical_schema_views (lower(view_code))
            WHERE is_deleted = false;

            CREATE TABLE IF NOT EXISTS public.schema_mapping_executions
            (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                canonical_schema_view_id uuid NULL,
                view_code text NOT NULL,
                target_entity text NOT NULL,
                execution_mode text NOT NULL DEFAULT 'ValidateAndRefreshView',
                status text NOT NULL,
                message text NULL,
                row_count integer NOT NULL DEFAULT 0,
                duration_ms integer NOT NULL DEFAULT 0,
                executed_by text NULL,
                started_at_utc timestamptz NOT NULL DEFAULT now(),
                completed_at_utc timestamptz NULL,
                details_json jsonb NOT NULL DEFAULT '{}'::jsonb
            );

            CREATE TABLE IF NOT EXISTS public.canonical_schema_view_audit
            (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                canonical_schema_view_id uuid NULL,
                action_code text NOT NULL,
                action_status text NOT NULL,
                action_message text NULL,
                payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                executed_by text NULL,
                executed_at_utc timestamptz NOT NULL DEFAULT now()
            );
            """,
            cancellationToken);
    }

    private static string? ValidateRegisterRequest(RegisterCanonicalViewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ViewCode))
            return "viewCode is required.";

        if (string.IsNullOrWhiteSpace(request.ViewName))
            return "viewName is required.";

        if (string.IsNullOrWhiteSpace(request.ViewKind))
            return "viewKind is required.";

        if (string.IsNullOrWhiteSpace(request.TargetEntity))
            return "targetEntity is required.";

        if (string.IsNullOrWhiteSpace(request.SqlText))
            return "sqlText is required.";

        return null;
    }

    private static string BuildJoinSql(CrossSourceJoinRequest request, bool includeLimit)
    {
        var leftSchema = CleanIdentifier(request.LeftSchema, "leftSchema");
        var leftTable = CleanIdentifier(request.LeftTable, "leftTable");
        var rightSchema = CleanIdentifier(request.RightSchema, "rightSchema");
        var rightTable = CleanIdentifier(request.RightTable, "rightTable");
        var leftJoinColumn = CleanIdentifier(request.LeftJoinColumn, "leftJoinColumn");
        var rightJoinColumn = CleanIdentifier(request.RightJoinColumn, "rightJoinColumn");

        var selected = request.Columns.Count == 0
            ? new[]
            {
                new JoinColumnSelection("left", leftJoinColumn, "left_join_key"),
                new JoinColumnSelection("right", rightJoinColumn, "right_join_key")
            }
            : request.Columns;

        var columns = selected.Select((c, index) =>
        {
            var side = string.Equals(c.Side, "right", StringComparison.OrdinalIgnoreCase) ? "r" : "l";
            var column = CleanIdentifier(c.Column, $"columns[{index}].column");
            var alias = CleanIdentifier(
                string.IsNullOrWhiteSpace(c.Alias) ? $"{side}_{column}" : c.Alias!,
                $"columns[{index}].alias");

            return $"{side}.{QuoteIdentifier(column)} AS {QuoteIdentifier(alias)}";
        });

        var maxRows = Math.Clamp(request.MaxRows ?? 100, 1, 5000);
        var limit = includeLimit ? $" LIMIT {maxRows}" : "";

        return $"""
        SELECT
            {string.Join("," + Environment.NewLine + "            ", columns)}
        FROM {QuoteIdentifier(leftSchema)}.{QuoteIdentifier(leftTable)} l
        JOIN {QuoteIdentifier(rightSchema)}.{QuoteIdentifier(rightTable)} r
            ON l.{QuoteIdentifier(leftJoinColumn)} = r.{QuoteIdentifier(rightJoinColumn)}
        {limit}
        """;
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

    private static string NormalizeSelectSql(string sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            throw new InvalidOperationException("SQL text is required.");

        var trimmed = StripTrailingSemicolon(sqlText.Trim());

        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only SELECT or WITH SQL is allowed.");
        }

        if (trimmed.Contains(';'))
            throw new InvalidOperationException("Multiple SQL statements are not allowed.");

        if (DangerousSql.IsMatch(trimmed))
            throw new InvalidOperationException("SQL contains a forbidden command or function.");

        return trimmed;
    }

    private static string StripTrailingSemicolon(string value)
    {
        while (value.EndsWith(";", StringComparison.Ordinal))
            value = value[..^1].TrimEnd();

        return value;
    }

    private static async Task<IReadOnlyList<PreviewColumn>> PreviewSchemaOnlyAsync(
        PlantProcessDbContext db,
        string sqlText,
        CancellationToken cancellationToken)
    {
        var preview = await PreviewRowsAsync(db, sqlText, 0, cancellationToken);
        return preview.columns;
    }

    private static async Task<(int rowCount, IReadOnlyList<PreviewColumn> columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)> PreviewRowsAsync(
        PlantProcessDbContext db,
        string sqlText,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSelectSql(sqlText);
        var take = Math.Clamp(maxRows, 0, 5000);

        var wrapped = take == 0
            ? $"SELECT * FROM ({normalized}) ppiq_preview WHERE 1 = 0"
            : $"SELECT * FROM ({normalized}) ppiq_preview LIMIT {take}";

        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = wrapped;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 30;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = reader.GetColumnSchema()
            .Select((c, i) => new PreviewColumn(
                c.ColumnName ?? $"column_{i}",
                c.DataTypeName ?? c.DataType?.Name ?? "unknown",
                i))
            .ToList();

        var rows = new List<IReadOnlyDictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return (rows.Count, columns, rows);
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

    private static async Task<Guid> UpsertCatalogAsync(
        PlantProcessDbContext db,
        string viewCode,
        string viewName,
        string viewKind,
        string targetEntity,
        string physicalSchema,
        string physicalViewName,
        string sqlText,
        string outputSchemaJson,
        string mappingJson,
        string sourceDatasetIdsJson,
        string? attachedScopeType,
        string? attachedScopeCode,
        bool isSystemSeed,
        string actor,
        string message,
        CancellationToken cancellationToken)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO public.canonical_schema_views
            (
                view_code,
                view_name,
                view_kind,
                target_entity,
                physical_schema,
                physical_view_name,
                sql_text,
                output_schema_json,
                mapping_json,
                source_dataset_ids,
                attached_scope_type,
                attached_scope_code,
                is_registered,
                is_approved,
                is_active,
                is_system_seed,
                last_validated_at_utc,
                last_validation_status,
                last_validation_message,
                created_by
            )
            VALUES
            (
                @view_code,
                @view_name,
                @view_kind,
                @target_entity,
                @physical_schema,
                @physical_view_name,
                @sql_text,
                CAST(@output_schema_json AS jsonb),
                CAST(@mapping_json AS jsonb),
                CAST(@source_dataset_ids AS jsonb),
                @attached_scope_type,
                @attached_scope_code,
                true,
                true,
                true,
                @is_system_seed,
                now(),
                'Success',
                @message,
                @created_by
            )
            ON CONFLICT (lower(view_code)) WHERE is_deleted = false
            DO UPDATE SET
                view_name = EXCLUDED.view_name,
                view_kind = EXCLUDED.view_kind,
                target_entity = EXCLUDED.target_entity,
                physical_schema = EXCLUDED.physical_schema,
                physical_view_name = EXCLUDED.physical_view_name,
                sql_text = EXCLUDED.sql_text,
                output_schema_json = EXCLUDED.output_schema_json,
                mapping_json = EXCLUDED.mapping_json,
                source_dataset_ids = EXCLUDED.source_dataset_ids,
                attached_scope_type = EXCLUDED.attached_scope_type,
                attached_scope_code = EXCLUDED.attached_scope_code,
                is_registered = true,
                is_approved = true,
                is_active = true,
                is_system_seed = EXCLUDED.is_system_seed,
                last_validated_at_utc = now(),
                last_validation_status = 'Success',
                last_validation_message = EXCLUDED.last_validation_message,
                updated_at_utc = now()
            RETURNING id;
            """;

        AddParameter(command, "view_code", viewCode.Trim());
        AddParameter(command, "view_name", viewName.Trim());
        AddParameter(command, "view_kind", viewKind.Trim());
        AddParameter(command, "target_entity", targetEntity.Trim());
        AddParameter(command, "physical_schema", physicalSchema.Trim());
        AddParameter(command, "physical_view_name", physicalViewName.Trim());
        AddParameter(command, "sql_text", sqlText.Trim());
        AddParameter(command, "output_schema_json", outputSchemaJson);
        AddParameter(command, "mapping_json", string.IsNullOrWhiteSpace(mappingJson) ? "{}" : mappingJson);
        AddParameter(command, "source_dataset_ids", string.IsNullOrWhiteSpace(sourceDatasetIdsJson) ? "[]" : sourceDatasetIdsJson);
        AddParameter(command, "attached_scope_type", EmptyToNull(attachedScopeType));
        AddParameter(command, "attached_scope_code", EmptyToNull(attachedScopeCode));
        AddParameter(command, "is_system_seed", isSystemSeed);
        AddParameter(command, "message", message);
        AddParameter(command, "created_by", actor);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return (Guid)value!;
    }

    private static async Task<IReadOnlyDictionary<string, object?>> GetCatalogByIdAsync(
        PlantProcessDbContext db,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            db,
            """
            SELECT
                id,
                view_code,
                view_name,
                view_kind,
                target_entity,
                physical_schema,
                physical_view_name,
                sql_text,
                output_schema_json::text,
                mapping_json::text,
                source_dataset_ids::text,
                attached_scope_type,
                attached_scope_code,
                is_registered,
                is_approved,
                is_active,
                is_system_seed,
                last_validated_at_utc,
                last_validation_status,
                last_validation_message,
                last_executed_at_utc,
                last_execution_status,
                last_execution_message,
                last_execution_row_count,
                created_by,
                created_at_utc,
                updated_at_utc
            FROM public.canonical_schema_views
            WHERE id = @id;
            """,
            cancellationToken,
            ("id", id));

        return rows.Single();
    }

    private static async Task TryInsertKpiDefinitionAsync(
        PlantProcessDbContext db,
        KpiViewRequest request,
        Guid schemaViewId,
        CancellationToken cancellationToken)
    {
        var exists = await QueryAsync(
            db,
            "SELECT to_regclass('public.kpi_definitions') AS table_name;",
            cancellationToken);

        if (exists.Count == 0 || exists[0]["table_name"] is null)
            return;

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO public.kpi_definitions
            (
                id,
                schema_view_definition_id,
                kpi_code,
                kpi_name,
                kpi_category,
                value_expression,
                unit,
                dimension_expression,
                filter_expression,
                aggregation_type,
                kpi_options_json,
                is_active,
                is_synthetic,
                source_system,
                source_record_id,
                created_at_utc,
                is_deleted
            )
            VALUES
            (
                gen_random_uuid(),
                NULL,
                @kpi_code,
                @kpi_name,
                @kpi_category,
                @value_expression,
                @unit,
                @dimension_expression,
                @filter_expression,
                @aggregation_type,
                CAST(@kpi_options_json AS jsonb)::text,
                true,
                @is_synthetic,
                'PlantProcessIQ.GenericSchemaMapping',
                @source_record_id,
                now(),
                false
            )
            ON CONFLICT DO NOTHING;
            """,
            cancellationToken,
            ("kpi_code", request.KpiCode.Trim()),
            ("kpi_name", (request.KpiName ?? request.ViewName).Trim()),
            ("kpi_category", request.KpiCategory ?? "Process"),
            ("value_expression", request.ValueExpression ?? "value"),
            ("unit", EmptyToNull(request.Unit)),
            ("dimension_expression", EmptyToNull(request.DimensionExpression)),
            ("filter_expression", EmptyToNull(request.FilterExpression)),
            ("aggregation_type", request.AggregationType ?? "Average"),
            ("kpi_options_json", request.KpiOptionsJson ?? "{}"),
            ("is_synthetic", request.IsSynthetic),
            ("source_record_id", schemaViewId.ToString()));
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 60;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<IReadOnlyDictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task ExecuteNonQueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 60;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string CleanIdentifier(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");

        var trimmed = value.Trim();

        if (!SafeIdentifier.IsMatch(trimmed))
            throw new InvalidOperationException($"{fieldName} contains an unsafe SQL identifier: {trimmed}");

        return trimmed;
    }

    private static string QuoteIdentifier(string value)
    {
        var safe = CleanIdentifier(value, "identifier");
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private static string NormalizeCode(string code)
    {
        var cleaned = Regex.Replace(code.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
        cleaned = Regex.Replace(cleaned, "_+", "_").Trim('_');

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "view";

        if (char.IsDigit(cleaned[0]))
            cleaned = "v_" + cleaned;

        return cleaned;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetActor(ClaimsPrincipal user)
    {
        return user.Identity?.Name
               ?? user.FindFirstValue(ClaimTypes.Name)
               ?? user.FindFirstValue("sub")
               ?? "unknown";
    }

    public sealed record RegisterCanonicalViewRequest(
        string ViewCode,
        string ViewName,
        string ViewKind,
        string TargetEntity,
        string SqlText,
        string? PhysicalSchema,
        string? PhysicalViewName,
        string? OutputSchemaJson,
        string? MappingJson,
        string? SourceDatasetIdsJson,
        string? AttachedScopeType,
        string? AttachedScopeCode,
        bool IsSystemSeed);

    public sealed record ResolveSchemaViewRequest(
        string? ViewCode,
        string? TargetEntity,
        string? WidgetCode,
        string? MeasureCode,
        string? DimensionCode);

    public sealed record JoinColumnSelection(
        string Side,
        string Column,
        string? Alias);

    public sealed record CrossSourceJoinRequest(
        string LeftSchema,
        string LeftTable,
        string RightSchema,
        string RightTable,
        string LeftJoinColumn,
        string RightJoinColumn,
        IReadOnlyList<JoinColumnSelection> Columns,
        int? MaxRows);

    public sealed record MaterializeJoinRequest(
        string ViewCode,
        string ViewName,
        CrossSourceJoinRequest Join,
        string? TargetEntity,
        string? PhysicalSchema,
        string? PhysicalViewName,
        string? MappingJson,
        string? SourceDatasetIdsJson,
        string? AttachedScopeType,
        string? AttachedScopeCode);

    public sealed record KpiViewRequest(
        string ViewCode,
        string ViewName,
        string KpiCode,
        string? KpiName,
        string? KpiCategory,
        string SqlText,
        string? PhysicalSchema,
        string? PhysicalViewName,
        string? Unit,
        string? ValueExpression,
        string? DimensionExpression,
        string? FilterExpression,
        string? AggregationType,
        string? KpiOptionsJson,
        string? MappingJson,
        string? AttachedScopeType,
        string? AttachedScopeCode,
        bool IsSynthetic);

    public sealed record ExecuteMappingRequest(
        string? ExecutionMode,
        bool PreviewOnly,
        bool StopOnFirstError);

    public sealed record PreviewColumn(
        string ColumnName,
        string DataType,
        int Ordinal);
}