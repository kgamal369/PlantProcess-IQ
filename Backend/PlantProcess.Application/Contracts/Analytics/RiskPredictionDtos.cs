namespace PlantProcess.Application.Contracts.Analytics;

public sealed record CalculateRiskScoreCommand(
    Guid MaterialUnitId,
    string RiskType,
    string? ModelVersion,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    bool StoreResult,
    string? RequestedBy,
    string? CorrelationId);

public sealed record CalculateRiskScoreResult(
    Guid? RiskScoreId,
    Guid MaterialUnitId,
    string RiskType,
    decimal Score,
    string RiskClass,
    string ModelVersion,
    string ScoringStrategy,
    string MainContributorsJson,
    DateTime CalculatedAtUtc,
    bool Stored,
    MaterialFeatureVectorDto FeatureVector,
    IReadOnlyList<RiskContributorDto> Contributors);

public sealed record RiskContributorDto(
    string ContributorType,
    string ContributorCode,
    string ContributorName,
    decimal Weight,
    decimal Contribution,
    string Direction,
    string Explanation);

public sealed record CalculateRiskScoresBatchCommand(
    Guid? SiteId,
    string RiskType,
    int MaxMaterials,
    bool StoreResult,
    string? RequestedBy,
    string? CorrelationId);

public sealed record CalculateRiskScoresBatchResult(
    int CandidatesScanned,
    int ScoresCalculated,
    int ScoresStored,
    int Skipped,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    TimeSpan Duration,
    IReadOnlyList<CalculateRiskScoreResult> Results,
    IReadOnlyList<string> Warnings);

public sealed record CalculateRiskScoreRequest(
    string? RiskType,
    string? ModelVersion,
    bool? StoreResult);

public sealed record CalculateRiskScoresBatchRequest(
    Guid? SiteId,
    string? RiskType,
    int? MaxMaterials,
    bool? StoreResult);
