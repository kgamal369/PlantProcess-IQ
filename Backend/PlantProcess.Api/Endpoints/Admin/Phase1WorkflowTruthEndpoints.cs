using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class Phase1WorkflowTruthEndpoints
{
    public static IEndpointRouteBuilder MapPhase1WorkflowTruthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/phase1")
            .WithTags("Admin - Phase 1 Workflow Truth");

        // ------------------------------------------------------------
        // PPIQ-WF-001
        // Connector Truth Matrix API
        // ------------------------------------------------------------
        group.MapGet("/connector-truth", GetConnectorTruthAsync)
            .WithName("GetPhase1ConnectorTruth")
            .Produces<ConnectorTruthMatrixResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-003
        // Connector certification status for PostgreSQL / MSSQL / MySQL
        // ------------------------------------------------------------
        group.MapGet("/connector-certification", GetConnectorCertificationAsync)
            .WithName("GetPhase1ConnectorCertification")
            .Produces<ConnectorCertificationResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-004 / PPIQ-WF-005
        // DB-driven source scheduling board and due-run trigger.
        // Real execution is delegated to your existing IDeltaImportExecutionService.
        // ------------------------------------------------------------
        group.MapGet("/source-schedule-board", GetSourceScheduleBoardAsync)
            .WithName("GetPhase1SourceScheduleBoard")
            .Produces<SourceScheduleBoardResponse>();

        group.MapPost("/run-due-source-imports", RunDueSourceImportsAsync)
            .WithName("RunPhase1DueSourceImports")
            .Produces<RunDueSourceImportsResponse>();

        group.MapPost("/source-datasets/{sourceDatasetDefinitionId:guid}/schedule-now", ScheduleSourceDatasetNowAsync)
            .WithName("SchedulePhase1SourceDatasetNow")
            .Produces<SourceScheduleRow>();

        group.MapPost("/source-datasets/{sourceDatasetDefinitionId:guid}/cursor", UpdateDatasetCursorAsync)
            .WithName("UpdatePhase1DatasetCursor")
            .Produces<SourceScheduleRow>();

        // ------------------------------------------------------------
        // PPIQ-WF-006
        // Raw/staging latest-copy viewer.
        // ------------------------------------------------------------
        group.MapGet("/staging/summary", GetStagingSummaryAsync)
            .WithName("GetPhase1StagingSummary")
            .Produces<StagingSummaryResponse>();

        group.MapGet("/staging/records", GetStagingRecordsAsync)
            .WithName("GetPhase1StagingRecords")
            .Produces<StagingRecordsResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-007
        // Schema Mapping Workbench / Cross-source join helper.
        // ------------------------------------------------------------
        group.MapGet("/schema-mapping/workbench", GetSchemaMappingWorkbenchAsync)
            .WithName("GetPhase1SchemaMappingWorkbench")
            .Produces<SchemaMappingWorkbenchResponse>();

        group.MapPost("/schema-mapping/preview-view", PreviewSchemaViewAsync)
            .WithName("PreviewPhase1SchemaView")
            .Produces<SchemaViewPreviewResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-010
        // Importing Data Job Configuration page support.
        // ------------------------------------------------------------
        group.MapGet("/import-jobs/configuration-board", GetImportJobConfigurationBoardAsync)
            .WithName("GetPhase1ImportJobConfigurationBoard")
            .Produces<ImportJobConfigurationBoardResponse>();

        group.MapPost("/import-jobs/from-mapping", CreateImportJobFromMappingAsync)
            .WithName("CreatePhase1ImportJobFromMapping")
            .Produces<ImportJobConfigurationRow>();

        return app;
    }

    private static async Task<IResult> GetConnectorTruthAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var providerRows = BuildProviderTruthRows();

        var activeProfilesByProvider = await dbContext.ConnectionProfiles
            .AsNoTracking()
            .GroupBy(x => x.ProviderType)
            .Select(g => new
            {
                ProviderType = g.Key,
                ActiveProfiles = g.Count(x => x.IsActive),
                TotalProfiles = g.Count()
            })
            .ToListAsync(cancellationToken);

        var datasetsByProvider = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                group dataset by profile.ProviderType
                into g
                select new
                {
                    ProviderType = g.Key,
                    ActiveDatasets = g.Count(x => x.IsActive),
                    TotalDatasets = g.Count()
                })
            .ToListAsync(cancellationToken);

        var enriched = providerRows
            .Select(row =>
            {
                var profileCount = activeProfilesByProvider
                    .FirstOrDefault(x => SameProvider(x.ProviderType, row.ProviderType));

                var datasetCount = datasetsByProvider
                    .FirstOrDefault(x => SameProvider(x.ProviderType, row.ProviderType));

                return row with
                {
                    ActiveConnectionProfiles = profileCount?.ActiveProfiles ?? 0,
                    TotalConnectionProfiles = profileCount?.TotalProfiles ?? 0,
                    ActiveSourceDatasets = datasetCount?.ActiveDatasets ?? 0,
                    TotalSourceDatasets = datasetCount?.TotalDatasets ?? 0
                };
            })
            .OrderBy(x => x.SortOrder)
            .ToList();

        var response = new ConnectorTruthMatrixResponse(
            GeneratedAtUtc: DateTime.UtcNow,
            OperatingRule:
            "Frontend must use this API as the single connector truth source. Do not hardcode connector availability in React.",
            Providers: enriched);

        return Results.Ok(response);
    }

    private static Task<IResult> GetConnectorCertificationAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var providers = new[]
        {
            BuildCertification("PostgreSql", "PPIQ_CERT_POSTGRES_CONNECTION", configuration),
            BuildCertification("SqlServer", "PPIQ_CERT_MSSQL_CONNECTION", configuration),
            BuildCertification("MySql", "PPIQ_CERT_MYSQL_CONNECTION", configuration),
            BuildCertification("Oracle", "PPIQ_CERT_ORACLE_CONNECTION", configuration)
        };

        var response = new ConnectorCertificationResponse(
            GeneratedAtUtc: DateTime.UtcNow,
            Message:
            "Certification is environment-driven. A provider is demo-certified only when implementation exists, smoke tests pass, and the related certification connection variable is configured.",
            Providers: providers);

        return Task.FromResult<IResult>(Results.Ok(response));
    }

    private static async Task<IResult> GetSourceScheduleBoardAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var rows = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                join source in dbContext.SourceSystemDefinitions.AsNoTracking()
                    on profile.SourceSystemDefinitionId equals source.Id
                select new SourceScheduleRow(
                    dataset.Id,
                    dataset.ConnectionProfileId,
                    profile.ConnectionProfileCode,
                    profile.ConnectionProfileName,
                    profile.ProviderType,
                    source.Id,
                    source.SourceSystemCode,
                    source.SourceSystemName,
                    dataset.DatasetCode,
                    dataset.DatasetName,
                    dataset.DatasetKind,
                    dataset.SourceSchemaName,
                    dataset.SourceObjectName,
                    dataset.PrimaryTimestampField,
                    dataset.IncrementalCursorField,
                    dataset.LastCursorValue,
                    dataset.RefreshIntervalSeconds,
                    dataset.NextRunAtUtc,
                    dataset.IsActive,
                    profile.IsActive,
                    dataset.NextRunAtUtc == null || dataset.NextRunAtUtc <= now,
                    dataset.Description,
                    dataset.CreatedAtUtc,
                    dataset.UpdatedAtUtc))
            .OrderBy(x => x.NextRunAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.ProviderType)
            .ThenBy(x => x.DatasetCode)
            .ToListAsync(cancellationToken);

        var response = new SourceScheduleBoardResponse(
            DateTime.UtcNow,
            rows.Count,
            rows.Count(x => x.IsDueNow && x.IsDatasetActive && x.IsConnectionActive),
            rows);

        return Results.Ok(response);
    }

    private static async Task<IResult> RunDueSourceImportsAsync(
        [FromBody] RunDueSourceImportsRequest request,
        IDeltaImportExecutionService deltaImportExecutionService,
        CancellationToken cancellationToken)
    {
        var maxDatasets = request.MaxDatasetsPerRun is > 0 and <= 200
            ? request.MaxDatasetsPerRun.Value
            : 25;

        var maxRows = request.MaxRowsPerDataset is > 0 and <= 50_000
            ? request.MaxRowsPerDataset.Value
            : 5_000;

        var sw = Stopwatch.StartNew();

        var summary = await deltaImportExecutionService.ExecuteAllAsync(
            maxDatasets,
            maxRows,
            cancellationToken);

        sw.Stop();

        var response = new RunDueSourceImportsResponse(
            DateTime.UtcNow,
            maxDatasets,
            maxRows,
            sw.ElapsedMilliseconds,
            summary.DatasetsProcessed,
            summary.TotalRowsImported,
            summary.DatasetsFailedCount,
            summary.DatasetResults.Select(x => new RunDueSourceDatasetResult(
                    x.DatasetId,
                    x.DatasetCode,
                    x.RowsImported,
                    x.ErrorMessage))
                .ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> ScheduleSourceDatasetNowAsync(
        Guid sourceDatasetDefinitionId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var dataset = await dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId, cancellationToken);

        if (dataset is null)
            return Results.NotFound(new { message = "Source dataset definition not found." });

        dataset.ScheduleNextRunImmediately();
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildSourceScheduleRowAsync(sourceDatasetDefinitionId, dbContext, cancellationToken);
    }

    private static async Task<IResult> UpdateDatasetCursorAsync(
        Guid sourceDatasetDefinitionId,
        [FromBody] UpdateDatasetCursorRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var dataset = await dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId, cancellationToken);

        if (dataset is null)
            return Results.NotFound(new { message = "Source dataset definition not found." });

        dataset.UpdateLastCursorValue(request.LastCursorValue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildSourceScheduleRowAsync(sourceDatasetDefinitionId, dbContext, cancellationToken);
    }

    private static async Task<IResult> GetStagingSummaryAsync(
        string? sourceObjectName,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query =
            from batch in dbContext.ImportBatches.AsNoTracking()
            join record in dbContext.StagingRecords.AsNoTracking()
                on batch.Id equals record.ImportBatchId into recordGroup
            select new
            {
                batch.Id,
                batch.SourceSystemDefinitionId,
                batch.ImportBatchCode,
                batch.ImportType,
                batch.Status,
                batch.StartedAtUtc,
                batch.CompletedAtUtc,
                batch.SourceObjectName,
                batch.FileName,
                batch.RowCount,
                batch.ErrorMessage,
                Records = recordGroup
            };

        if (!string.IsNullOrWhiteSpace(sourceObjectName))
        {
            var normalized = sourceObjectName.Trim();
            query = query.Where(x => x.SourceObjectName == normalized);
        }

        var rows = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(200)
            .Select(x => new StagingSummaryRow(
                x.Id,
                x.SourceSystemDefinitionId,
                x.ImportBatchCode,
                x.ImportType,
                x.Status,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.SourceObjectName,
                x.FileName,
                x.RowCount,
                x.ErrorMessage,
                x.Records.Count(),
                x.Records.Count(r => r.ProcessingStatus == "Pending"),
                x.Records.Count(r => r.ProcessingStatus == "Mapped"),
                x.Records.Count(r => r.ProcessingStatus == "Failed"),
                x.Records.Count(r => r.ProcessingStatus == "Skipped")))
            .ToListAsync(cancellationToken);

        var response = new StagingSummaryResponse(
            DateTime.UtcNow,
            "This is the raw latest-copy/staging layer before canonical mapping. It proves PlantProcess IQ copies source-shaped data first, then maps it into the generic model.",
            rows);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetStagingRecordsAsync(
        Guid? importBatchId,
        string? sourceObjectName,
        string? processingStatus,
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var safeTake = take is > 0 and <= 1000 ? take.Value : 200;

        var query = dbContext.StagingRecords
            .AsNoTracking();

        if (importBatchId.HasValue)
            query = query.Where(x => x.ImportBatchId == importBatchId.Value);

        if (!string.IsNullOrWhiteSpace(sourceObjectName))
        {
            var normalized = sourceObjectName.Trim();
            query = query.Where(x => x.SourceObjectName == normalized);
        }

        if (!string.IsNullOrWhiteSpace(processingStatus))
        {
            var normalized = processingStatus.Trim();
            query = query.Where(x => x.ProcessingStatus == normalized);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.RowNumber)
            .Take(safeTake)
            .Select(x => new StagingRecordRow(
                x.Id,
                x.ImportBatchId,
                x.SourceObjectName,
                x.RowNumber,
                x.RawJson,
                x.IsProcessed,
                x.ProcessedAtUtc,
                x.ProcessingStatus,
                x.ProcessingError,
                x.CanonicalEntityId,
                x.CanonicalEntityName,
                x.SourceSystem,
                x.SourceRecordId,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new StagingRecordsResponse(DateTime.UtcNow, rows.Count, rows));
    }

    private static async Task<IResult> GetSchemaMappingWorkbenchAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var datasets = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                select new WorkbenchDatasetRow(
                    dataset.Id,
                    dataset.DatasetCode,
                    dataset.DatasetName,
                    dataset.DatasetKind,
                    profile.ProviderType,
                    dataset.SourceSchemaName,
                    dataset.SourceObjectName,
                    dataset.IsActive))
            .OrderBy(x => x.ProviderType)
            .ThenBy(x => x.DatasetCode)
            .ToListAsync(cancellationToken);

        var fields = await dbContext.SourceFieldDefinitions
            .AsNoTracking()
            .OrderBy(x => x.SourceDatasetDefinitionId)
            .ThenBy(x => x.Ordinal)
            .Select(x => new WorkbenchSourceFieldRow(
                x.Id,
                x.SourceDatasetDefinitionId,
                x.FieldName,
                x.DisplayName,
                x.SourceDataType,
                x.Ordinal,
                x.IsNullable,
                x.SampleValue,
                x.IsPrimaryKeyCandidate,
                x.IsTimestampCandidate,
                x.IsActive))
            .ToListAsync(cancellationToken);

        var mappings = await dbContext.MappingDefinitions
            .AsNoTracking()
            .OrderBy(x => x.MappingCode)
            .Select(x => new WorkbenchMappingRow(
                x.Id,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.MappingJson,
                x.MappingVersion,
                x.IsActive,
                x.Description))
            .ToListAsync(cancellationToken);

        var schemaViews = await dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .OrderBy(x => x.SchemaViewCode)
            .Select(x => new WorkbenchSchemaViewRow(
                x.Id,
                x.SchemaViewCode,
                x.SchemaViewName,
                x.ViewKind,
                x.PrimarySourceDatasetDefinitionId,
                x.SourceDatasetIdsJson,
                x.IsApproved,
                x.IsActive,
                x.LastValidationStatus,
                x.LastValidationMessage))
            .ToListAsync(cancellationToken);

        var canonicalTargets = BuildCanonicalTargets();

        return Results.Ok(new SchemaMappingWorkbenchResponse(
            DateTime.UtcNow,
            "Schema mapping is the centerpiece of genericity: source fields stay source-shaped, then approved mapping turns them into canonical PlantProcess IQ entities.",
            datasets,
            fields,
            canonicalTargets,
            mappings,
            schemaViews));
    }

    private static async Task<IResult> PreviewSchemaViewAsync(
        [FromBody] PreviewSchemaViewRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSafePreviewSql(request.SqlText);
        if (validation is not null)
        {
            return Results.BadRequest(new SchemaViewPreviewResponse(
                false,
                validation,
                0,
                0,
                new List<SchemaViewPreviewColumn>(),
                new List<Dictionary<string, object?>>()));
        }

        var maxRows = request.MaxRows is > 0 and <= 500 ? request.MaxRows.Value : 100;
        var sql = WrapSelectWithLimit(request.SqlText, maxRows);

        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<SchemaViewPreviewColumn>();

        var sw = Stopwatch.StartNew();

        try
        {
            var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = request.TimeoutSeconds is > 0 and <= 30
                    ? request.TimeoutSeconds.Value
                    : 10
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new SchemaViewPreviewColumn(
                    reader.GetName(i),
                    reader.GetDataTypeName(i),
                    i + 1));
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = await reader.IsDBNullAsync(i, cancellationToken)
                        ? null
                        : reader.GetValue(i);

                    row[reader.GetName(i)] = value;
                }

                rows.Add(row);
            }

            sw.Stop();

            return Results.Ok(new SchemaViewPreviewResponse(
                true,
                $"Preview returned {rows.Count} row(s).",
                rows.Count,
                sw.ElapsedMilliseconds,
                columns,
                rows));
        }
        catch (Exception ex)
        {
            sw.Stop();

            return Results.BadRequest(new SchemaViewPreviewResponse(
                false,
                $"Preview failed: {ex.Message}",
                0,
                sw.ElapsedMilliseconds,
                columns,
                rows));
        }
    }

    private static async Task<IResult> GetImportJobConfigurationBoardAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.MappingDefinitions
            .AsNoTracking()
            .OrderBy(x => x.MappingCode)
            .Select(x => new
            {
                x.Id,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.IsActive,
                x.Description
            })
            .ToListAsync(cancellationToken);

        var jobs = await dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => x.TargetType == "MappingDefinition")
            .OrderBy(x => x.JobCode)
            .Select(x => new ImportJobConfigurationRow(
                x.Id,
                x.JobCode,
                x.JobName,
                x.JobType.ToString(),
                x.TargetId,
                x.TargetType,
                x.ScheduleExpression,
                x.IsEnabled,
                x.LastRunStatus.ToString(),
                x.LastRunStartedAtUtc,
                x.LastRunCompletedAtUtc,
                x.LastRunDurationMs,
                x.LastFailureReason,
                x.NextRunAtUtc,
                x.Description,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var rows = mappings.Select(mapping =>
        {
            var existing = jobs.FirstOrDefault(x => x.TargetId == mapping.Id);

            return new MappingImportJobCandidateRow(
                mapping.Id,
                mapping.MappingCode,
                mapping.MappingName,
                mapping.SourceObjectName,
                mapping.TargetEntityName,
                mapping.IsActive,
                existing?.JobDefinitionId,
                existing?.JobCode,
                existing?.IsEnabled ?? false,
                existing?.ScheduleExpression,
                existing?.LastRunStatus,
                existing?.NextRunAtUtc);
        }).ToList();

        return Results.Ok(new ImportJobConfigurationBoardResponse(
            DateTime.UtcNow,
            "This board supports the Admin > DB Configuration > Importing Data tab. It turns approved mappings into scheduled canonical import jobs.",
            rows,
            jobs));
    }

    private static async Task<IResult> CreateImportJobFromMappingAsync(
        [FromBody] CreateImportJobFromMappingRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mapping = await dbContext.MappingDefinitions
            .FirstOrDefaultAsync(x => x.Id == request.MappingDefinitionId, cancellationToken);

        if (mapping is null)
            return Results.NotFound(new { message = "Mapping definition not found." });

        if (!mapping.IsActive)
            return Results.BadRequest(new { message = "Mapping definition is not active." });

        var jobCode = string.IsNullOrWhiteSpace(request.JobCode)
            ? $"CANONICAL_IMPORT_{mapping.MappingCode}"
            : request.JobCode.Trim();

        var normalizedJobCode = NormalizeCode(jobCode);

        var existing = await dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => x.JobCode == normalizedJobCode, cancellationToken);

        var scheduleExpression = string.IsNullOrWhiteSpace(request.ScheduleExpression)
            ? "Every 15 minutes"
            : request.ScheduleExpression.Trim();

        if (existing is null)
        {
            existing = new JobDefinition(
                jobCode: normalizedJobCode,
                jobName: string.IsNullOrWhiteSpace(request.JobName)
                    ? $"Canonical import - {mapping.MappingName}"
                    : request.JobName.Trim(),
                jobType: JobDefinitionType.CanonicalRefresh,
                scheduleExpression: scheduleExpression,
                isSynthetic: request.IsSynthetic,
                targetId: mapping.Id,
                targetType: "MappingDefinition",
                isEnabled: request.IsEnabled,
                description: request.Description ??
                             $"Imports {mapping.SourceObjectName} into canonical {mapping.TargetEntityName}.",
                sourceSystem: "PlantProcessIQ.Phase1",
                sourceRecordId: mapping.MappingCode);

            dbContext.JobDefinitions.Add(existing);
        }
        else
        {
            existing.UpdateDefinition(
                jobName: string.IsNullOrWhiteSpace(request.JobName)
                    ? existing.JobName
                    : request.JobName.Trim(),
                jobType: JobDefinitionType.CanonicalRefresh,
                scheduleExpression: scheduleExpression,
                targetId: mapping.Id,
                targetType: "MappingDefinition",
                isEnabled: request.IsEnabled,
                description: request.Description ?? existing.Description);

            if (request.IsEnabled)
                existing.Enable(existing.NextRunAtUtc);
            else
                existing.Disable();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ImportJobConfigurationRow(
            existing.Id,
            existing.JobCode,
            existing.JobName,
            existing.JobType.ToString(),
            existing.TargetId,
            existing.TargetType,
            existing.ScheduleExpression,
            existing.IsEnabled,
            existing.LastRunStatus.ToString(),
            existing.LastRunStartedAtUtc,
            existing.LastRunCompletedAtUtc,
            existing.LastRunDurationMs,
            existing.LastFailureReason,
            existing.NextRunAtUtc,
            existing.Description,
            existing.CreatedAtUtc,
            existing.UpdatedAtUtc));
    }

    private static async Task<IResult> BuildSourceScheduleRowAsync(
        Guid sourceDatasetDefinitionId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var row = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                join source in dbContext.SourceSystemDefinitions.AsNoTracking()
                    on profile.SourceSystemDefinitionId equals source.Id
                where dataset.Id == sourceDatasetDefinitionId
                select new SourceScheduleRow(
                    dataset.Id,
                    dataset.ConnectionProfileId,
                    profile.ConnectionProfileCode,
                    profile.ConnectionProfileName,
                    profile.ProviderType,
                    source.Id,
                    source.SourceSystemCode,
                    source.SourceSystemName,
                    dataset.DatasetCode,
                    dataset.DatasetName,
                    dataset.DatasetKind,
                    dataset.SourceSchemaName,
                    dataset.SourceObjectName,
                    dataset.PrimaryTimestampField,
                    dataset.IncrementalCursorField,
                    dataset.LastCursorValue,
                    dataset.RefreshIntervalSeconds,
                    dataset.NextRunAtUtc,
                    dataset.IsActive,
                    profile.IsActive,
                    dataset.NextRunAtUtc == null || dataset.NextRunAtUtc <= now,
                    dataset.Description,
                    dataset.CreatedAtUtc,
                    dataset.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? Results.NotFound(new { message = "Source dataset definition not found." })
            : Results.Ok(row);
    }

    private static ConnectorProviderTruthRow[] BuildProviderTruthRows()
    {
        return new[]
        {
            new ConnectorProviderTruthRow(
                SortOrder: 10,
                ProviderType: "Csv",
                DisplayName: "CSV",
                Description: "Flat-file export connector. Good for first demo and quick plant data diagnostic.",
                IsImplemented: true,
                IsDemoCertified: true,
                IsAvailableNow: true,
                RequiresSecretReference: false,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Available now",
                Limitation: "Best for snapshots and controlled exports; not real-time streaming.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 20,
                ProviderType: "Excel",
                DisplayName: "Excel",
                Description: "Excel workbook/sheet connector for manual QA, lab, yard and business files.",
                IsImplemented: true,
                IsDemoCertified: true,
                IsAvailableNow: true,
                RequiresSecretReference: false,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false,
                StatusLabel: "Available now",
                Limitation: "Snapshot import only; no continuous database cursor.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 30,
                ProviderType: "PostgreSql",
                DisplayName: "PostgreSQL",
                Description: "Read-only SQL database connector for process/MES-like source databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Show as available only after PPIQ-WF-003 smoke certification passes in your demo environment.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 40,
                ProviderType: "SqlServer",
                DisplayName: "Microsoft SQL Server",
                Description: "Read-only connector for MES, QA, ERP or Level-3 databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Requires certified local/demo SQL Server connection before frontend marks it selectable.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 50,
                ProviderType: "MySql",
                DisplayName: "MySQL",
                Description: "Read-only connector for inspection, downtime, small MES or device-side databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Requires certified MySQL smoke test before customer demo.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 60,
                ProviderType: "Oracle",
                DisplayName: "Oracle",
                Description: "Planned read-only connector for caster/HSM/legacy manufacturing source databases.",
                IsImplemented: false,
                IsDemoCertified: false,
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsConnectionTest: false,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: false,
                StatusLabel: "Planned",
                Limitation: "Use Oracle-shaped demo source tables until a real Oracle provider is implemented and certified.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 70,
                ProviderType: "RestApi",
                DisplayName: "REST API",
                Description: "Planned connector for API-based systems.",
                IsImplemented: false,
                IsDemoCertified: false,
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsConnectionTest: false,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: false,
                StatusLabel: "Planned",
                Limitation: "Not demo-certified in Phase 1.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0)
        };
    }

    private static ConnectorCertificationRow BuildCertification(
        string providerType,
        string environmentVariableName,
        IConfiguration configuration)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName)
            ?? configuration[environmentVariableName];

        var hasConnection = !string.IsNullOrWhiteSpace(value);

        return new ConnectorCertificationRow(
            ProviderType: providerType,
            EnvironmentVariableName: environmentVariableName,
            HasCertificationConnectionString: hasConnection,
            CertificationStatus: hasConnection
                ? "Ready to run smoke certification"
                : "Missing certification connection string",
            IsDemoCertified: false,
            Message: hasConnection
                ? "Run the provider smoke test and then flip IsDemoCertified in connector truth only after it passes."
                : $"Set {environmentVariableName} in the demo machine or CI secret store.");
    }

    private static IReadOnlyList<CanonicalTargetRow> BuildCanonicalTargets()
    {
        return new List<CanonicalTargetRow>
        {
            new("MaterialUnit", "MaterialCode", "string", true, "Heat, slab, coil, batch, lot, roll, component, etc."),
            new("MaterialUnit", "MaterialUnitType", "string", true, "Generic material type."),
            new("MaterialUnit", "ProductFamily", "string", false, "Product family / business family."),
            new("MaterialUnit", "GradeOrRecipe", "string", false, "Grade, recipe, steel grade, pharma recipe, tire recipe, etc."),
            new("MaterialAlias", "ExternalId", "string", true, "Source-side ID or alternative piece/batch ID."),
            new("GenealogyEdge", "ParentMaterialCode", "string", true, "Parent material for genealogy."),
            new("GenealogyEdge", "ChildMaterialCode", "string", true, "Child material for genealogy."),
            new("ProcessStepExecution", "OperationCode", "string", true, "EAF, LF, Caster, HSM, PKL, Mix, Pack, Cure, etc."),
            new("ProcessStepExecution", "EquipmentCode", "string", true, "Generic equipment or line code."),
            new("ProcessStepExecution", "StartedAtUtc", "datetime", true, "Process step start time."),
            new("ProcessStepExecution", "EndedAtUtc", "datetime", false, "Process step end time."),
            new("ParameterObservation", "ParameterCode", "string", true, "Measured process parameter."),
            new("ParameterObservation", "NumericValue", "decimal", false, "Numeric measurement value."),
            new("ParameterObservation", "TextValue", "string", false, "Text measurement value."),
            new("ParameterObservation", "BooleanValue", "bool", false, "Boolean measurement value."),
            new("ParameterObservation", "ObservedAtUtc", "datetime", true, "Observation timestamp."),
            new("QualityEvent", "DefectCode", "string", false, "Source defect code mapped to DefectCatalog."),
            new("QualityEvent", "EventType", "string", true, "Defect, QA decision, inspection finding, lab issue, etc."),
            new("QualityEvent", "Decision", "string", false, "Accepted, downgraded, rejected, hold, rework."),
            new("QualityEvent", "EventAtUtc", "datetime", true, "Quality event timestamp."),
            new("DowntimeEvent", "ReasonCode", "string", true, "Downtime reason code."),
            new("DowntimeEvent", "StartedAtUtc", "datetime", true, "Downtime start."),
            new("DowntimeEvent", "EndedAtUtc", "datetime", false, "Downtime end.")
        };
    }

    private static string? ValidateSafePreviewSql(string? sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            return "SQL text is required.";

        var sql = sqlText.Trim();

        var lower = $" {sql.ToLowerInvariant()} ";

        var forbidden = new[]
        {
            " insert ",
            " update ",
            " delete ",
            " drop ",
            " alter ",
            " create ",
            " truncate ",
            " grant ",
            " revoke ",
            " execute ",
            " exec ",
            " call ",
            " copy ",
            " vacuum ",
            " analyze ",
            " set ",
            " reset ",
            " do ",
            " merge ",
            " pg_read_file",
            " pg_ls_dir",
            " dblink",
            " xp_",
            ";--"
        };

        if (forbidden.Any(lower.Contains))
            return "Only safe SELECT/WITH preview queries are allowed. Mutating, administrative, file-system and extension calls are blocked.";

        if (!lower.TrimStart().StartsWith("select ") && !lower.TrimStart().StartsWith("with "))
            return "Preview SQL must start with SELECT or WITH.";

        if (!lower.Contains("staging_records") &&
            !lower.Contains("import_batches") &&
            !lower.Contains("source_dataset_definitions") &&
            !lower.Contains("source_field_definitions") &&
            !lower.Contains("mapping_definitions") &&
            !lower.Contains("schema_view_definitions"))
        {
            return "Phase 1 schema-preview SQL must operate on staging/schema/mapping tables only.";
        }

        return null;
    }

    private static string WrapSelectWithLimit(string sqlText, int maxRows)
    {
        var trimmed = sqlText.Trim().TrimEnd(';');
        return $"SELECT * FROM ({trimmed}) AS phase1_preview LIMIT {maxRows}";
    }

    private static bool SameProvider(string left, string right)
    {
        return NormalizeProvider(left) == NormalizeProvider(right);
    }

    private static string NormalizeProvider(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "csv" => "csv",
            "excel" => "excel",
            "xlsx" => "excel",

            "postgres" => "postgresql",
            "postgresql" => "postgresql",
            "pgsql" => "postgresql",

            "sqlserver" => "sqlserver",
            "sql_server" => "sqlserver",
            "mssql" => "sqlserver",
            "microsoftsqlserver" => "sqlserver",

            "mysql" => "mysql",
            "mariadb" => "mysql",

            "oracle" => "oracle",

            "restapi" => "restapi",
            "rest" => "restapi",
            "api" => "restapi",

            "opcua" => "opcua",
            "opc_ua" => "opcua",
            "historian" => "historian",

            _ => normalized
        };
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_").Replace("-", "_");
    }
}

// ============================================================
// DTOs
// ============================================================

public sealed record ConnectorTruthMatrixResponse(
    DateTime GeneratedAtUtc,
    string OperatingRule,
    IReadOnlyList<ConnectorProviderTruthRow> Providers);

public sealed record ConnectorProviderTruthRow(
    int SortOrder,
    string ProviderType,
    string DisplayName,
    string Description,
    bool IsImplemented,
    bool IsDemoCertified,
    bool IsAvailableNow,
    bool RequiresSecretReference,
    bool SupportsConnectionTest,
    bool SupportsSchemaDiscovery,
    bool SupportsSnapshotImport,
    bool SupportsIncrementalImport,
    string StatusLabel,
    string Limitation,
    int ActiveConnectionProfiles,
    int TotalConnectionProfiles,
    int ActiveSourceDatasets,
    int TotalSourceDatasets);

public sealed record ConnectorCertificationResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<ConnectorCertificationRow> Providers);

public sealed record ConnectorCertificationRow(
    string ProviderType,
    string EnvironmentVariableName,
    bool HasCertificationConnectionString,
    string CertificationStatus,
    bool IsDemoCertified,
    string Message);

public sealed record SourceScheduleBoardResponse(
    DateTime GeneratedAtUtc,
    int TotalDatasets,
    int DueNowDatasets,
    IReadOnlyList<SourceScheduleRow> Rows);

public sealed record SourceScheduleRow(
    Guid SourceDatasetDefinitionId,
    Guid ConnectionProfileId,
    string ConnectionProfileCode,
    string ConnectionProfileName,
    string ProviderType,
    Guid SourceSystemDefinitionId,
    string SourceSystemCode,
    string SourceSystemName,
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string? SourceSchemaName,
    string SourceObjectName,
    string? PrimaryTimestampField,
    string? IncrementalCursorField,
    string? LastCursorValue,
    int RefreshIntervalSeconds,
    DateTime? NextRunAtUtc,
    bool IsDatasetActive,
    bool IsConnectionActive,
    bool IsDueNow,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record RunDueSourceImportsRequest(
    int? MaxDatasetsPerRun,
    int? MaxRowsPerDataset);

public sealed record RunDueSourceImportsResponse(
    DateTime CompletedAtUtc,
    int MaxDatasetsPerRun,
    int MaxRowsPerDataset,
    long DurationMs,
    int DatasetsProcessed,
    int TotalRowsImported,
    int DatasetsFailedCount,
    IReadOnlyList<RunDueSourceDatasetResult> DatasetResults);

public sealed record RunDueSourceDatasetResult(
    string DatasetId,
    string DatasetCode,
    int RowsImported,
    string? ErrorMessage);

public sealed record UpdateDatasetCursorRequest(string? LastCursorValue);

public sealed record StagingSummaryResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<StagingSummaryRow> Rows);

public sealed record StagingSummaryRow(
    Guid ImportBatchId,
    Guid SourceSystemDefinitionId,
    string ImportBatchCode,
    string ImportType,
    string Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? SourceObjectName,
    string? FileName,
    int? RowCount,
    string? ErrorMessage,
    int StagingRecordCount,
    int PendingCount,
    int MappedCount,
    int FailedCount,
    int SkippedCount);

public sealed record StagingRecordsResponse(
    DateTime GeneratedAtUtc,
    int Count,
    IReadOnlyList<StagingRecordRow> Rows);

public sealed record StagingRecordRow(
    Guid Id,
    Guid ImportBatchId,
    string SourceObjectName,
    int RowNumber,
    string RawJson,
    bool IsProcessed,
    DateTime? ProcessedAtUtc,
    string ProcessingStatus,
    string? ProcessingError,
    Guid? CanonicalEntityId,
    string? CanonicalEntityName,
    string? SourceSystem,
    string? SourceRecordId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SchemaMappingWorkbenchResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<WorkbenchDatasetRow> Datasets,
    IReadOnlyList<WorkbenchSourceFieldRow> SourceFields,
    IReadOnlyList<CanonicalTargetRow> CanonicalTargets,
    IReadOnlyList<WorkbenchMappingRow> Mappings,
    IReadOnlyList<WorkbenchSchemaViewRow> SchemaViews);

public sealed record WorkbenchDatasetRow(
    Guid Id,
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string ProviderType,
    string? SourceSchemaName,
    string SourceObjectName,
    bool IsActive);

public sealed record WorkbenchSourceFieldRow(
    Guid Id,
    Guid SourceDatasetDefinitionId,
    string FieldName,
    string DisplayName,
    string SourceDataType,
    int Ordinal,
    bool IsNullable,
    string? SampleValue,
    bool IsPrimaryKeyCandidate,
    bool IsTimestampCandidate,
    bool IsActive);

public sealed record CanonicalTargetRow(
    string EntityName,
    string FieldName,
    string DataType,
    bool IsRequired,
    string Description);

public sealed record WorkbenchMappingRow(
    Guid Id,
    string MappingCode,
    string MappingName,
    string SourceObjectName,
    string TargetEntityName,
    string MappingJson,
    string MappingVersion,
    bool IsActive,
    string? Description);

public sealed record WorkbenchSchemaViewRow(
    Guid Id,
    string SchemaViewCode,
    string SchemaViewName,
    string ViewKind,
    Guid? PrimarySourceDatasetDefinitionId,
    string SourceDatasetIdsJson,
    bool IsApproved,
    bool IsActive,
    string? LastValidationStatus,
    string? LastValidationMessage);

public sealed record PreviewSchemaViewRequest(
    string SqlText,
    int? MaxRows,
    int? TimeoutSeconds);

public sealed record SchemaViewPreviewResponse(
    bool IsSuccess,
    string Message,
    int RowCount,
    long DurationMs,
    IReadOnlyList<SchemaViewPreviewColumn> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed record SchemaViewPreviewColumn(
    string ColumnName,
    string DataType,
    int Ordinal);

public sealed record ImportJobConfigurationBoardResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<MappingImportJobCandidateRow> MappingCandidates,
    IReadOnlyList<ImportJobConfigurationRow> ExistingImportJobs);

public sealed record MappingImportJobCandidateRow(
    Guid MappingDefinitionId,
    string MappingCode,
    string MappingName,
    string SourceObjectName,
    string TargetEntityName,
    bool IsMappingActive,
    Guid? ExistingJobDefinitionId,
    string? ExistingJobCode,
    bool HasEnabledJob,
    string? ExistingScheduleExpression,
    string? LastRunStatus,
    DateTime? NextRunAtUtc);

public sealed record CreateImportJobFromMappingRequest(
    Guid MappingDefinitionId,
    string? JobCode,
    string? JobName,
    string? ScheduleExpression,
    bool IsEnabled,
    string? Description,
    bool IsSynthetic);

public sealed record ImportJobConfigurationRow(
    Guid JobDefinitionId,
    string JobCode,
    string JobName,
    string JobType,
    Guid? TargetId,
    string? TargetType,
    string ScheduleExpression,
    bool IsEnabled,
    string LastRunStatus,
    DateTime? LastRunStartedAtUtc,
    DateTime? LastRunCompletedAtUtc,
    long? LastRunDurationMs,
    string? LastFailureReason,
    DateTime? NextRunAtUtc,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);