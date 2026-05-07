using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.PlantLayout;

public class Equipment : BaseEntity
{
    public Guid SiteId { get; private set; }

    public Guid? AreaId { get; private set; }

    public Guid? ParentEquipmentId { get; private set; }

    public string EquipmentCode { get; private set; } = null!;

    public string EquipmentName { get; private set; } = null!;

    public string EquipmentType { get; private set; } = null!;
    // Examples:
    // ProductionLine, Machine, Furnace, Caster, Stand, Mould, Reactor, Press,
    // Cutter, InspectionDevice, SensorGroup, Tooling, Station, PackagingMachine

    public string? Manufacturer { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int? SortOrder { get; private set; }

    private Equipment()
    {
    }

    public Equipment(
        Guid siteId,
        string equipmentCode,
        string equipmentName,
        string equipmentType,
        bool isSynthetic,
        Guid? areaId = null,
        Guid? parentEquipmentId = null,
        string? manufacturer = null,
        int? sortOrder = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (siteId == Guid.Empty)
            throw new ArgumentException("Site ID is required.", nameof(siteId));

        if (string.IsNullOrWhiteSpace(equipmentCode))
            throw new ArgumentException("Equipment code is required.", nameof(equipmentCode));

        if (string.IsNullOrWhiteSpace(equipmentName))
            throw new ArgumentException("Equipment name is required.", nameof(equipmentName));

        if (string.IsNullOrWhiteSpace(equipmentType))
            throw new ArgumentException("Equipment type is required.", nameof(equipmentType));

        SiteId = siteId;
        AreaId = areaId;
        ParentEquipmentId = parentEquipmentId;
        EquipmentCode = equipmentCode.Trim();
        EquipmentName = equipmentName.Trim();
        EquipmentType = equipmentType.Trim();
        Manufacturer = manufacturer?.Trim();
        SortOrder = sortOrder;

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void MoveToParent(Guid? parentEquipmentId)
    {
        if (parentEquipmentId == Id)
            throw new InvalidOperationException("Equipment cannot be its own parent.");

        ParentEquipmentId = parentEquipmentId;
        MarkAsUpdated();
    }

    public void MoveToArea(Guid? areaId)
    {
        AreaId = areaId;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }
}