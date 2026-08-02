using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

public class DowntimeEvent : BaseEntity
{
    public Guid? MaterialUnitId { get; private set; }

    public Guid? ProcessStepExecutionId { get; private set; }

    public Guid? EquipmentId { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public DateTime StartedAtLocal { get; private set; }

    public DateTime? EndedAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    public string DowntimeType { get; private set; } = null!;

    /// <summary>
    /// Chapter 3 4.5.4: stopped_minutes numeric(12,3) NOT NULL, CHECK >= 0.
    /// The raw time the equipment was halted.
    /// </summary>
    public decimal StoppedMinutes { get; private set; }

    /// <summary>
    /// Chapter 3 4.5.4: production_impact_minutes numeric(12,3) NOT NULL, CHECK >= 0.
    /// The time production output was actually lost. THIS IS A DIFFERENT QUANTITY
    /// and one may never stand in for the other: a twenty-minute mill stoppage
    /// absorbed by buffer slabs costs no production, while a three-minute caster
    /// pump stoppage can force a sequence rebuild and cost six hours.
    /// Neither value is ever derived from the other, or from the timestamps.
    /// </summary>
    public decimal ProductionImpactMinutes { get; private set; }

    public string? ReasonCode { get; private set; }

    public string? Description { get; private set; }

    private DowntimeEvent()
    {
    }

    public DowntimeEvent(
        DateTime startedAtUtc,
        string downtimeType,
        decimal stoppedMinutes,
        decimal productionImpactMinutes,
        bool isSynthetic,
        DateTime? endedAtUtc = null,
        Guid? materialUnitId = null,
        Guid? processStepExecutionId = null,
        Guid? equipmentId = null,
        string? reasonCode = null,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "Europe/Berlin",
        int plantUtcOffsetMinutes = 60)
    {
        if (string.IsNullOrWhiteSpace(downtimeType))
            throw new ArgumentException("Downtime type is required.", nameof(downtimeType));

        if (stoppedMinutes < 0m)
            throw new ArgumentOutOfRangeException(nameof(stoppedMinutes), "Stopped minutes cannot be negative.");

        if (productionImpactMinutes < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(productionImpactMinutes), "Production impact minutes cannot be negative.");

        // Deliberately NOT validated: production impact may exceed stopped minutes.
        // A three-minute caster pump trip can force a sequence rebuild costing six
        // hours of production. Constraining one by the other would encode a
        // relationship the plant does not have.

        StartedAtUtc = EnsureUtc(startedAtUtc);
        EndedAtUtc = endedAtUtc.HasValue ? EnsureUtc(endedAtUtc.Value) : null;

        if (EndedAtUtc.HasValue && EndedAtUtc.Value < StartedAtUtc)
            throw new InvalidOperationException("Downtime end cannot be before downtime start.");

        StartedAtLocal = DateTime.SpecifyKind(
            StartedAtUtc.AddMinutes(plantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        EndedAtLocal = EndedAtUtc.HasValue
            ? DateTime.SpecifyKind(EndedAtUtc.Value.AddMinutes(plantUtcOffsetMinutes), DateTimeKind.Unspecified)
            : null;

        PlantTimeZoneId = string.IsNullOrWhiteSpace(plantTimeZoneId)
            ? "Europe/Berlin"
            : plantTimeZoneId.Trim();

        PlantUtcOffsetMinutes = plantUtcOffsetMinutes;

        DowntimeType = downtimeType.Trim();
        StoppedMinutes = stoppedMinutes;
        ProductionImpactMinutes = productionImpactMinutes;
        MaterialUnitId = materialUnitId;
        ProcessStepExecutionId = processStepExecutionId;
        EquipmentId = equipmentId;
        ReasonCode = reasonCode?.Trim();
        Description = description?.Trim();
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}