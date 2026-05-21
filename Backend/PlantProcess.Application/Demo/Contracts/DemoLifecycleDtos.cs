using PlantProcess.Application.Licensing.Contracts;

namespace PlantProcess.Application.Demo.Contracts;

public sealed record DemoLifecycleDto(
    DateTime GeneratedAtUtc,
    string DemoMode,
    LicenseStatusDto License,
    IReadOnlyCollection<DemoLifecycleStepDto> Steps,
    DemoConnectorTruthDto ConnectorTruth,
    DemoStagingSummaryDto StagingSummary,
    DemoSchemaMappingSummaryDto SchemaMapping,
    DemoJobChainDto JobChain,
    DemoDashboardOutputSummaryDto DashboardOutput,
    DemoMlReadinessSummaryDto MlReadiness,
    DemoReportCloseSummaryDto ReportClose);

public sealed record DemoLifecycleStepDto(
    int Order,
    string Code,
    string Title,
    string Status,
    string RequiredLicenseTier,
    string Description,
    string EvidenceEndpoint);

public sealed record DemoConnectorTruthDto(
    IReadOnlyCollection<DemoConnectorStatusDto> Connectors);

public sealed record DemoConnectorStatusDto(
    string ProviderType,
    string DisplayName,
    string ImplementationStatus,
    string LicenseStatus,
    bool IsAvailableNow,
    bool IsAllowedByLicense,
    bool IsSafeForDemo,
    string Message);

public sealed record DemoStagingSummaryDto(
    int ActiveConnectionProfiles,
    int ActiveDatasets,
    int StagingRecordCount,
    int ImportBatchCount,
    DateTime? LastSnapshotUtc,
    string Status,
    string Message);

public sealed record DemoSchemaMappingSummaryDto(
    int SchemaViewCount,
    int ActiveSchemaViewCount,
    int MappingDefinitionCount,
    int ActiveMappingDefinitionCount,
    int KpiDefinitionCount,
    string Status,
    string Message);

public sealed record DemoJobChainDto(
    int TotalJobs,
    int EnabledJobs,
    int FailedOrTimeoutJobs,
    IReadOnlyCollection<DemoJobStatusDto> Jobs);

public sealed record DemoJobStatusDto(
    Guid JobId,
    string JobCode,
    string JobName,
    string JobType,
    bool IsEnabled,
    string LastRunStatus,
    DateTime? LastRunStartedAtUtc,
    DateTime? LastRunFinishedAtUtc,
    long? LastRunDurationMs,
    string LicenseStatus,
    string OperationalRole);

public sealed record DemoDashboardOutputSummaryDto(
    int DashboardCount,
    int ActiveDashboardCount,
    int WidgetCount,
    int ActiveWidgetCount,
    string Status,
    string Message);

public sealed record DemoMlReadinessSummaryDto(
    string ModelStatus,
    string TrainingStatus,
    string CurrentIntelligence,
    string RequiredBeforeTraining,
    int ParameterObservationCount,
    int QualityEventCount,
    int GenealogyEdgeCount,
    int CorrelationResultCount,
    int ModelRegistryCount,
    string ReadinessStatus,
    IReadOnlyCollection<string> Warnings);

public sealed record DemoReportCloseSummaryDto(
    string ReportPurpose,
    string DataDiagnosticBridge,
    string Disclaimer,
    IReadOnlyCollection<string> IncludedSections);