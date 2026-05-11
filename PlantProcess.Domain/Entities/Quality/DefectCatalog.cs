using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Quality;

public class DefectCatalog : BaseEntity
{
    private readonly List<QualityEvent> _qualityEvents = new();

    public string DefectCode { get; private set; } = null!;

    public string DefectName { get; private set; } = null!;

    public string? DefectCategory { get; private set; }

    public string? IndustryTemplate { get; private set; }

    public IReadOnlyCollection<QualityEvent> QualityEvents => _qualityEvents.AsReadOnly();

    private DefectCatalog()
    {
    }

    public DefectCatalog(
        string defectCode,
        string defectName,
        string? defectCategory,
        string? industryTemplate,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(defectCode))
            throw new ArgumentException("Defect code is required.", nameof(defectCode));

        if (string.IsNullOrWhiteSpace(defectName))
            throw new ArgumentException("Defect name is required.", nameof(defectName));

        DefectCode = defectCode.Trim();
        DefectName = defectName.Trim();
        DefectCategory = defectCategory?.Trim();
        IndustryTemplate = industryTemplate?.Trim();
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}
