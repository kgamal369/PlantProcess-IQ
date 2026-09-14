namespace PlantProcess.Application.Integration.Contracts.Mapping;

/// <summary>
/// PPIQ T-099. The truthful answer to one reprocess attempt. Resolved is only
/// ever reported once the canonical write has actually been persisted.
/// </summary>
public sealed record QuarantineReprocessResult(
    Guid QuarantineId,
    Guid StagingRecordId,
    int StagingRowNumber,
    string State,
    int AttemptCount,
    string? ValidationCode,
    string? Detail,
    string? OffendingValue,
    string? SuggestedCorrection,
    Guid? ResolvedCanonicalId,
    string? ResolvedCanonicalName);
