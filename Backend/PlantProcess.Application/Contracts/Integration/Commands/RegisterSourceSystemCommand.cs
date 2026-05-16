using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Integration.Commands;

public sealed record RegisterSourceSystemCommand(
    string SourceSystemCode,
    string SourceSystemName,
    string SourceSystemType,
    bool IsReadOnlySource,
    string? Description,
    CommandMetadata Metadata);