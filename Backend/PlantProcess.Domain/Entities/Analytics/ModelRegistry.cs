using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Analytics;

/// <summary>
/// Registers risk scoring/model versions used by PlantProcess IQ.
///
/// Phase H starts with transparent rule-based scoring. Future versions can point to
/// ML.NET, Python, ONNX, or external model artifacts while keeping RiskScore.ModelVersion stable.
/// </summary>
public class ModelRegistry : BaseEntity
{
    public string ModelCode { get; private set; } = null!;
    public string ModelName { get; private set; } = null!;
    public string ModelType { get; private set; } = null!; // RuleBased, MLNet, Python, ONNX
    public string ModelVersion { get; private set; } = null!;
    public string RiskType { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? ArtifactUri { get; private set; }
    public string? TrainingDataSummaryJson { get; private set; }
    public string? MetricsJson { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime RegisteredAtUtc { get; private set; }

    private ModelRegistry()
    {
    }

    public ModelRegistry(
        string modelCode,
        string modelName,
        string modelType,
        string modelVersion,
        string riskType,
        bool isSynthetic,
        string? description = null,
        string? artifactUri = null,
        string? trainingDataSummaryJson = null,
        string? metricsJson = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(modelCode)) throw new ArgumentException("Model code is required.", nameof(modelCode));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.", nameof(modelName));
        if (string.IsNullOrWhiteSpace(modelType)) throw new ArgumentException("Model type is required.", nameof(modelType));
        if (string.IsNullOrWhiteSpace(modelVersion)) throw new ArgumentException("Model version is required.", nameof(modelVersion));
        if (string.IsNullOrWhiteSpace(riskType)) throw new ArgumentException("Risk type is required.", nameof(riskType));

        ModelCode = modelCode.Trim();
        ModelName = modelName.Trim();
        ModelType = modelType.Trim();
        ModelVersion = modelVersion.Trim();
        RiskType = riskType.Trim();
        Description = description?.Trim();
        ArtifactUri = artifactUri?.Trim();
        TrainingDataSummaryJson = trainingDataSummaryJson;
        MetricsJson = metricsJson;
        RegisteredAtUtc = DateTime.UtcNow;
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
