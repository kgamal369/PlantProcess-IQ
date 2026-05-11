using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

public class ParameterObservation : BaseEntity
{
    public Guid MaterialUnitId { get; private set; }

    public Guid? ProcessStepExecutionId { get; private set; }

    public Guid ParameterDefinitionId { get; private set; }

    public Guid? EquipmentId { get; private set; }

    public DateTime ObservedAtUtc { get; private set; }

    public DateTime ObservedAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    public decimal? NumericValue { get; private set; }

    public string? TextValue { get; private set; }

    public bool? BooleanValue { get; private set; }

    public string? UnitOfMeasure { get; private set; }

    public string QualityFlag { get; private set; } = "Valid";
    // Valid, Missing, Estimated, Outlier, Invalid, Converted

    public string? RawValue { get; private set; }

    private ParameterObservation()
    {
    }

    public ParameterObservation(
        Guid materialUnitId,
        Guid parameterDefinitionId,
        DateTime observedAtUtc,
        bool isSynthetic,
        decimal? numericValue = null,
        string? textValue = null,
        bool? booleanValue = null,
        string? unitOfMeasure = null,
        Guid? processStepExecutionId = null,
        Guid? equipmentId = null,
        string? qualityFlag = null,
        string? rawValue = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "Europe/Berlin",
        int plantUtcOffsetMinutes = 60)
    {
        if (materialUnitId == Guid.Empty)
            throw new ArgumentException("Material unit ID is required.", nameof(materialUnitId));

        if (parameterDefinitionId == Guid.Empty)
            throw new ArgumentException("Parameter definition ID is required.", nameof(parameterDefinitionId));

        MaterialUnitId = materialUnitId;
        ParameterDefinitionId = parameterDefinitionId;
        ObservedAtUtc = EnsureUtc(observedAtUtc);
        ObservedAtLocal = DateTime.SpecifyKind(
            ObservedAtUtc.AddMinutes(plantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        PlantTimeZoneId = string.IsNullOrWhiteSpace(plantTimeZoneId)
            ? "Europe/Berlin"
            : plantTimeZoneId.Trim();

        PlantUtcOffsetMinutes = plantUtcOffsetMinutes;

        NumericValue = numericValue;
        TextValue = textValue?.Trim();
        BooleanValue = booleanValue;
        UnitOfMeasure = unitOfMeasure?.Trim();
        ProcessStepExecutionId = processStepExecutionId;
        EquipmentId = equipmentId;
        QualityFlag = string.IsNullOrWhiteSpace(qualityFlag) ? "Valid" : qualityFlag.Trim();
        RawValue = rawValue?.Trim();

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void MarkAsOutlier()
    {
        QualityFlag = "Outlier";
        MarkAsUpdated();
    }

    public void MarkAsConverted(string newUnitOfMeasure)
    {
        if (string.IsNullOrWhiteSpace(newUnitOfMeasure))
            throw new ArgumentException("New unit of measure is required.", nameof(newUnitOfMeasure));

        UnitOfMeasure = newUnitOfMeasure.Trim();
        QualityFlag = "Converted";
        MarkAsUpdated();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}