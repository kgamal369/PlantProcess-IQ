namespace PlantProcess.Application.Licensing.Contracts;

public enum LicenseFeature
{
    ReadOnlySourceRegistry = 1,

    CsvImport = 100,
    ExcelImport = 101,
    PostgreSqlConnector = 102,
    SqlServerConnector = 103,
    OracleConnector = 104,
    MySqlConnector = 105,
    RestApiConnector = 106,
    OpcUaHistorianConnector = 107,

    DbLinkConfiguration = 200,
    DumpStagingRetention = 201,
    SourceSnapshotImport = 202,
    IncrementalImport = 203,

    SchemaSqlViewBuilder = 300,
    CrossSourceJoinExecution = 301,
    KpiViewBuilder = 302,
    SchemaPreviewExecution = 303,
    MappingExecution = 304,

    WidgetScriptLayer = 400,
    DashboardPageBuilder = 401,
    DashboardWidgetBuilder = 402,
    DashboardLayoutPersistence = 403,

    DataQualityBasicScan = 500,
    DataQualityFullScan = 501,

    RiskDashboardView = 600,
    RiskDashboardContributors = 601,

    CorrelationManualRun = 700,
    CorrelationScheduledRun = 701,

    InvestigationWorkflow = 800,

    MlWorkspacePreview = 900,
    MlLearningJobs = 901,
    SuggestionRecommendation = 902,

    BasicInvestigationReportPdf = 1000,
    FullGenealogyReportPdf = 1001,
    BrandedReportPdf = 1002
}