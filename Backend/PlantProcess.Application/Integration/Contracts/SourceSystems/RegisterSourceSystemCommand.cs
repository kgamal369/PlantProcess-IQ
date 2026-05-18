using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Integration.Contracts.SourceSystems;

public sealed record RegisterSourceSystemCommand(
    string SourceSystemCode,
    string SourceSystemName,
    string SourceSystemType,
    bool IsReadOnlySource,
    string? Description,
    CommandMetadata Metadata);


