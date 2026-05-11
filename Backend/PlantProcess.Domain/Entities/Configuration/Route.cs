using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Configuration;

public class Route : BaseEntity
{
    public Guid IndustryTemplateId { get; private set; }

    public string RouteCode { get; private set; } = null!;

    public string RouteName { get; private set; } = null!;

    public string? ProductFamily { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    private Route()
    {
    }

    public Route(
        Guid industryTemplateId,
        string routeCode,
        string routeName,
        bool isSynthetic,
        string? productFamily = null,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (industryTemplateId == Guid.Empty)
            throw new ArgumentException("Industry template ID is required.", nameof(industryTemplateId));

        if (string.IsNullOrWhiteSpace(routeCode))
            throw new ArgumentException("Route code is required.", nameof(routeCode));

        if (string.IsNullOrWhiteSpace(routeName))
            throw new ArgumentException("Route name is required.", nameof(routeName));

        IndustryTemplateId = industryTemplateId;
        RouteCode = routeCode.Trim();
        RouteName = routeName.Trim();
        ProductFamily = productFamily?.Trim();
        Description = description?.Trim();

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
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