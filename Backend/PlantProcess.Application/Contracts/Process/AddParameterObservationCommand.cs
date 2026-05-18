using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Process;

public sealed record AddParameterObservationCommand(
    Guid MaterialUnitId,
    Guid? ProcessStepExecutionId,
    Guid ParameterDefinitionId,
    Guid? EquipmentId,
    DateTime ObservedAtUtc,
    decimal? NumericValue,
    string? TextValue,
    bool? BooleanValue,
    string? UnitOfMeasure,
    string? QualityFlag,
    string? RawValue,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);



