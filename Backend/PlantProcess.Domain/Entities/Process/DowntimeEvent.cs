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

    public string? ReasonCode { get; private set; }

    public string? Description { get; private set; }

    private DowntimeEvent()
    {
    }

    public DowntimeEvent(
        DateTime startedAtUtc,
        string downtimeType,
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