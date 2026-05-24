using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Security;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

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

        var wrappedSql = $"""
            SELECT *
            FROM (
            {sqlText.Trim()}
            ) AS ppq_schema_preview
            LIMIT {maxRows}
            """;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = wrappedSql;
            command.CommandTimeout = timeoutSeconds;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = new List<SchemaViewPreviewColumnDto>();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new SchemaViewPreviewColumnDto(
                    ColumnName: reader.GetName(i),
                    DataType: reader.GetFieldType(i).Name,
                    Ordinal: i + 1));
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>();

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

            stopwatch.Stop();

            return new SchemaViewPreviewResult(
                IsSuccess: true,
                Message: $"Preview succeeded. {rows.Count} row(s) returned.",
                RowCount: rows.Count,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Columns: columns,
                Rows: rows);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new SchemaViewPreviewResult(
                IsSuccess: false,
                Message: ex.Message,
                RowCount: 0,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Columns: Array.Empty<SchemaViewPreviewColumnDto>(),
                Rows: Array.Empty<IReadOnlyDictionary<string, object?>>());
        }
    }

    private static class SafeSqlValidator
    {
        private static readonly string[] ForbiddenTokens =
        [
            "insert",
            "update",
            "delete",
            "drop",
            "alter",
            "truncate",
            "create",
            "grant",
            "revoke",
            "execute",
            "exec",
            "merge",
            "copy",
            "vacuum",
            "analyze",
            "call",
            "do",
            "listen",
            "notify",
            "unlisten",
            "set",
            "reset",
            "prepare"
        ];

        private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
        {
            // Phase 3 raw/dump/source metadata
            "staging_records",
            "import_batches",
            "source_system_definitions",
            "connection_profiles",
            "source_dataset_definitions",
            "source_field_definitions",

            // Canonical/analytics tables allowed for controlled preview
            "material_units",
            "material_aliases",
            "genealogy_edges",
            "process_step_executions",
            "parameter_definitions",
            "parameter_observations",
            "process_events",
            "downtime_events",
            "defect_catalogs",
            "quality_events",
            "data_quality_issues",
            "risk_scores",
            "correlation_results",

            // New Phase 4 metadata
            "schema_view_definitions",
            "kpi_definitions"
        };

        public static SqlSafetyValidationResultDto Validate(string? sqlText)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var referencedTables = new List<string>();

            if (string.IsNullOrWhiteSpace(sqlText))
            {
                errors.Add("SQL text is required.");
                return new SqlSafetyValidationResultDto(false, errors, warnings, referencedTables);
            }

            var sql = StripSqlComments(sqlText).Trim();

            if (sql.Contains(';'))
                errors.Add("Semicolon is not allowed. Submit one SELECT/WITH query only.");

            if (!sql.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
                !sql.StartsWith("with", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Only SELECT or WITH queries are allowed.");
            }

            var lowered = Regex.Replace(sql.ToLowerInvariant(), @"\s+", " ");

            foreach (var token in ForbiddenTokens)
            {
                if (Regex.IsMatch(lowered, $@"(^|\W){Regex.Escape(token)}(\W|$)", RegexOptions.IgnoreCase))
                    errors.Add($"Forbidden SQL token detected: {token}");
            }

            var tableMatches = Regex.Matches(
                sql,
                @"\b(from|join)\s+([a-zA-Z_][a-zA-Z0-9_\.]*)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (Match match in tableMatches)
            {
                var rawTableName = match.Groups[2].Value;
                var tableName = rawTableName.Contains('.')
                    ? rawTableName.Split('.').Last()
                    : rawTableName;

                referencedTables.Add(tableName);

                if (!AllowedTables.Contains(tableName))
                    errors.Add($"Table '{rawTableName}' is not in the Phase 4 allowed table list.");
            }

            if (referencedTables.Count == 0)
                warnings.Add("No FROM/JOIN table reference detected. This is allowed but may not be useful.");

            if (!lowered.Contains("limit "))
            {
                warnings.Add("No LIMIT found. The preview endpoint wraps the query and applies its own LIMIT.");
            }

            return new SqlSafetyValidationResultDto(
                IsValid: errors.Count == 0,
                Errors: errors,
                Warnings: warnings,
                ReferencedTables: referencedTables.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static string StripSqlComments(string sql)
        {
            var noLineComments = Regex.Replace(sql, @"--.*?$", string.Empty, RegexOptions.Multiline);
            var noBlockComments = Regex.Replace(noLineComments, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return noBlockComments;
        }
    }
}
