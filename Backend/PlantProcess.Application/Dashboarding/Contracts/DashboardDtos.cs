namespace PlantProcess.Application.Dashboarding.Contracts;

// ============================================================================
// Phase 8 / 9 - Shared Dashboard Query Model
// ============================================================================

public sealed record DashboardQueryDto(
    Guid? SiteId,
    Guid? AreaId,
    Guid? EquipmentId,
    string? MaterialCode,
    string? SourceSystem,
    string? DefectType,
    string? RiskClass,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? ShiftCode,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDirection)
{
    public int SafePage => Page <= 0 ? 1 : Page;
    public int SafePageSize => Math.Clamp(PageSize <= 0 ? 25 : PageSize, 1, 200);
    public string SafeSortDirection =>
        string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
}

public sealed record DashboardWorkspaceDto(
    DateTime GeneratedAtUtc,
    DashboardQueryDto Query,
    DashboardOverviewDto Overview,
    QualityDashboardDto Quality,
    RiskDashboardDto Risk,
    DataQualityDashboardDto DataQuality,
    DashboardPagedResultDto<DashboardMaterialRowDto> Materials);

public sealed record DashboardPagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string? SortBy,
    string? SortDirection);

public sealed record DashboardReferenceDataDto(
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardReferenceItemDto> Sites,
    IReadOnlyList<DashboardReferenceItemDto> Areas,
    IReadOnlyList<DashboardReferenceItemDto> Equipment,
    IReadOnlyList<DashboardReferenceItemDto> SourceSystems,
    IReadOnlyList<DashboardReferenceItemDto> Defects,
    IReadOnlyList<DashboardReferenceItemDto> Parameters,
    IReadOnlyList<DashboardReferenceItemDto> RiskClasses,
    IReadOnlyList<DashboardReferenceItemDto> Shifts);

public sealed record DashboardReferenceItemDto(
    string Id,
    string Code,
    string Name,
    string? Group,
    int Count);

public sealed record DashboardMaterialRowDto(
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    string? ProductFamily,
    string? GradeOrRecipe,
    Guid SiteId,
    string? SiteName,
    DateTime? ProductionStartUtc,
    DateTime? ProductionEndUtc,
    string? SourceSystem,
    int ProcessStepCount,
    int ParameterObservationCount,
    int QualityEventCount,
    int DefectEventCount,
    decimal? LatestRiskScore,
    string? LatestRiskClass,
    DateTime? LatestScoredAtUtc);

public sealed record DashboardOverviewDto(
    DateTime GeneratedAtUtc,
    Guid? SiteId,
    int Sites,
    int Materials,
    int ProcessSteps,
    int ParameterObservations,
    int QualityEvents,
    int DefectEvents,
    int DataQualityIssues,
    int RiskScores,
    int HighRiskMaterials,
    int CorrelationResults,
    decimal DefectRatePercent,
    decimal HighRiskRatePercent,
    IReadOnlyList<DashboardMetricDto> Metrics,
    IReadOnlyList<DashboardTrendPointDto> DefectTrend,
    IReadOnlyList<DashboardRiskContributorDto> TopRiskContributors);

public sealed record DashboardMetricDto(
    string MetricCode,
    string MetricName,
    decimal Value,
    string? Unit,
    string Interpretation);

public sealed record DashboardTrendPointDto(
    DateTime DateUtc,
    int MaterialCount,
    int QualityEventCount,
    int DefectEventCount,
    decimal DefectRatePercent);

public sealed record DashboardRiskContributorDto(
    string ContributorType,
    string ContributorCode,
    int Count,
    decimal AverageRiskScore);

public sealed record QualityDashboardDto(
    DateTime GeneratedAtUtc,
    Guid? SiteId,
    int QualityEvents,
    int DefectEvents,
    decimal DefectRatePercent,
    IReadOnlyList<DefectBreakdownDto> DefectBreakdown,
    IReadOnlyList<DecisionBreakdownDto> DecisionBreakdown);

public sealed record DefectBreakdownDto(
    string? DefectCode,
    string? DefectName,
    string? DefectCategory,
    int Count,
    decimal PercentOfDefects);

public sealed record DecisionBreakdownDto(
    string? Decision,
    int Count,
    decimal PercentOfQualityEvents);

public sealed record RiskDashboardDto(
    DateTime GeneratedAtUtc,
    Guid? SiteId,
    int RiskScores,
    int HighRiskScores,
    decimal AverageScore,
    IReadOnlyList<RiskClassBreakdownDto> RiskClassBreakdown,
    IReadOnlyList<HighRiskMaterialDto> HighRiskMaterials);

public sealed record RiskClassBreakdownDto(
    string? RiskClass,
    int Count,
    decimal Percent);

public sealed record HighRiskMaterialDto(
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    string? ProductFamily,
    string? GradeOrRecipe,
    string RiskType,
    decimal Score,
    string? RiskClass,
    string? ModelVersion,
    DateTime ScoredAtUtc);

public sealed record DataQualityDashboardDto(
    DateTime GeneratedAtUtc,
    Guid? SiteId,
    int TotalIssues,
    int OpenIssues,
    IReadOnlyList<DataQualityIssueBreakdownDto> SeverityBreakdown,
    IReadOnlyList<DataQualityIssueBreakdownDto> IssueTypeBreakdown);

public sealed record DataQualityIssueBreakdownDto(
    string Code,
    int Count,
    decimal Percent);



