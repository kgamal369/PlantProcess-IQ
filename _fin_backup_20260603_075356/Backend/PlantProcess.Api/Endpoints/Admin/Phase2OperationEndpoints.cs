using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class Phase2OperationEndpoints
{
    public static IEndpointRouteBuilder MapPhase2OperationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/phase2")
            .WithTags("Admin - Phase 2 Operations")
            .RequireAuthorization("PlantProcessAdmin");

        // PPIQ-WF-008
        group.MapPost("/cross-source/preview-join", PreviewCrossSourceJoinAsync)
            .WithName("PreviewPhase2CrossSourceJoin")
            .Produces<CrossSourceJoinPreviewResponse>();

        group.MapPost("/cross-source/save-join-view", SaveCrossSourceJoinViewAsync)
            .WithName("SavePhase2CrossSourceJoinView")
            .Produces<CrossSourceJoinViewSavedResponse>();

        // PPIQ-WF-009
        group.MapGet("/kpi-parameter-bindings", GetKpiParameterBindingsAsync)
            .WithName("GetPhase2KpiParameterBindings")
            .Produces<KpiParameterBindingListResponse>();

        group.MapPost("/kpi-parameter-bindings", CreateKpiParameterBindingAsync)
            .WithName("CreatePhase2KpiParameterBinding")
            .Produces<KpiParameterBindingRow>();

        // PPIQ-WF-011
        group.MapPost("/jobs/{jobDefinitionId:guid}/run-now", RunJobNowAsync)
            .WithName("RunPhase2JobNow")
            .Produces<object>();

        group.MapPost("/jobs/{jobDefinitionId:guid}/retry", RetryJobAsync)
            .WithName("RetryPhase2Job")
            .Produces<object>();

        group.MapPost("/jobs/{jobDefinitionId:guid}/enable", EnableJobAsync)
            .WithName("EnablePhase2Job")
            .Produces<JobActionSurfaceResponse>();

        group.MapPost("/jobs/{jobDefinitionId:guid}/disable", DisableJobAsync)
            .WithName("DisablePhase2Job")
            .Produces<JobActionSurfaceResponse>();

        // PPIQ-HARD-026
        group.MapGet("/operations/progress/recent", GetRecentOperationProgressAsync)
            .WithName("GetPhase2RecentOperationProgress")
            .Produces<OperationProgressListResponse>();

        group.MapGet("/operations/progress/{operationId:guid}", GetOperationProgressAsync)
            .WithName("GetPhase2OperationProgress")
            .Produces<OperationProgressRow>();

        group.MapPost("/operations/progress", UpsertOperationProgressAsync)
            .WithName("UpsertPhase2OperationProgress")
            .Produces<OperationProgressRow>();

        return app;
    }

    private static async Task<IResult> PreviewCrossSourceJoinAsync(
        [FromBody] CrossSourceJoinPreviewRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateJoinRequest(request);

        if (validation is not null)
            return Results.BadRequest(new { message = validation });

        var sql = BuildJoinSql(request, request.MaxRows ?? 100);

        var preview = await ExecutePreviewAsync(
            dbContext,
            sql,
            Math.Clamp(request.TimeoutSeconds ?? 15, 1, 60),
            cancellationToken);

        return Results.Ok(new CrossSourceJoinPreviewResponse(
            IsSuccess: true,
            Message: "Join preview executed.",
            SqlText: sql,
            RowCount: preview.Rows.Count,
            DurationMs: preview.DurationMs,
            Columns: preview.Columns,
            Rows: preview.Rows));
    }

    private static async Task<IResult> SaveCrossSourceJoinViewAsync(
        [FromBody] SaveCrossSourceJoinViewRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateJoinRequest(request.Join);

        if (validation is not null)
            return Results.BadRequest(new { message = validation });

        if (string.IsNullOrWhiteSpace(request.SchemaViewCode))
            return Results.BadRequest(new { message = "SchemaViewCode is required." });

        if (string.IsNullOrWhiteSpace(request.SchemaViewName))
            return Results.BadRequest(new { message = "SchemaViewName is required." });

        var duplicate = await dbContext.SchemaViewDefinitions
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.SchemaViewCode == request.SchemaViewCode.Trim(),
                cancellationToken);

        if (duplicate)
            return Results.Conflict(new { message = "Schema view code already exists." });

        var sql = BuildJoinSql(request.Join, request.Join.MaxRows ?? 500);

        var sourceIds = JsonSerializer.Serialize(new[]
        {
            request.Join.Left.SourceDatasetDefinitionId,
            request.Join.Right.SourceDatasetDefinitionId
        });

        var entity = new SchemaViewDefinition(
            schemaViewCode: request.SchemaViewCode,
            schemaViewName: request.SchemaViewName,
            viewKind: "JoinView",
            sqlText: sql,
            isSynthetic: request.IsSynthetic,
            primarySourceDatasetDefinitionId: request.Join.Left.SourceDatasetDefinitionId,
            sourceDatasetIdsJson: sourceIds,
            maxPreviewRows: Math.Clamp(request.Join.MaxRows ?? 500, 1, 5000),
            timeoutSeconds: Math.Clamp(request.Join.TimeoutSeconds ?? 15, 1, 60),
            description: request.Description,
            sourceSystem: "PlantProcessIQ.Phase2.CrossSourceJoin",
            sourceRecordId: request.SchemaViewCode);

        dbContext.SchemaViewDefinitions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new CrossSourceJoinViewSavedResponse(
            entity.Id,
            entity.SchemaViewCode,
            entity.SchemaViewName,
            entity.ViewKind,
            entity.SqlText,
            entity.IsApproved,
            entity.IsActive));
    }

    private static async Task<IResult> GetKpiParameterBindingsAsync(
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<KpiParameterBindingRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                b.id,
                b.kpi_code,
                b.kpi_name,
                b.parameter_definition_id,
                p.parameter_code,
                p.parameter_name,
                b.aggregation_method,
                b.unit_of_measure,
                b.filter_json::text,
                b.is_active,
                b.created_at_utc,
                b.updated_at_utc
            FROM public.kpi_parameter_bindings b
            JOIN public.parameter_definitions p
                ON p.id = b.parameter_definition_id
            WHERE b.is_deleted = false
            ORDER BY b.kpi_code
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(ReadKpiBinding(reader));
        }

        return Results.Ok(new KpiParameterBindingListResponse(DateTime.UtcNow, rows));
    }

    private static async Task<IResult> CreateKpiParameterBindingAsync(
        [FromBody] CreateKpiParameterBindingRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.KpiCode))
            return Results.BadRequest(new { message = "KpiCode is required." });

        if (string.IsNullOrWhiteSpace(request.KpiName))
            return Results.BadRequest(new { message = "KpiName is required." });

        if (string.IsNullOrWhiteSpace(request.ParameterCode))
            return Results.BadRequest(new { message = "ParameterCode is required." });

        var parameterCode = NormalizeCode(request.ParameterCode);

        var parameter = await dbContext.ParameterDefinitions
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                x.ParameterCode == parameterCode,
                cancellationToken);

        if (parameter is null)
        {
            parameter = new PlantProcess.Domain.Entities.Process.ParameterDefinition(
                parameterCode: parameterCode,
                parameterName: request.ParameterName ?? parameterCode,
                valueType: request.ValueType ?? "Numeric",
                unitOfMeasure: request.UnitOfMeasure,
                parameterCategory: request.ParameterCategory ?? "KPI",
                industryTemplate: request.IndustryTemplate ?? "GenericManufacturing",
                isSynthetic: request.IsSynthetic,
                sourceSystem: "PlantProcessIQ.Phase2.KpiBinding",
                sourceRecordId: request.KpiCode);

            if (request.ExpectedMinValue.HasValue || request.ExpectedMaxValue.HasValue)
                parameter.SetExpectedRange(request.ExpectedMinValue, request.ExpectedMaxValue);

            dbContext.ParameterDefinitions.Add(parameter);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var id = Guid.NewGuid();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO public.kpi_parameter_bindings
            (
                id,
                kpi_code,
                kpi_name,
                parameter_definition_id,
                aggregation_method,
                unit_of_measure,
                filter_json,
                is_active,
                is_synthetic,
                source_system,
                source_record_id,
                created_at_utc
            )
            VALUES
            (
                {0},
                {1},
                {2},
                {3},
                {4},
                {5},
                CAST({6} AS jsonb),
                {7},
                {8},
                {9},
                {10},
                now()
            )
            """,
            id,
            NormalizeCode(request.KpiCode),
            request.KpiName.Trim(),
            parameter.Id,
            string.IsNullOrWhiteSpace(request.AggregationMethod) ? "Average" : request.AggregationMethod.Trim(),
            request.UnitOfMeasure,
            string.IsNullOrWhiteSpace(request.FilterJson) ? "{}" : request.FilterJson,
            true,
            request.IsSynthetic,
            "PlantProcessIQ.Phase2.KpiBinding",
            request.KpiCode,
            cancellationToken);

        var row = new KpiParameterBindingRow(
            id,
            NormalizeCode(request.KpiCode),
            request.KpiName.Trim(),
            parameter.Id,
            parameter.ParameterCode,
            parameter.ParameterName,
            string.IsNullOrWhiteSpace(request.AggregationMethod) ? "Average" : request.AggregationMethod.Trim(),
            request.UnitOfMeasure,
            string.IsNullOrWhiteSpace(request.FilterJson) ? "{}" : request.FilterJson,
            true,
            DateTime.UtcNow,
            null);

        return Results.Ok(row);
    }

    private static async Task<IResult> RunJobNowAsync(
        Guid jobDefinitionId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        [FromServices] IJobRunOrchestratorService orchestrator,
        CancellationToken cancellationToken)
    {
        var result = await orchestrator.RunNowAsync(
            jobDefinitionId,
            user.Identity?.Name ?? "Admin",
            httpContext.TraceIdentifier,
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { message = result.Error?.Message ?? "Run Now failed." });
    }

    private static async Task<IResult> RetryJobAsync(
        Guid jobDefinitionId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        [FromServices] IJobRunOrchestratorService orchestrator,
        CancellationToken cancellationToken)
    {
        var result = await orchestrator.RunNowAsync(
            jobDefinitionId,
            user.Identity?.Name ?? "Admin",
            $"Retry-{httpContext.TraceIdentifier}",
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { message = result.Error?.Message ?? "Retry failed." });
    }

    private static async Task<IResult> EnableJobAsync(
        Guid jobDefinitionId,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);

        if (job is null)
            return Results.NotFound(new { message = "Job definition not found." });

        job.Enable(job.NextRunAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new JobActionSurfaceResponse(
            job.Id,
            job.JobCode,
            job.JobName,
            true,
            "Job enabled."));
    }

    private static async Task<IResult> DisableJobAsync(
        Guid jobDefinitionId,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);

        if (job is null)
            return Results.NotFound(new { message = "Job definition not found." });

        job.Disable();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new JobActionSurfaceResponse(
            job.Id,
            job.JobCode,
            job.JobName,
            false,
            "Job disabled."));
    }

    private static async Task<IResult> GetRecentOperationProgressAsync(
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<OperationProgressRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                operation_code,
                operation_type,
                operation_name,
                status,
                percent_complete,
                current_step,
                total_steps,
                completed_steps,
                message,
                started_at_utc,
                completed_at_utc,
                failed_at_utc,
                failure_reason,
                correlation_id,
                requested_by,
                metadata_json::text
            FROM public.long_operation_progress
            ORDER BY started_at_utc DESC
            LIMIT 50
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadProgress(reader));

        return Results.Ok(new OperationProgressListResponse(DateTime.UtcNow, rows));
    }

    private static async Task<IResult> GetOperationProgressAsync(
        Guid operationId,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                operation_code,
                operation_type,
                operation_name,
                status,
                percent_complete,
                current_step,
                total_steps,
                completed_steps,
                message,
                started_at_utc,
                completed_at_utc,
                failed_at_utc,
                failure_reason,
                correlation_id,
                requested_by,
                metadata_json::text
            FROM public.long_operation_progress
            WHERE id = @id
            """;

        command.Parameters.AddWithValue("id", operationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return Results.NotFound(new { message = "Operation progress not found." });

        return Results.Ok(ReadProgress(reader));
    }

    private static async Task<IResult> UpsertOperationProgressAsync(
        [FromBody] UpsertOperationProgressRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var id = request.Id ?? Guid.NewGuid();
        var percent = Math.Clamp(request.PercentComplete, 0m, 100m);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO public.long_operation_progress
            (
                id,
                operation_code,
                operation_type,
                operation_name,
                status,
                percent_complete,
                current_step,
                total_steps,
                completed_steps,
                message,
                completed_at_utc,
                failed_at_utc,
                failure_reason,
                correlation_id,
                requested_by,
                metadata_json,
                updated_at_utc
            )
            VALUES
            (
                {0},
                {1},
                {2},
                {3},
                {4},
                {5},
                {6},
                {7},
                {8},
                {9},
                {10},
                {11},
                {12},
                {13},
                {14},
                CAST({15} AS jsonb),
                now()
            )
            ON CONFLICT (id)
            DO UPDATE SET
                status = EXCLUDED.status,
                percent_complete = EXCLUDED.percent_complete,
                current_step = EXCLUDED.current_step,
                total_steps = EXCLUDED.total_steps,
                completed_steps = EXCLUDED.completed_steps,
                message = EXCLUDED.message,
                completed_at_utc = EXCLUDED.completed_at_utc,
                failed_at_utc = EXCLUDED.failed_at_utc,
                failure_reason = EXCLUDED.failure_reason,
                metadata_json = EXCLUDED.metadata_json,
                updated_at_utc = now()
            """,
            id,
            request.OperationCode,
            request.OperationType,
            request.OperationName,
            request.Status,
            percent,
            request.CurrentStep,
            request.TotalSteps,
            request.CompletedSteps,
            request.Message,
            request.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            request.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            request.FailureReason,
            request.CorrelationId,
            request.RequestedBy,
            string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}" : request.MetadataJson,
            cancellationToken);

        return await GetOperationProgressAsync(id, dbContext, cancellationToken);
    }

    private static async Task<PreviewResult> ExecutePreviewAsync(
        PlantProcessDbContext dbContext,
        string sql,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;

        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<SchemaViewPreviewColumn>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(new SchemaViewPreviewColumn(
                reader.GetName(i),
                reader.GetDataTypeName(i),
                i));
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();

            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

            rows.Add(row);
        }

        started.Stop();

        return new PreviewResult(columns, rows, started.ElapsedMilliseconds);
    }

    private static string? ValidateJoinRequest(CrossSourceJoinPreviewRequest request)
    {
        if (request.Left is null || request.Right is null)
            return "Left and Right join sides are required.";

        if (string.IsNullOrWhiteSpace(request.Left.SourceObjectName))
            return "Left.SourceObjectName is required.";

        if (string.IsNullOrWhiteSpace(request.Right.SourceObjectName))
            return "Right.SourceObjectName is required.";

        if (string.IsNullOrWhiteSpace(request.Left.JoinField))
            return "Left.JoinField is required.";

        if (string.IsNullOrWhiteSpace(request.Right.JoinField))
            return "Right.JoinField is required.";

        if (!AllowedJoinTypes.Contains(NormalizeJoinType(request.JoinType)))
            return "JoinType must be Inner or Left.";

        return null;
    }

    private static string BuildJoinSql(CrossSourceJoinPreviewRequest request, int maxRows)
    {
        var joinType = NormalizeJoinType(request.JoinType);
        var limit = Math.Clamp(maxRows, 1, 5000);

        var leftAlias = "l";
        var rightAlias = "r";

        var leftObject = BuildQualifiedPgObject(request.Left.SourceSchemaName, request.Left.SourceObjectName);
        var rightObject = BuildQualifiedPgObject(request.Right.SourceSchemaName, request.Right.SourceObjectName);

        return
            $"""
            SELECT
                {leftAlias}.*,
                row_to_json({rightAlias}) AS joined_record_json
            FROM {leftObject} AS {leftAlias}
            {joinType} JOIN {rightObject} AS {rightAlias}
                ON {leftAlias}.{QuotePgIdentifier(request.Left.JoinField)}
                 = {rightAlias}.{QuotePgIdentifier(request.Right.JoinField)}
            LIMIT {limit}
            """;
    }

    private static string BuildQualifiedPgObject(string? schema, string objectName)
    {
        var safeSchema = string.IsNullOrWhiteSpace(schema)
            ? "public"
            : schema.Trim();

        return $"{QuotePgIdentifier(safeSchema)}.{QuotePgIdentifier(objectName)}";
    }

    private static string QuotePgIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Identifier is required.");

        var clean = value.Trim().Trim('"');

        if (clean.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')))
            throw new InvalidOperationException($"Unsafe identifier: {value}");

        if (clean.Contains('.'))
            throw new InvalidOperationException($"Identifier must not include dot: {value}");

        return "\"" + clean.Replace("\"", "\"\"") + "\"";
    }

    private static string NormalizeJoinType(string? joinType)
    {
        return joinType?.Trim().ToLowerInvariant() switch
        {
            "left" or "leftjoin" or "left join" => "LEFT",
            _ => "INNER"
        };
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_").Replace("-", "_");
    }

    private static KpiParameterBindingRow ReadKpiBinding(IDataRecord reader)
    {
        return new KpiParameterBindingRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            reader.GetBoolean(9),
            reader.GetDateTime(10),
            reader.IsDBNull(11) ? null : reader.GetDateTime(11));
    }

    private static OperationProgressRow ReadProgress(IDataRecord reader)
    {
        return new OperationProgressRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetInt32(7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetDateTime(10),
            reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.GetString(16));
    }

    private static readonly HashSet<string> AllowedJoinTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INNER",
        "LEFT"
    };

    private sealed record PreviewResult(
        IReadOnlyList<SchemaViewPreviewColumn> Columns,
        IReadOnlyList<Dictionary<string, object?>> Rows,
        long DurationMs);

    private static object DbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static object DbValue(Guid? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(DateTime? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }
}

public sealed record CrossSourceJoinSide(
    Guid? SourceDatasetDefinitionId,
    string? SourceSchemaName,
    string SourceObjectName,
    string JoinField);

public sealed record CrossSourceJoinPreviewRequest(
    CrossSourceJoinSide Left,
    CrossSourceJoinSide Right,
    string? JoinType,
    int? MaxRows,
    int? TimeoutSeconds);

public sealed record SaveCrossSourceJoinViewRequest(
    string SchemaViewCode,
    string SchemaViewName,
    CrossSourceJoinPreviewRequest Join,
    string? Description,
    bool IsSynthetic);

public sealed record CrossSourceJoinPreviewResponse(
    bool IsSuccess,
    string Message,
    string SqlText,
    int RowCount,
    long DurationMs,
    IReadOnlyList<SchemaViewPreviewColumn> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed record CrossSourceJoinViewSavedResponse(
    Guid Id,
    string SchemaViewCode,
    string SchemaViewName,
    string ViewKind,
    string SqlText,
    bool IsApproved,
    bool IsActive);

public sealed record KpiParameterBindingListResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<KpiParameterBindingRow> Rows);

public sealed record KpiParameterBindingRow(
    Guid Id,
    string KpiCode,
    string KpiName,
    Guid ParameterDefinitionId,
    string ParameterCode,
    string ParameterName,
    string AggregationMethod,
    string? UnitOfMeasure,
    string FilterJson,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateKpiParameterBindingRequest(
    string KpiCode,
    string KpiName,
    string ParameterCode,
    string? ParameterName,
    string? ValueType,
    string? UnitOfMeasure,
    string? ParameterCategory,
    string? IndustryTemplate,
    decimal? ExpectedMinValue,
    decimal? ExpectedMaxValue,
    string? AggregationMethod,
    string? FilterJson,
    bool IsSynthetic);

public sealed record JobActionSurfaceResponse(
    Guid JobDefinitionId,
    string JobCode,
    string JobName,
    bool IsEnabled,
    string Message);

public sealed record OperationProgressListResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<OperationProgressRow> Rows);

public sealed record OperationProgressRow(
    Guid Id,
    string OperationCode,
    string OperationType,
    string OperationName,
    string Status,
    decimal PercentComplete,
    string? CurrentStep,
    int? TotalSteps,
    int? CompletedSteps,
    string? Message,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    string? FailureReason,
    string? CorrelationId,
    string? RequestedBy,
    string MetadataJson);

public sealed record UpsertOperationProgressRequest(
    Guid? Id,
    string OperationCode,
    string OperationType,
    string OperationName,
    string Status,
    decimal PercentComplete,
    string? CurrentStep,
    int? TotalSteps,
    int? CompletedSteps,
    string? Message,
    string? FailureReason,
    string? CorrelationId,
    string? RequestedBy,
    string? MetadataJson);