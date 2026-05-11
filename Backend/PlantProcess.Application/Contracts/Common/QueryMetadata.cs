namespace PlantProcess.Application.Contracts.Common;

public sealed record QueryMetadata(
    string? RequestedBy = null,
    string? CorrelationId = null);