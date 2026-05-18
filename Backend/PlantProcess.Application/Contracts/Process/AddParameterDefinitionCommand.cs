using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Process;

public sealed record AddParameterDefinitionCommand(
    string ParameterCode,
    string ParameterName,
    string ValueType,
    string? UnitOfMeasure,
    string? ParameterCategory,
    string? IndustryTemplate,
    decimal? ExpectedMinValue,
    decimal? ExpectedMaxValue,
    CommandMetadata Metadata);



