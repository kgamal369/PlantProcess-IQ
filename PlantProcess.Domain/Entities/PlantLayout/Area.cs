using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.PlantLayout;

public class Area : BaseEntity
{
    public Guid SiteId { get; private set; }

    public Guid? ParentAreaId { get; private set; }

    public string AreaCode { get; private set; } = null!;

    public string AreaName { get; private set; } = null!;

    public string? AreaType { get; private set; }
    // Examples:
    // Building, Department, ProductionArea, LineArea, Zone, Cell, Room, Lab, Warehouse

    public int? SortOrder { get; private set; }

    private Area()
    {
    }

    public Area(
        Guid siteId,
        string areaCode,
        string areaName,
        bool isSynthetic,
        Guid? parentAreaId = null,
        string? areaType = null,
        int? sortOrder = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (siteId == Guid.Empty)
            throw new ArgumentException("Site ID is required.", nameof(siteId));

        if (string.IsNullOrWhiteSpace(areaCode))
            throw new ArgumentException("Area code is required.", nameof(areaCode));

        if (string.IsNullOrWhiteSpace(areaName))
            throw new ArgumentException("Area name is required.", nameof(areaName));

        SiteId = siteId;
        ParentAreaId = parentAreaId;
        AreaCode = areaCode.Trim();
        AreaName = areaName.Trim();
        AreaType = areaType?.Trim();
        SortOrder = sortOrder;

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void MoveToParent(Guid? parentAreaId)
    {
        if (parentAreaId == Id)
            throw new InvalidOperationException("Area cannot be its own parent.");

        ParentAreaId = parentAreaId;
        MarkAsUpdated();
    }
}