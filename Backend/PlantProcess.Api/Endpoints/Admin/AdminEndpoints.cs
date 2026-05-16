using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

/// <summary>
/// Phase 2 — Admin Area Foundation.
/// 
/// Purpose:
/// This endpoint group supports the Administrator shell required before
/// customer discovery. It does not yet connect to live customer databases.
/// Instead, it exposes the current source-system, staging, mapping, and
/// import-batch state so the frontend can show the two-stage model:
///
///   Stage 1: DB Link / Raw Source Snapshot
///            Customer source data is copied into PlantProcess IQ staging/dump records.
/// 
///   Stage 2: Canonical Refresh
///            Staging/dump rows are mapped into the generic canonical model:
///            MaterialUnit, ProcessStepExecution, ParameterObservation,
///            QualityEvent, DowntimeEvent, RiskScore, etc.
/// 
/// This keeps Phase 2 clean and prepares Phase 3 connector work.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .WithTags("Admin");

        group.MapGet("/overview", GetOverviewAsync)
            .WithSummary("Get Admin overview")
            .WithDescription("Returns admin-level status for source systems, staging, mappings, dashboards, jobs, and canonical data.");

        group.MapGet("/two-stage-import-model", GetTwoStageImportModelAsync)
            .WithSummary("Get two-stage import model")
            .WithDescription("Explains the raw snapshot / dump stage and canonical refresh stage with current live counts.");

        group.MapGet("/db-configuration/summary", GetDbConfigurationSummaryAsync)
            .WithSummary("Get DB Configuration summary")
            .WithDescription("Returns current source-system and import-batch state used by the Admin DB Configuration shell.");

        group.MapGet("/schema-configuration/summary", GetSchemaConfigurationSummaryAsync)
            .WithSummary("Get Schema Configuration summary")
            .WithDescription("Returns current mapping definitions and staging source-object coverage.");

       group.MapGet("/jobs-monitor", GetJobsMonitorAsync)
            .WithSummary("Get Jobs Monitor")
            .WithDescription("Returns DB-backed job status from JobDefinition records.");
            
        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sourceSystems = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeSourceSystems = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var importBatches = await dbContext.ImportBatches
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var runningImportBatches = await dbContext.ImportBatches
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == "Running", cancellationToken);

        var failedImportBatches = await dbContext.ImportBatches
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == "Failed", cancellationToken);

        var stagingRecords = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var pendingStagingRecords = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.ProcessingStatus == "Pending", cancellationToken);

        var failedStagingRecords = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.ProcessingStatus == "Failed", cancellationToken);

        var mappingDefinitions = await dbContext.MappingDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeMappings = await dbContext.MappingDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var materialUnits = await dbContext.MaterialUnits
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var qualityEvents = await dbContext.QualityEvents
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var parameterObservations = await dbContext.ParameterObservations
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var dataQualityIssues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var dashboardDefinitions = await dbContext.DashboardDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var latestImportBatch = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new AdminLatestImportBatchDto(
                x.Id,
                x.ImportBatchCode,
                x.ImportType,
                x.Status,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.RowCount,
                x.ErrorMessage))
            .FirstOrDefaultAsync(cancellationToken);

        var status = failedImportBatches > 0 || failedStagingRecords > 0
            ? "AttentionRequired"
            : runningImportBatches > 0
                ? "Running"
                : "Ready";

        var response = new AdminOverviewDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Status: status,
            Cards:
            [
                new AdminMetricCardDto("Source Systems", sourceSystems, $"{activeSourceSystems} active", "DB Link foundation"),
                new AdminMetricCardDto("Import Batches", importBatches, $"{runningImportBatches} running / {failedImportBatches} failed", "Raw snapshot stage"),
                new AdminMetricCardDto("Staging Records", stagingRecords, $"{pendingStagingRecords} pending / {failedStagingRecords} failed", "Dump / raw rows"),
                new AdminMetricCardDto("Mappings", mappingDefinitions, $"{activeMappings} active", "Canonical refresh"),
                new AdminMetricCardDto("Canonical Materials", materialUnits, $"{parameterObservations} parameters", "Intelligence model"),
                new AdminMetricCardDto("Quality Events", qualityEvents, $"{dataQualityIssues} DQ issues", "Quality readiness"),
                new AdminMetricCardDto("Dashboards", dashboardDefinitions, "Saved definitions", "HMI / UI layer")
            ],
            LatestImportBatch: latestImportBatch);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetTwoStageImportModelAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var importBatchCount = await dbContext.ImportBatches
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var stagingCount = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var mappedStagingCount = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.ProcessingStatus == "Mapped", cancellationToken);

        var pendingStagingCount = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.ProcessingStatus == "Pending", cancellationToken);

        var failedStagingCount = await dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.ProcessingStatus == "Failed", cancellationToken);

        var canonicalRecordCount =
            await dbContext.MaterialUnits.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken) +
            await dbContext.ProcessStepExecutions.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken) +
            await dbContext.ParameterObservations.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken) +
            await dbContext.QualityEvents.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken) +
            await dbContext.DowntimeEvents.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken) +
            await dbContext.ProcessEvents.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken);

        var response = new TwoStageImportModelDto(
            GeneratedAtUtc: DateTime.UtcNow,
            ModelName: "PlantProcess IQ Two-Stage Import Model",
            Summary:
                "PlantProcess IQ separates customer-source copying from canonical refresh. " +
                "Stage 1 copies raw source data into staging/dump records in the original source shape. " +
                "Stage 2 maps those staging records into the generic canonical manufacturing model.",
            Stages:
            [
                new TwoStageImportStageDto(
                    StageNo: 1,
                    StageCode: "RAW_SOURCE_SNAPSHOT",
                    StageName: "DB Link Import / Raw Source Snapshot",
                    Purpose: "Copy customer source rows into PlantProcess IQ without changing their original structure.",
                    CurrentImplementation: "Represented today by SourceSystemDefinition, ImportBatch and StagingRecord.",
                    RefreshOwner: "DB Link / connector jobs",
                    CurrentCount: stagingCount,
                    Status: importBatchCount > 0 ? "Available" : "WaitingForSourceData"),
                new TwoStageImportStageDto(
                    StageNo: 2,
                    StageCode: "CANONICAL_REFRESH",
                    StageName: "Canonical Refresh",
                    Purpose: "Map raw/staging rows into MaterialUnit, process, quality, genealogy, risk and dashboard-ready structures.",
                    CurrentImplementation: "Represented today by MappingDefinition and MappingExecutionService.",
                    RefreshOwner: "Canonical import / mapping jobs",
                    CurrentCount: canonicalRecordCount,
                    Status: mappedStagingCount > 0 || canonicalRecordCount > 0 ? "Available" : "WaitingForMapping")
            ],
            Metrics:
            [
                new AdminMetricCardDto("Import Batches", importBatchCount, "Stage 1 containers", "Raw source snapshot"),
                new AdminMetricCardDto("Staging Records", stagingCount, $"{pendingStagingCount} pending", "Raw/dump layer"),
                new AdminMetricCardDto("Mapped Records", mappedStagingCount, $"{failedStagingCount} failed", "Canonical readiness"),
                new AdminMetricCardDto("Canonical Records", canonicalRecordCount, "Across core entities", "HMI refresh model")
            ]);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetDbConfigurationSummaryAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sourceSystems = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SourceSystemCode)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemCode,
                x.SourceSystemName,
                x.SourceSystemType,
                x.Description,
                x.IsReadOnlySource,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var importCounts = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.SourceSystemDefinitionId)
            .Select(x => new
            {
                SourceSystemDefinitionId = x.Key,
                Count = x.Count(),
                Running = x.Count(y => y.Status == "Running"),
                Failed = x.Count(y => y.Status == "Failed"),
                Completed = x.Count(y => y.Status == "Completed"),
                LastImportAtUtc = x.Max(y => (DateTime?)y.StartedAtUtc)
            })
            .ToDictionaryAsync(x => x.SourceSystemDefinitionId, cancellationToken);

        var sourceRows = sourceSystems
            .Select(x =>
            {
                importCounts.TryGetValue(x.Id, out var counts);

                return new DbConfigurationSourceSystemDto(
                    x.Id,
                    x.SourceSystemCode,
                    x.SourceSystemName,
                    x.SourceSystemType,
                    x.Description,
                    x.IsReadOnlySource,
                    x.IsActive,
                    counts?.Count ?? 0,
                    counts?.Completed ?? 0,
                    counts?.Running ?? 0,
                    counts?.Failed ?? 0,
                    counts?.LastImportAtUtc);
            })
            .ToList();

        var response = new DbConfigurationSummaryDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Message:
                "Phase 2 Admin shell is visible now. Real ConnectionProfile, SourceDatasetDefinition, " +
                "SourceFieldDefinition and connector execution are planned for Phase 3.",
            PlannedProviderTypes:
            [
                new PlannedProviderDto("Csv", "File upload / exported CSV", "Phase 3 first connector", true),
                new PlannedProviderDto("Excel", "Excel workbook / sheet import", "Phase 3 after CSV", true),
                new PlannedProviderDto("PostgreSql", "Read-only PostgreSQL source", "Phase 3 connector foundation", false),
                new PlannedProviderDto("SqlServer", "Read-only Microsoft SQL Server source", "Phase 3 connector foundation", false),
                new PlannedProviderDto("Oracle", "Read-only Oracle source", "Phase 3 connector foundation", false),
                new PlannedProviderDto("RestApi", "REST/API source snapshot", "Later connector family", false)
            ],
            SourceSystems: sourceRows);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetSchemaConfigurationSummaryAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.MappingDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SourceObjectName)
            .ThenBy(x => x.MappingCode)
            .Select(x => new SchemaMappingSummaryDto(
                x.Id,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.MappingVersion,
                x.IsActive,
                x.Description))
            .ToListAsync(cancellationToken);

        var sourceObjects = await dbContext.StagingRecords
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.SourceObjectName)
            .Select(x => new SourceObjectCoverageDto(
                x.Key,
                x.Count(),
                x.Count(y => y.ProcessingStatus == "Pending"),
                x.Count(y => y.ProcessingStatus == "Mapped"),
                x.Count(y => y.ProcessingStatus == "Failed"),
                x.Count(y => y.ProcessingStatus == "Skipped")))
            .OrderByDescending(x => x.TotalRows)
            .ToListAsync(cancellationToken);

        var targetCoverage = mappings
            .GroupBy(x => x.TargetEntityName)
            .Select(x => new AdminStatusCountDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var response = new SchemaConfigurationSummaryDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Message:
                "Schema Configuration currently reads existing mapping definitions. " +
                "Phase 4 will add controlled SQL views, JOIN definitions and KPI view builder.",
            MappingCount: mappings.Count,
            ActiveMappingCount: mappings.Count(x => x.IsActive),
            SourceObjects: sourceObjects,
            TargetCoverage: targetCoverage,
            Mappings: mappings);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetJobsMonitorAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var jobs = await dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.JobType)
            .ThenBy(x => x.JobCode)
            .Select(x => new AdminJobMonitorRowDto(
                x.Id.ToString(),
                x.JobCode,
                x.JobName,
                x.JobType.ToString(),
                x.TargetType ?? "PlantProcessIQ",
                x.TargetId.HasValue ? x.TargetId.Value.ToString() : "Global / System",
                x.LastRunStatus.ToString(),
                ToStatusClass(x.LastRunStatus.ToString(), x.IsEnabled),
                x.LastRunStartedAtUtc,
                x.LastRunDurationMs,
                x.NextRunAtUtc,
                null,
                x.LastFailureReason,
                true,
                true))
            .ToListAsync(cancellationToken);

        var response = new AdminJobsMonitorDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Summary:
            [
                new AdminStatusCountDto("Total", jobs.Count),
                new AdminStatusCountDto("Enabled", jobs.Count(x => x.IsConfigured)),
                new AdminStatusCountDto("Running", jobs.Count(x => x.Status == "Running")),
                new AdminStatusCountDto("Ok", jobs.Count(x => x.Status == "Ok")),
                new AdminStatusCountDto("Failed", jobs.Count(x => x.Status == "Failed")),
                new AdminStatusCountDto("Timeout", jobs.Count(x => x.Status == "Timeout")),
                new AdminStatusCountDto("NeverRun", jobs.Count(x => x.Status == "NeverRun"))
            ],
            Jobs: jobs);

        return Results.Ok(response);
    }

    private static string InferJobType(string? importType)
    {
        if (string.IsNullOrWhiteSpace(importType))
            return "RawSnapshot";

        var normalized = importType.Trim().ToLowerInvariant();

        if (normalized.Contains("sql") ||
            normalized.Contains("oracle") ||
            normalized.Contains("postgres") ||
            normalized.Contains("server") ||
            normalized.Contains("csv") ||
            normalized.Contains("excel") ||
            normalized.Contains("api") ||
            normalized.Contains("snapshot"))
        {
            return "RawSnapshot";
        }

        if (normalized.Contains("synthetic"))
            return "DemoSeed";

        return "RawSnapshot";
    }

    private static string ToStatusClass(string? status, bool isEnabled = true)
    {
        if (!isEnabled)
            return "paused";

        return status?.Trim().ToLowerInvariant() switch
        {
            "ok" => "success",
            "completed" => "success",
            "running" => "running",
            "failed" => "danger",
            "timeout" => "danger",
            "cancelled" => "warning",
            "created" => "neutral",
            "configured" => "info",
            "neverrun" => "neutral",
            _ => "neutral"
        };
    }

    private sealed record AdminOverviewDto(
        DateTime GeneratedAtUtc,
        string Status,
        IReadOnlyList<AdminMetricCardDto> Cards,
        AdminLatestImportBatchDto? LatestImportBatch);

    private sealed record AdminMetricCardDto(
        string Label,
        int Value,
        string Note,
        string Group);

    private sealed record AdminLatestImportBatchDto(
        Guid Id,
        string ImportBatchCode,
        string ImportType,
        string Status,
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc,
        int? RowCount,
        string? ErrorMessage);

    private sealed record TwoStageImportModelDto(
        DateTime GeneratedAtUtc,
        string ModelName,
        string Summary,
        IReadOnlyList<TwoStageImportStageDto> Stages,
        IReadOnlyList<AdminMetricCardDto> Metrics);

    private sealed record TwoStageImportStageDto(
        int StageNo,
        string StageCode,
        string StageName,
        string Purpose,
        string CurrentImplementation,
        string RefreshOwner,
        int CurrentCount,
        string Status);

    private sealed record DbConfigurationSummaryDto(
        DateTime GeneratedAtUtc,
        string Message,
        IReadOnlyList<PlannedProviderDto> PlannedProviderTypes,
        IReadOnlyList<DbConfigurationSourceSystemDto> SourceSystems);

    private sealed record PlannedProviderDto(
        string ProviderType,
        string Description,
        string RoadmapStatus,
        bool RecommendedForFirstDemo);

    private sealed record DbConfigurationSourceSystemDto(
        Guid Id,
        string SourceSystemCode,
        string SourceSystemName,
        string SourceSystemType,
        string? Description,
        bool IsReadOnlySource,
        bool IsActive,
        int ImportBatchCount,
        int CompletedBatchCount,
        int RunningBatchCount,
        int FailedBatchCount,
        DateTime? LastImportAtUtc);

    private sealed record SchemaConfigurationSummaryDto(
        DateTime GeneratedAtUtc,
        string Message,
        int MappingCount,
        int ActiveMappingCount,
        IReadOnlyList<SourceObjectCoverageDto> SourceObjects,
        IReadOnlyList<AdminStatusCountDto> TargetCoverage,
        IReadOnlyList<SchemaMappingSummaryDto> Mappings);

    private sealed record SourceObjectCoverageDto(
        string SourceObjectName,
        int TotalRows,
        int PendingRows,
        int MappedRows,
        int FailedRows,
        int SkippedRows);

    private sealed record SchemaMappingSummaryDto(
        Guid Id,
        string MappingCode,
        string MappingName,
        string SourceObjectName,
        string TargetEntityName,
        string MappingVersion,
        bool IsActive,
        string? Description);

    private sealed record AdminJobsMonitorDto(
        DateTime GeneratedAtUtc,
        IReadOnlyList<AdminStatusCountDto> Summary,
        IReadOnlyList<AdminJobMonitorRowDto> Jobs);

    private sealed record AdminStatusCountDto(
        string Status,
        int Count);

    private sealed record AdminJobMonitorRowDto(
        string Id,
        string JobCode,
        string JobName,
        string JobType,
        string SourceSystemCode,
        string SourceSystemName,
        string Status,
        string StatusClass,
        DateTime? LastRunAtUtc,
        long? LastDurationMs,
        DateTime? NextRunAtUtc,
        int? RowCount,
        string? ErrorMessage,
        bool IsConfigured,
        bool IsRealRuntimeJob);
}