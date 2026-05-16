using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Integration.Commands;

public sealed record CreateMappingDefinitionCommand(
    Guid SourceSystemDefinitionId,
    string MappingCode,
    string MappingName,
    string SourceObjectName,
    string TargetEntityName,
    string MappingJson,
    string? MappingVersion,
    string? Description,
    CommandMetadata Metadata);