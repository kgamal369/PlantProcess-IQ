using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

public class ProcessEvent : BaseEntity
{
    public Guid? MaterialUnitId { get; private set; }

    public Guid? ProcessStepExecutionId { get; private set; }

    public Guid? EquipmentId { get; private set; }

    public string EventType { get; private set; } = null!;
    // Breakout, CasterCrack, PowderChange, MouldChange, SpeedDrop

    public DateTime EventAtUtc { get; private set; }

    public DateTime EventAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    public string? EventValue { get; private set; }

    public string? Description { get; private set; }

    private ProcessEvent()
    {
    }

    public ProcessEvent(
        string eventType,
        DateTime eventAtUtc,
        bool isSynthetic,
        Guid? materialUnitId = null,
        Guid? processStepExecutionId = null,
        Guid? equipmentId = null,
        string? eventValue = null,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "Europe/Berlin",
        int plantUtcOffsetMinutes = 60)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));

        EventType = eventType.Trim();
        EventAtUtc = EnsureUtc(eventAtUtc);
        EventAtLocal = DateTime.SpecifyKind(
            EventAtUtc.AddMinutes(plantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        PlantTimeZoneId = string.IsNullOrWhiteSpace(plantTimeZoneId)
            ? "Europe/Berlin"
            : plantTimeZoneId.Trim();

        PlantUtcOffsetMinutes = plantUtcOffsetMinutes;

        MaterialUnitId = materialUnitId;
        ProcessStepExecutionId = processStepExecutionId;
        EquipmentId = equipmentId;
        EventValue = eventValue?.Trim();
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