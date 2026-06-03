using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Security;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

/// <summary>
/// Phase 4 Schema Configuration / SQL View Layer.
/// 
/// This endpoint group supports:
/// - schema view metadata CRUD,
/// - controlled SQL safety validation,
/// - SELECT/WITH-only preview execution,
/// - KPI metadata registration.
/// 
/// It intentionally does NOT allow arbitrary destructive SQL.
/// </summary>
public static class SchemaConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapSchemaConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/schema-configuration")
        .WithTags("Admin - Schema Configuration")
        .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/views", GetSchemaViewsAsync)
            .WithSummary("Get schema views");

        group.MapGet("/views/{id:guid}", GetSchemaViewByIdAsync)
            .WithSummary("Get schema view by ID");

        group.MapPost("/views", CreateSchemaViewAsync)
            .WithSummary("Create schema view");

        group.MapPut("/views/{id:guid}", UpdateSchemaViewAsync)
            .WithSummary("Update schema view");

        group.MapPost("/views/{id:guid}/preview", PreviewSchemaViewAsync)
            .WithSummary("Preview stored schema view SQL");

        group.MapPost("/views/preview", PreviewAdHocSchemaSqlAsync)
            .WithSummary("Preview ad-hoc schema SQL");

        group.MapPost("/views/{id:guid}/approve", ApproveSchemaViewAsync)
            .WithSummary("Approve schema view");

        group.MapPost("/views/{id:guid}/activate", ActivateSchemaViewAsync)
            .WithSummary("Activate schema view");

        group.MapPost("/views/{id:guid}/deactivate", DeactivateSchemaViewAsync)
            .WithSummary("Deactivate schema view");

        group.MapGet("/kpis", GetKpisAsync)
            .WithSummary("Get KPI definitions");

        group.MapPost("/kpis", CreateKpiAsync)
            .WithSummary("Create KPI definition");

        return app;
    }

    private static async Task<IResult> GetSchemaViewsAsync(
        bool? includeInactive,
        ISchemaConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSchemaViewsAsync(
            includeInactive ?? true,
            cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> GetSchemaViewByIdAsync(
        Guid id,
        ISchemaConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSchemaViewByIdAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> CreateSchemaViewAsync(
        CreateSchemaViewDefinitionRequest request,
        ISchemaConfigurationService service,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.SchemaSqlViewBuilder);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());
        var safety = SafeSqlValidator.Validate(request.SqlText);

        if (!safety.IsValid)
        {
            return Results.BadRequest(new
            {
                message = "SQL safety validation failed.",
                safety
            });
        }

        var result = await service.CreateSchemaViewAsync(request, cancellationToken);

        return result.ToHttpResult(value =>
            Results.Created($"/admin/schema-configuration/views/{value.Id}", value));
    }

    private static async Task<IResult> UpdateSchemaViewAsync(
        Guid id,
        UpdateSchemaViewDefinitionRequest request,
        ISchemaConfigurationService service,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.SchemaSqlViewBuilder);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var safety = SafeSqlValidator.Validate(request.SqlText);

        if (!safety.IsValid)
        {
            return Results.BadRequest(new
            {
                message = "SQL safety validation failed.",
                safety
            });
        }

        var result = await service.UpdateSchemaViewAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> PreviewSchemaViewAsync(
        Guid id,
        SchemaViewPreviewRequest request,
        PlantProcessDbContext dbContext,
        ISchemaConfigurationService service,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var previewGate = licenseService.EnsureFeatureEnabled(LicenseFeature.SchemaPreviewExecution);
        if (previewGate.IsFailure)
            return previewGate.ToHttpResult(() => Results.NoContent());

        var view = await dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (view is null)
            return Results.NotFound(new { message = "Schema view definition not found." });

        var sqlText = string.IsNullOrWhiteSpace(request.SqlText)
            ? view.SqlText
            : request.SqlText;

        var maxRows = Math.Clamp(request.MaxRows ?? view.MaxPreviewRows, 1, 5000);
        var timeoutSeconds = Math.Clamp(request.TimeoutSeconds ?? view.TimeoutSeconds, 1, 120);

        var preview = await ExecuteSafePreviewAsync(
            dbContext,
            sqlText,
            maxRows,
            timeoutSeconds,
            cancellationToken);

        var outputSchemaJson = JsonSerializer.Serialize(preview.Columns);

        await service.MarkSchemaViewValidationAsync(
            id,
            preview.IsSuccess,
            preview.Message,
            preview.IsSuccess ? outputSchemaJson : null,
            cancellationToken);

        return preview.IsSuccess
            ? Results.Ok(preview)
            : Results.BadRequest(preview);
    }

    private static async Task<IResult> PreviewAdHocSchemaSqlAsync(
        SchemaViewPreviewRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SqlText))
            return Results.BadRequest(new { message = "SqlText is required for ad-hoc preview." });

        var maxRows = Math.Clamp(request.MaxRows ?? 50, 1, 5000);
        var timeoutSeconds = Math.Clamp(request.TimeoutSeconds ?? 15, 1, 120);

        var preview = await ExecuteSafePreviewAsync(
            dbContext,
            request.SqlText,
            maxRows,
            timeoutSeconds,
            cancellationToken);

        return preview.IsSuccess
            ? Results.Ok(preview)
            : Results.BadRequest(preview);
    }

    private static async Task<IResult> ApproveSchemaViewAsync(
        Guid id,
        ISchemaConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveSchemaViewAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ActivateSchemaViewAsync(
        Guid id,
        ISchemaConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateSchemaViewAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> DeactivateSchemaViewAsync(
        Guid id,
        ISchemaConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateSchemaViewAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> GetKpisAsync(
        bool? includeInactive,
        ISchemaConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetKpisAsync(
            includeInactive ?? true,
            cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> CreateKpiAsync(
        CreateKpiDefinitionRequest request,
        ISchemaConfigurationService service,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.KpiViewBuilder);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var result = await service.CreateKpiAsync(request, cancellationToken);

        return result.ToHttpResult(value =>
            Results.Created($"/admin/schema-configuration/kpis/{value.Id}", value));
    }
    
    private static async Task<SchemaViewPreviewResult> ExecuteSafePreviewAsync(
        PlantProcessDbContext dbContext,
        string sqlText,
        int maxRows,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var safety = SafeSqlValidator.Validate(sqlText);

        if (!safety.IsValid)
        {
            return new SchemaViewPreviewResult(
                IsSuccess: false,
                Message: "SQL safety validation failed: " + string.Join(" | ", safety.Errors),
                RowCount: 0,
                DurationMs: 0,
                Columns: Array.Empty<SchemaViewPreviewColumnDto>(),
                Rows: Array.Empty<IReadOnlyDictionary<string, object?>>());
        }

        var safeMaxRows = Math.Clamp(maxRows, 1, 5000);

        // Hard safety:
        // - CommandTimeout protects the client call.
        // - SET LOCAL statement_timeout protects the PostgreSQL server.
        var safeTimeoutSeconds = Math.Clamp(timeoutSeconds, 1, 5);
        var statementTimeoutMs = safeTimeoutSeconds * 1000;

        var wrappedSql = $"""
            SELECT *
            FROM (
            {sqlText.Trim()}
            ) AS ppq_schema_preview
            LIMIT {safeMaxRows}
            """;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await using (var timeoutCommand = connection.CreateCommand())
                {
                    timeoutCommand.Transaction = transaction;
                    timeoutCommand.CommandText = $"SET LOCAL statement_timeout = '{statementTimeoutMs}ms';";
                    timeoutCommand.CommandTimeout = 2;

                    await timeoutCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                var columns = new List<SchemaViewPreviewColumnDto>();
                var rows = new List<IReadOnlyDictionary<string, object?>>();

                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = wrappedSql;

                    // Let PostgreSQL statement_timeout fire first.
                    command.CommandTimeout = safeTimeoutSeconds + 2;

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(new SchemaViewPreviewColumnDto(
                            ColumnName: reader.GetName(i),
                            DataType: reader.GetFieldType(i).Name,
                            Ordinal: i + 1));
                    }

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object?>();

                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);

                            if (await reader.IsDBNullAsync(i, cancellationToken))
                            {
                                row[name] = null;
                                continue;
                            }

                            var value = reader.GetValue(i);

                            row[name] = value switch
                            {
                                DateTime dateTime => dateTime.ToString("O"),
                                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
                                Guid guid => guid.ToString(),
                                _ => value
                            };
                        }

                        rows.Add(row);
                    }
                }

                await transaction.RollbackAsync(cancellationToken);

                stopwatch.Stop();

                return new SchemaViewPreviewResult(
                    IsSuccess: true,
                    Message: $"Preview succeeded. {rows.Count} row(s) returned.",
                    RowCount: rows.Count,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    Columns: columns,
                    Rows: rows);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var message = IsSqlPreviewTimeout(ex)
                ? "Query exceeded the 5 second safety timeout. Add a WHERE filter, reduce the date range, or preview fewer rows."
                : ex.Message;

            return new SchemaViewPreviewResult(
                IsSuccess: false,
                Message: message,
                RowCount: 0,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Columns: Array.Empty<SchemaViewPreviewColumnDto>(),
                Rows: Array.Empty<IReadOnlyDictionary<string, object?>>());
        }
    }
    
    private static bool IsSqlPreviewTimeout(Exception ex)
    {
        var message = ex.Message ?? string.Empty;

        return ex is TimeoutException ||
            message.Contains("statement timeout", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("canceling statement due to statement timeout", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("57014", StringComparison.OrdinalIgnoreCase);
    }
  }
