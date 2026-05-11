using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Materials;

public class MaterialUnit : BaseEntity
{
    public string MaterialCode { get; private set; } = null!;

    public string MaterialUnitType { get; private set; } = null!;
    // Steel: Heat, Cast, Slab, Coil
    // Pharma: Batch, Lot
    // Tire: TireBatch, TireUnit
    // Paper: Roll, Reel
    // Automotive: Component, Assembly, VIN

    public string? ProductFamily { get; private set; }

    public string? GradeOrRecipe { get; private set; }

    public Guid SiteId { get; private set; }

    public DateTime? ProductionStartUtc { get; private set; }

    public DateTime? ProductionEndUtc { get; private set; }

    public DateTime? ProductionStartLocal { get; private set; }

    public DateTime? ProductionEndLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    private readonly List<MaterialAlias> _aliases = new();

    public IReadOnlyCollection<MaterialAlias> Aliases => _aliases.AsReadOnly();

    private MaterialUnit()
    {
    }

    public MaterialUnit(
        string materialCode,
        string materialUnitType,
        Guid siteId,
        string? productFamily,
        string? gradeOrRecipe,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(materialCode))
            throw new ArgumentException("Material code is required.", nameof(materialCode));

        if (string.IsNullOrWhiteSpace(materialUnitType))
            throw new ArgumentException("Material unit type is required.", nameof(materialUnitType));

        if (siteId == Guid.Empty)
            throw new ArgumentException("Site ID is required.", nameof(siteId));

        MaterialCode = materialCode.Trim();
        MaterialUnitType = materialUnitType.Trim();
        SiteId = siteId;
        ProductFamily = productFamily?.Trim();
        GradeOrRecipe = gradeOrRecipe?.Trim();
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void SetProductionWindow(
        DateTime startUtc,
        DateTime? endUtc,
        TimeSpan? plantUtcOffset = null,
        string plantTimeZoneId = "Europe/Berlin")
    {
        var normalizedStartUtc = EnsureUtc(startUtc);
        DateTime? normalizedEndUtc = endUtc.HasValue
            ? EnsureUtc(endUtc.Value)
            : null;

        if (normalizedEndUtc.HasValue && normalizedEndUtc.Value < normalizedStartUtc)
            throw new InvalidOperationException("Production end cannot be before production start.");

        ProductionStartUtc = normalizedStartUtc;
        ProductionEndUtc = normalizedEndUtc;

        var offset = plantUtcOffset ?? TimeSpan.FromHours(1);

        PlantUtcOffsetMinutes = (int)offset.TotalMinutes;
        PlantTimeZoneId = string.IsNullOrWhiteSpace(plantTimeZoneId)
            ? "Europe/Berlin"
            : plantTimeZoneId.Trim();

        ProductionStartLocal = ToLocal(normalizedStartUtc, offset);

        ProductionEndLocal = normalizedEndUtc.HasValue
             ? ToLocal(normalizedEndUtc.Value, offset)
             : null;

        MarkAsUpdated();
    }

    public void AddAlias(string aliasCode, string sourceSystem, string? aliasType = null)
    {
        if (string.IsNullOrWhiteSpace(aliasCode))
            throw new ArgumentException("Alias code is required.", nameof(aliasCode));

        if (string.IsNullOrWhiteSpace(sourceSystem))
            throw new ArgumentException("Source system is required.", nameof(sourceSystem));

        var normalizedAliasCode = aliasCode.Trim();
        var normalizedSourceSystem = sourceSystem.Trim();

        if (_aliases.Any(x =>
                x.AliasCode == normalizedAliasCode &&
                x.SourceSystem == normalizedSourceSystem))
        {
            return;
        }

        _aliases.Add(new MaterialAlias(
            Id,
            normalizedAliasCode,
            normalizedSourceSystem,
            aliasType,
            IsSynthetic));

        MarkAsUpdated();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime ToLocal(DateTime utcTime, TimeSpan offset)
    {
        return DateTime.SpecifyKind(
            utcTime.Add(offset),
            DateTimeKind.Unspecified);
    }
}