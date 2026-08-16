using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Quality;

public class QualityEvent : BaseEntity
{
    public Guid MaterialUnitId { get; private set; }

    public Guid? DefectCatalogId { get; private set; }

    public DateTime EventAtUtc { get; private set; }

    public DateTime EventAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    public string EventType { get; private set; } = null!;
    // Defect, Downgrade, Reject, Hold, Rework, Claim

    public string? Severity { get; private set; }

    public string? Decision { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// T-044-R1. WHERE ON THE MATERIAL, AS RECORDED.
    ///
    /// Three separate facts, not one. The longitudinal pair is an EXTENT - a
    /// defect runs from one position to another - and reducing it to a midpoint
    /// would discard the length permanently. A renderer may derive a midpoint;
    /// this layer may not.
    ///
    /// Units are in the names because the source records them in different
    /// ones: metres along the material, millimetres across it. A unitless
    /// number here would be ambiguous the first time someone compared them.
    ///
    /// All three are nullable. An event with no recorded position - a
    /// disposition, for instance - keeps NULL. Zero is a position, and would
    /// place the defect at the very start of the material.
    /// </summary>
    public decimal? PositionStartM { get; private set; }

    public decimal? PositionEndM { get; private set; }

    public decimal? WidthPositionMm { get; private set; }

    private QualityEvent()
    {
    }

    public QualityEvent(
        Guid materialUnitId,
        string eventType,
        DateTime eventAtUtc,
        bool isSynthetic,
        Guid? defectCatalogId = null,
        string? severity = null,
        string? decision = null,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "Europe/Berlin",
        int plantUtcOffsetMinutes = 60)
    {
        if (materialUnitId == Guid.Empty)
            throw new ArgumentException("Material unit ID is required.", nameof(materialUnitId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));

        MaterialUnitId = materialUnitId;
        EventType = eventType.Trim();
        EventAtUtc = EnsureUtc(eventAtUtc);
        EventAtLocal = DateTime.SpecifyKind(
            EventAtUtc.AddMinutes(plantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        PlantTimeZoneId = string.IsNullOrWhiteSpace(plantTimeZoneId)
            ? "Europe/Berlin"
            : plantTimeZoneId.Trim();

        PlantUtcOffsetMinutes = plantUtcOffsetMinutes;
        DefectCatalogId = defectCatalogId;
        Severity = severity?.Trim();
        Decision = decision?.Trim();
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