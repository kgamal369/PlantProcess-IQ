namespace PlantProcess.Application.Contracts.Common;

public sealed record CommandMetadata(
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId,
    string? RequestedBy = null,
    string? CorrelationId = null);



