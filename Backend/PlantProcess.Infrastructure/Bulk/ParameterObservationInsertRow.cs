namespace PlantProcess.Infrastructure.Bulk;

public sealed record ParameterObservationInsertRow
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; init; }

    public bool IsSynthetic { get; init; }

    public string? SourceSystem { get; init; }

    public string? SourceRecordId { get; init; }

    public bool IsDeleted { get; init; }

    public DateTime? DeletedAtUtc { get; init; }

    public string? DeletedReason { get; init; }

    public Guid MaterialUnitId { get; init; }

    public Guid? ProcessStepExecutionId { get; init; }

    public Guid ParameterDefinitionId { get; init; }

    public Guid? EquipmentId { get; init; }

    public DateTime ObservedAtUtc { get; init; }

    public DateTime ObservedAtLocal { get; init; }

    public string PlantTimeZoneId { get; init; } = "UTC";

    public int PlantUtcOffsetMinutes { get; init; }

    public decimal? NumericValue { get; init; }

    public string? TextValue { get; init; }

    public bool? BooleanValue { get; init; }

    public string? UnitOfMeasure { get; init; }

    public string QualityFlag { get; init; } = "Valid";

    public string? RawValue { get; init; }
}