using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Analytics;

public class DashboardDefinition : BaseEntity
{
    public Guid? UserId { get; private set; }

    public string DashboardCode { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string LayoutJson { get; private set; } = "{}";

    public bool IsDefault { get; private set; }

    public bool IsSystemTemplate { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<DashboardWidgetDefinition> Widgets { get; private set; } =
        new List<DashboardWidgetDefinition>();

    private DashboardDefinition()
    {
    }

    public DashboardDefinition(
        string dashboardCode,
        string name,
        bool isSynthetic,
        Guid? userId = null,
        string? description = null,
        string? layoutJson = null,
        bool isDefault = false,
        bool isSystemTemplate = false,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(dashboardCode))
            throw new ArgumentException("Dashboard code is required.", nameof(dashboardCode));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dashboard name is required.", nameof(name));

        DashboardCode = dashboardCode.Trim();
        Name = name.Trim();
        Description = NormalizeNullable(description);
        LayoutJson = NormalizeJson(layoutJson);
        IsDefault = isDefault;
        IsSystemTemplate = isSystemTemplate;
        IsActive = true;

        UserId = userId;
        IsSynthetic = isSynthetic;
        SourceSystem = NormalizeNullable(sourceSystem);
        SourceRecordId = NormalizeNullable(sourceRecordId);
    }

    public void Rename(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dashboard name is required.", nameof(name));

        Name = name.Trim();
        Description = NormalizeNullable(description);
        MarkAsUpdated();
    }

    public void UpdateLayout(string? layoutJson)
    {
        LayoutJson = NormalizeJson(layoutJson);
        MarkAsUpdated();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        MarkAsUpdated();
    }

    public void RemoveDefaultFlag()
    {
        IsDefault = false;
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

    private static string NormalizeJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "{}"
            : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}