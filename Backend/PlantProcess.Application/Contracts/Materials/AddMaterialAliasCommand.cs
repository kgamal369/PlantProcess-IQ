using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Materials;

public sealed record AddMaterialAliasCommand(
    Guid MaterialUnitId,
    string AliasCode,
    string SourceSystem,
    string? AliasType,
    CommandMetadata Metadata);


