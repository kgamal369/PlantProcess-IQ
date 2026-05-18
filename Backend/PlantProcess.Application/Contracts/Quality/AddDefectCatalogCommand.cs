using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Quality;

public sealed record AddDefectCatalogCommand(
    string DefectCode,
    string DefectName,
    string? DefectCategory,
    string? IndustryTemplate,
    CommandMetadata Metadata);


