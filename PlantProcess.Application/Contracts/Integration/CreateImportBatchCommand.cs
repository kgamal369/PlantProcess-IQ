using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Integration;

public sealed record CreateImportBatchCommand(
    Guid SourceSystemDefinitionId,
    string ImportBatchCode,
    string ImportType,
    string? SourceObjectName,
    string? FileName,
    string? Checksum,
    CommandMetadata Metadata);