using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Configuration;

public class IndustryTemplate : BaseEntity
{
    public string TemplateCode { get; private set; } = null!;

    public string TemplateName { get; private set; } = null!;

    public string IndustryName { get; private set; } = null!;
    // Examples: FlatSteel, RailSteel, Pharma, Tire, Paper, Aluminum, Food, Chemicals, Automotive

    public string? Description { get; private set; }

    public string Version { get; private set; } = "v1";

    public bool IsActive { get; private set; } = true;

    private IndustryTemplate()
    {
    }

    public IndustryTemplate(
        string templateCode,
        string templateName,
        string industryName,
        bool isSynthetic,
        string version = "v1",
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            throw new ArgumentException("Template code is required.", nameof(templateCode));

        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name is required.", nameof(templateName));

        if (string.IsNullOrWhiteSpace(industryName))
            throw new ArgumentException("Industry name is required.", nameof(industryName));

        TemplateCode = templateCode.Trim();
        TemplateName = templateName.Trim();
        IndustryName = industryName.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? "v1" : version.Trim();
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