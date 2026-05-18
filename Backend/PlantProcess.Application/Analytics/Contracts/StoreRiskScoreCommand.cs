using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Analytics.Contracts;

public sealed record StoreRiskScoreCommand(
    Guid MaterialUnitId,
    string RiskType,
    decimal Score,
    string? RiskClass,
    string? MainContributorsJson,
    string? ModelVersion,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);



