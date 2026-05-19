namespace PlantProcess.Application.Contracts.Readiness;

public sealed record ApplicationReadinessDto(
    string Service,
    string Layer,
    string Status,
    string Version,
    DateTime CheckedAtUtc,
    IReadOnlyList<string> RegisteredCapabilities,
    decimal OverallScore,
    string OverallFeasibility,
    IReadOnlyList<ReadinessDimensionDto> Dimensions,
    IReadOnlyList<ReadinessBlockerDto> TopBlockers,
    IReadOnlyList<ReadinessRecommendedActionDto> RecommendedActions,
    PilotFeasibilityDto PilotFeasibility,
    ReadinessEvidenceDto Evidence);

public sealed record ReadinessDimensionDto(
    string Code,
    string Name,
    decimal Score,
    string Status,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Evidence);

public sealed record ReadinessBlockerDto(
    string Blocker,
    string Dimension,
    int EstimatedEffortHours,
    string Severity);

public sealed record ReadinessRecommendedActionDto(
    int Rank,
    string Action,
    string Dimension,
    string ExpectedImpact,
    int EstimatedEffortHours);

public sealed record PilotFeasibilityDto(
    PilotScopeFeasibilityDto Minimal,
    PilotScopeFeasibilityDto Standard,
    PilotScopeFeasibilityDto Full);

public sealed record PilotScopeFeasibilityDto(
    string Scope,
    bool Feasible,
    string Recommendation,
    IReadOnlyList<string> Blockers);

public sealed record ReadinessEvidenceDto(
    int ConnectionProfileCount,
    int ActiveConnectionProfileCount,
    int SourceDatasetCount,
    int SourceFieldCount,
    int MappingDefinitionCount,
    int ActiveMappingDefinitionCount,
    int MaterialUnitCount,
    int MaterialUnitsWithGenealogyCount,
    int GenealogyEdgeCount,
    int ParameterDefinitionCount,
    int ParameterObservationCount,
    int QualityEventCount,
    int DataQualityIssueCount,
    int CriticalDataQualityIssueCount,
    int HighDataQualityIssueCount,
    int RiskScoreCount,
    int ModelRegistryCount,
    int LatestImportBatchCount,
    DateTime? LastSuccessfulImportAtUtc,
    DateTime? LastRiskScoreAtUtc,
    DateTime? LastModelRegisteredAtUtc);

public sealed record CommercialReadinessReportDto(
    Guid AssessmentId,
    DateTime GeneratedAtUtc,
    decimal OverallScore,
    string OverallFeasibility,
    string ExecutiveSummary,
    IReadOnlyList<ReadinessDimensionDto> Dimensions,
    IReadOnlyList<ReadinessBlockerDto> TopBlockers,
    IReadOnlyList<ReadinessRecommendedActionDto> RecommendedActions,
    PilotFeasibilityDto PilotFeasibility,
    ReadinessEvidenceDto Evidence,
    string Disclaimer);

public sealed record CommercialReadinessReportRequest(
    string? CustomerName,
    string? PreparedBy,
    string? RequestedBy);