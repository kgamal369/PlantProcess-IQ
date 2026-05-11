using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.DataQuality;

public sealed record RaiseDataQualityIssueCommand(
    Guid? MaterialUnitId,
    string IssueType,
    string? Severity,
    string Description,
    string? AffectedEntityName,
    Guid? AffectedEntityId,
    CommandMetadata Metadata);