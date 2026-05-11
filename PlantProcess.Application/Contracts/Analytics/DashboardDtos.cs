namespace PlantProcess.Application.Contracts.Analytics;

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
