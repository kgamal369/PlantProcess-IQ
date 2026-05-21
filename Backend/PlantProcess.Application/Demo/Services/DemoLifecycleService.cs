using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Demo.Contracts;
using PlantProcess.Application.Demo.Interfaces;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Demo.Services;

public sealed class DemoLifecycleService : IDemoLifecycleService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILicenseService _licenseService;

    public DemoLifecycleService(
        IPlantProcessDbContext dbContext,
        ILicenseService licenseService)
    {
        _dbContext = dbContext;
        _licenseService = licenseService;
    }

    public async Task<ApplicationResult<DemoLifecycleDto>> GetDemoLifecycleAsync(CancellationToken cancellationToken)
    {
        var license = _licenseService.GetStatus();

        var connectorTruth = BuildConnectorTruth();
        var stagingSummary = await BuildStagingSummaryAsync(cancellationToken);
        var schemaMapping = await BuildSchemaMappingSummaryAsync(cancellationToken);
        var jobChain = await BuildJobChainAsync(cancellationToken);
        var dashboardOutput = await BuildDashboardOutputSummaryAsync(cancellationToken);
        var mlReadiness = await BuildMlReadinessSummaryAsync(cancellationToken);
        var reportClose = BuildReportCloseSummary();

        var steps = new List<DemoLifecycleStepDto>
        {
            new(
                1,
                "POSITIONING",
                "Read-only manufacturing intelligence layer",
                "Ready",
                "Light",
                "PlantProcess IQ connects plant data for quality/process investigation without replacing MES, SCADA, L2, PLC control or BI.",
                "/"),

            new(
                2,
                "LICENSE",
                "License tier and feature limits",
                "Ready",
                license.Tier,
                "The active license controls connectors, refresh intervals, source count, dashboards, SQL editor, correlation, ML preview and reports.",
                "/admin/license/current"),

            new(
                3,
                "CONNECT",
                "Configure allowed data source",
                connectorTruth.Connectors.Any(x => x.IsSafeForDemo) ? "Ready" : "NeedsAttention",
                "Light",
                "A user configures a safe read-only connector or snapshot source.",
                "/admin/connectors/provider-types"),

            new(
                4,
                "STAGE",
                "Create internal staging/dump copy",
                stagingSummary.StagingRecordCount > 0 || stagingSummary.ActiveConnectionProfiles > 0 ? "Ready" : "Partial",
                "Light",
                "Customer data is copied into PlantProcess IQ staging without changing the customer source schema.",
                "/admin/connectors/datasets"),

            new(
                5,
                "MAP",
                "Map source-specific data into generic canonical model",
                schemaMapping.ActiveMappingDefinitionCount > 0 || schemaMapping.ActiveSchemaViewCount > 0 ? "Ready" : "Partial",
                "Pro",
                "Schema views and mapping definitions transform plant-specific tables into the generic canonical model.",
                "/admin/schema-configuration/views"),

            new(
                6,
                "MONITOR",
                "Monitor the operational job chain",
                jobChain.TotalJobs > 0 ? "Ready" : "Partial",
                "Light",
                "Jobs Monitor shows source snapshot, canonical refresh, data quality, risk scoring, correlation-style custom jobs and future ML learning jobs.",
                "/admin/jobs-monitor"),

            new(
                7,
                "DASHBOARD",
                "Show dashboard generated from configured data",
                dashboardOutput.ActiveWidgetCount > 0 ? "Ready" : "Partial",
                "Light",
                "Dashboards use backend metadata and mapped canonical data instead of one endpoint per customer table.",
                "/analytics/dashboard/definitions"),

            new(
                8,
                "QUALITY_RISK",
                "Show data-quality and rule-based risk",
                "Ready",
                "Pro",
                "Current intelligence is data quality scanning, rule-based risk scoring and suspected contributor analysis.",
                "/analytics/dashboard/data-quality"),

            new(
                9,
                "CORRELATE",
                "Run or show correlation analysis",
                _licenseService.IsFeatureEnabled(LicenseFeature.CorrelationManualRun) ? "Ready" : "Locked",
                _licenseService.GetRequiredTierForFeature(LicenseFeature.CorrelationManualRun),
                "Correlation explains statistical patterns and suspected contributors, not guaranteed root cause.",
                "/analytics/correlations"),

            new(
                10,
                "INVESTIGATE",
                "Open material/process investigation",
                _licenseService.IsFeatureEnabled(LicenseFeature.InvestigationWorkflow) ? "Ready" : "Locked",
                _licenseService.GetRequiredTierForFeature(LicenseFeature.InvestigationWorkflow),
                "Investigation connects genealogy, process parameters, quality events, data quality issues and risk.",
                "/materials/{materialUnitId}/investigation-full"),

            new(
                11,
                "ML_READINESS",
                "Show honest ML readiness preview",
                _licenseService.IsFeatureEnabled(LicenseFeature.MlWorkspacePreview) ? "Ready" : "Locked",
                _licenseService.GetRequiredTierForFeature(LicenseFeature.MlWorkspacePreview),
                "No trained production model is active. ML is readiness-driven and requires validated labeled historical data.",
                "/demo/lifecycle"),

            new(
                12,
                "REPORT",
                "Close with customer-grade report",
                _licenseService.IsFeatureEnabled(LicenseFeature.BasicInvestigationReportPdf) ? "Ready" : "Locked",
                _licenseService.GetRequiredTierForFeature(LicenseFeature.BasicInvestigationReportPdf),
                "The final report bridges demo to paid Data Diagnostic.",
                "/reports/readiness-assessment")
        };

        var dto = new DemoLifecycleDto(
            GeneratedAtUtc: DateTime.UtcNow,
            DemoMode: "ControlledProductLifecycle",
            License: license,
            Steps: steps,
            ConnectorTruth: connectorTruth,
            StagingSummary: stagingSummary,
            SchemaMapping: schemaMapping,
            JobChain: jobChain,
            DashboardOutput: dashboardOutput,
            MlReadiness: mlReadiness,
            ReportClose: reportClose);

        return ApplicationResult<DemoLifecycleDto>.Success(dto);
    }

    private DemoConnectorTruthDto BuildConnectorTruth()
    {
        var connectors = new[]
        {
            BuildConnector(
                providerType: "Csv",
                displayName: "CSV Snapshot",
                implementationStatus: "ImplementedAndTested",
                feature: LicenseFeature.CsvImport,
                implementationCanBeDemoSafe: true),

            BuildConnector(
                providerType: "Excel",
                displayName: "Excel Snapshot",
                implementationStatus: "ImplementedButRequiresEndToEndProof",
                feature: LicenseFeature.ExcelImport,
                implementationCanBeDemoSafe: false),

            BuildConnector(
                providerType: "PostgreSql",
                displayName: "PostgreSQL Read-only DB Link",
                implementationStatus: "ImplementedRequiresEnvironmentProof",
                feature: LicenseFeature.PostgreSqlConnector,
                implementationCanBeDemoSafe: true),

            BuildConnector(
                providerType: "SqlServer",
                displayName: "Microsoft SQL Server",
                implementationStatus: "Planned",
                feature: LicenseFeature.SqlServerConnector,
                implementationCanBeDemoSafe: false),

            BuildConnector(
                providerType: "Oracle",
                displayName: "Oracle",
                implementationStatus: "Planned",
                feature: LicenseFeature.OracleConnector,
                implementationCanBeDemoSafe: false),

            BuildConnector(
                providerType: "MySql",
                displayName: "MySQL",
                implementationStatus: "Planned",
                feature: LicenseFeature.MySqlConnector,
                implementationCanBeDemoSafe: false),

            BuildConnector(
                providerType: "RestApi",
                displayName: "REST API",
                implementationStatus: "Future",
                feature: LicenseFeature.RestApiConnector,
                implementationCanBeDemoSafe: false),

            BuildConnector(
                providerType: "OpcUaHistorian",
                displayName: "OPC-UA / Historian",
                implementationStatus: "Future",
                feature: LicenseFeature.OpcUaHistorianConnector,
                implementationCanBeDemoSafe: false)
        };

        return new DemoConnectorTruthDto(connectors);
    }

    private DemoConnectorStatusDto BuildConnector(
        string providerType,
        string displayName,
        string implementationStatus,
        LicenseFeature feature,
        bool implementationCanBeDemoSafe)
    {
        var allowedByLicense = _licenseService.IsFeatureEnabled(feature);
        var safeForDemo = implementationCanBeDemoSafe && allowedByLicense;

        return new DemoConnectorStatusDto(
            ProviderType: providerType,
            DisplayName: displayName,
            ImplementationStatus: implementationStatus,
            LicenseStatus: allowedByLicense
                ? "AllowedByCurrentLicense"
                : $"Requires{_licenseService.GetRequiredTierForFeature(feature)}",
            IsAvailableNow: implementationCanBeDemoSafe,
            IsAllowedByLicense: allowedByLicense,
            IsSafeForDemo: safeForDemo,
            Message: safeForDemo
                ? "Safe to show in the controlled demo."
                : "Do not show as fully available in demo unless implementation, tests and license are all green.");
    }

    private async Task<DemoStagingSummaryDto> BuildStagingSummaryAsync(CancellationToken cancellationToken)
    {
        var activeConnections = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var activeDatasets = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var stagingRecords = await _dbContext.StagingRecords
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var importBatches = await _dbContext.ImportBatches
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var lastSnapshot = await _dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var status = stagingRecords > 0
            ? "Ready"
            : activeConnections > 0
                ? "Partial"
                : "NotConfigured";

        return new DemoStagingSummaryDto(
            ActiveConnectionProfiles: activeConnections,
            ActiveDatasets: activeDatasets,
            StagingRecordCount: stagingRecords,
            ImportBatchCount: importBatches,
            LastSnapshotUtc: lastSnapshot,
            Status: status,
            Message: "Staging/dump copy proves PlantProcess IQ can hold a safe copy of customer data before canonical mapping.");
    }

    private async Task<DemoSchemaMappingSummaryDto> BuildSchemaMappingSummaryAsync(CancellationToken cancellationToken)
    {
        var schemaViews = await _dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeSchemaViews = await _dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var mappings = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeMappings = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var kpis = await _dbContext.KpiDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var status = activeMappings > 0 || activeSchemaViews > 0
            ? "Ready"
            : schemaViews > 0
                ? "Partial"
                : "NotConfigured";

        return new DemoSchemaMappingSummaryDto(
            SchemaViewCount: schemaViews,
            ActiveSchemaViewCount: activeSchemaViews,
            MappingDefinitionCount: mappings,
            ActiveMappingDefinitionCount: activeMappings,
            KpiDefinitionCount: kpis,
            Status: status,
            Message: "Schema mapping is the centerpiece of product genericity across plants, industries, source schemas and inspection devices.");
    }

    private async Task<DemoJobChainDto> BuildJobChainAsync(CancellationToken cancellationToken)
    {
        var rawJobs = await _dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.JobType)
            .ThenBy(x => x.JobCode)
            .Select(x => new
            {
                x.Id,
                x.JobCode,
                x.JobName,
                x.JobType,
                x.IsEnabled,
                x.LastRunStatus,
                x.LastRunStartedAtUtc,
                x.LastRunCompletedAtUtc,
                x.LastRunDurationMs
            })
            .ToListAsync(cancellationToken);

        var jobs = rawJobs
            .Select(x => new DemoJobStatusDto(
                JobId: x.Id,
                JobCode: x.JobCode,
                JobName: x.JobName,
                JobType: x.JobType.ToString(),
                IsEnabled: x.IsEnabled,
                LastRunStatus: x.LastRunStatus.ToString(),
                LastRunStartedAtUtc: x.LastRunStartedAtUtc,
                LastRunFinishedAtUtc: x.LastRunCompletedAtUtc,
                LastRunDurationMs: x.LastRunDurationMs,
                LicenseStatus: ResolveJobLicenseStatus(x.JobType, x.JobCode, x.JobName),
                OperationalRole: ResolveOperationalRole(x.JobType, x.JobCode, x.JobName)))
            .ToList();

        return new DemoJobChainDto(
            TotalJobs: jobs.Count,
            EnabledJobs: jobs.Count(x => x.IsEnabled),
            FailedOrTimeoutJobs: jobs.Count(x =>
                x.LastRunStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
                x.LastRunStatus.Equals("Timeout", StringComparison.OrdinalIgnoreCase)),
            Jobs: jobs);
    }

    private async Task<DemoDashboardOutputSummaryDto> BuildDashboardOutputSummaryAsync(CancellationToken cancellationToken)
    {
        var dashboards = await _dbContext.DashboardDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && !x.IsSystemTemplate, cancellationToken);

        var activeDashboards = await _dbContext.DashboardDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive && !x.IsSystemTemplate, cancellationToken);

        var widgets = await _dbContext.DashboardWidgetDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeWidgets = await _dbContext.DashboardWidgetDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var status = activeDashboards > 0 && activeWidgets > 0
            ? "Ready"
            : "Partial";

        return new DemoDashboardOutputSummaryDto(
            DashboardCount: dashboards,
            ActiveDashboardCount: activeDashboards,
            WidgetCount: widgets,
            ActiveWidgetCount: activeWidgets,
            Status: status,
            Message: "Dashboard output must be shown after configuration/mapping so it is understood as generated from configured canonical data.");
    }

    private async Task<DemoMlReadinessSummaryDto> BuildMlReadinessSummaryAsync(CancellationToken cancellationToken)
    {
        var parameterObservations = await _dbContext.ParameterObservations
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var qualityEvents = await _dbContext.QualityEvents
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var genealogyEdges = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var correlationResults = await _dbContext.CorrelationResults
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var modelRegistry = await _dbContext.ModelRegistries
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var warnings = new List<string>();

        if (parameterObservations < 1000)
            warnings.Add("Parameter observation count is below the suggested minimum for reliable learning.");

        if (qualityEvents < 200)
            warnings.Add("Quality event count is below the suggested minimum for meaningful labels.");

        if (genealogyEdges == 0)
            warnings.Add("Genealogy coverage is missing or incomplete.");

        if (modelRegistry == 0)
            warnings.Add("No trained production ML model registry entry is active.");

        warnings.Add("Current results are rule-based risk, data-quality scanning, statistical correlation and suspected contributor ranking only.");

        var readiness = parameterObservations >= 1000 && qualityEvents >= 200 && genealogyEdges > 0
            ? "DataAlmostReadyForFutureTraining"
            : "NotReadyForTraining";

        return new DemoMlReadinessSummaryDto(
            ModelStatus: "NoTrainedProductionModelActive",
            TrainingStatus: "DisabledUntilValidatedLabeledHistoricalDataExists",
            CurrentIntelligence: "Rule-based risk scoring, data-quality scanning, statistical correlation and suspected contributor ranking.",
            RequiredBeforeTraining: "Validated source-to-canonical pipeline, sufficient labeled quality outcomes, genealogy coverage and stable feature vectors.",
            ParameterObservationCount: parameterObservations,
            QualityEventCount: qualityEvents,
            GenealogyEdgeCount: genealogyEdges,
            CorrelationResultCount: correlationResults,
            ModelRegistryCount: modelRegistry,
            ReadinessStatus: readiness,
            Warnings: warnings);
    }

    private static DemoReportCloseSummaryDto BuildReportCloseSummary()
    {
        return new DemoReportCloseSummaryDto(
            ReportPurpose: "Customer-grade evidence summary for demo and Data Diagnostic.",
            DataDiagnosticBridge: "The report converts the demo into a paid Data Diagnostic by summarizing connected data, mapping coverage, quality issues, risk/correlation findings, ML readiness and recommended next actions.",
            Disclaimer: "PlantProcess IQ is a read-only intelligence layer. It does not replace MES, SCADA, L2, PLC control or BI tools. It does not claim guaranteed root cause or production-ready AI prediction.",
            IncludedSections: new[]
            {
                "Executive summary",
                "Connected data sources",
                "Connector truth/status",
                "Staging/dump summary",
                "Schema mapping coverage",
                "Canonical data readiness",
                "Data quality findings",
                "Risk scoring summary",
                "Correlation/suspected contributor summary",
                "Material investigation evidence",
                "ML readiness status",
                "Recommended next actions",
                "Read-only safety statement"
            });
    }

    private string ResolveJobLicenseStatus(
        JobDefinitionType jobType,
        string jobCode,
        string jobName)
    {
        if (jobType == JobDefinitionType.DbLinkImport)
        {
            return _licenseService.IsFeatureEnabled(LicenseFeature.SourceSnapshotImport)
                ? "Allowed"
                : "Locked";
        }

        if (jobType == JobDefinitionType.CanonicalRefresh)
        {
            return _licenseService.IsFeatureEnabled(LicenseFeature.MappingExecution)
                ? "Allowed"
                : "Locked";
        }

        if (jobType == JobDefinitionType.DataQualityScan)
        {
            return _licenseService.IsFeatureEnabled(LicenseFeature.DataQualityBasicScan)
                ? "Allowed"
                : "Locked";
        }

        if (jobType == JobDefinitionType.RiskScoring)
        {
            return _licenseService.IsFeatureEnabled(LicenseFeature.RiskDashboardView)
                ? "Allowed"
                : "Locked";
        }

        if (IsMlJob(jobType))
        {
            return _licenseService.IsFeatureEnabled(LicenseFeature.MlLearningJobs)
                ? "AllowedButDisabledUntilDataReady"
                : "Locked";
        }

        if (LooksLikeCorrelationJob(jobCode, jobName))
        {
            return _licenseService.IsFeatureEnabled(LicenseFeature.CorrelationScheduledRun)
                ? "Allowed"
                : "Locked";
        }

        return "Allowed";
    }

    private static string ResolveOperationalRole(
        JobDefinitionType jobType,
        string jobCode,
        string jobName)
    {
        if (jobType == JobDefinitionType.DbLinkImport)
            return "Source snapshot / staging copy";

        if (jobType == JobDefinitionType.CanonicalRefresh)
            return "Canonical import / mapping execution";

        if (jobType == JobDefinitionType.DataQualityScan)
            return "Data-quality scan";

        if (jobType == JobDefinitionType.RiskScoring)
            return "Rule-based risk scoring";

        if (IsMlJob(jobType))
            return "Future ML learning job - honest disabled/readiness-dependent state";

        if (LooksLikeCorrelationJob(jobCode, jobName))
            return "Correlation / suspected contributors";

        return "Operational job";
    }

    private static bool IsMlJob(JobDefinitionType jobType)
    {
        return jobType is JobDefinitionType.MlParamsVsDefects
            or JobDefinitionType.MlParamsVsDowntime
            or JobDefinitionType.MlParamsVsKpis
            or JobDefinitionType.MlWeeklyFull;
    }

    private static bool LooksLikeCorrelationJob(string jobCode, string jobName)
    {
        return ContainsIgnoreCase(jobCode, "CORRELATION")
            || ContainsIgnoreCase(jobCode, "CORRELATE")
            || ContainsIgnoreCase(jobName, "CORRELATION")
            || ContainsIgnoreCase(jobName, "CORRELATE");
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}