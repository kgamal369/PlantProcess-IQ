using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlantProcess.ML.Runtime;

/// <summary>What the job is allowed to consume. Declared here, enforced by this side.</summary>
public sealed record ResourceBudget(
    [property: JsonPropertyName("max_wall_clock_seconds")] double MaxWallClockSeconds,
    [property: JsonPropertyName("max_memory_mb")] int MaxMemoryMb = 0,
    [property: JsonPropertyName("gpu_required")] bool GpuRequired = false);

/// <summary>
/// A sealed input artifact. The Python runtime never reads a database; it reads these.
/// A physical schema change is absorbed by the snapshot materialiser, not by a model.
/// </summary>
public sealed record ArtifactRef(
    [property: JsonPropertyName("artifact_id")] string ArtifactId,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("content_hash")] string ContentHash,
    [property: JsonPropertyName("artifact_format")] string ArtifactFormat,
    [property: JsonPropertyName("byte_size")] long ByteSize = 0);

/// <summary>
/// Identity, context and inputs for one ML execution. Carries no connection string,
/// no table name and no SQL.
/// </summary>
public sealed record JobSpec
{
    [JsonPropertyName("protocol")] public string Protocol { get; init; } = MlJobProtocol.Id;
    [JsonPropertyName("job_id")] public string JobId { get; init; } = string.Empty;
    [JsonPropertyName("tenant_id")] public string TenantId { get; init; } = string.Empty;
    [JsonPropertyName("site_id")] public string SiteId { get; init; } = string.Empty;
    [JsonPropertyName("model_family")] public string ModelFamily { get; init; } = string.Empty;
    [JsonPropertyName("inputs")] public IReadOnlyList<ArtifactRef> Inputs { get; init; } = Array.Empty<ArtifactRef>();
    [JsonPropertyName("output_directory")] public string OutputDirectory { get; init; } = string.Empty;
    [JsonPropertyName("seed")] public int Seed { get; init; }
    [JsonPropertyName("code_identity")] public string CodeIdentity { get; init; } = string.Empty;
    [JsonPropertyName("resources")] public ResourceBudget Resources { get; init; } = new(0);

    /// <summary>Present once the canonical Semantic Contract Manifest exists. Optional today.</summary>
    [JsonPropertyName("semantic_manifest_id")] public string? SemanticManifestId { get; init; }

    [JsonPropertyName("checkpoint_directory")] public string? CheckpointDirectory { get; init; }
    [JsonPropertyName("cancellation_file")] public string? CancellationFile { get; init; }
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; }
        = new Dictionary<string, JsonElement>();

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static JobSpec FromJson(string text)
    {
        JobSpec? spec;
        try
        {
            spec = JsonSerializer.Deserialize<JobSpec>(text, Json);
        }
        catch (JsonException exception)
        {
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                $"The job spec is not valid JSON: {exception.Message}");
        }

        if (spec is null)
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec, "The job spec is empty.");

        if (string.IsNullOrWhiteSpace(spec.Protocol))
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                "The job spec declares no protocol. It was not interpreted.");

        if (spec.Protocol != MlJobProtocol.Id)
            throw new MlProtocolException(MlRefusalCode.ProtocolVersionMismatch,
                $"Job spec declares protocol '{spec.Protocol}'; this runtime speaks "
                + $"'{MlJobProtocol.Id}'. The payload was not interpreted.");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(spec.JobId)) missing.Add("job_id");
        if (string.IsNullOrWhiteSpace(spec.TenantId)) missing.Add("tenant_id");
        if (string.IsNullOrWhiteSpace(spec.SiteId)) missing.Add("site_id");
        if (string.IsNullOrWhiteSpace(spec.ModelFamily)) missing.Add("model_family");
        if (string.IsNullOrWhiteSpace(spec.OutputDirectory)) missing.Add("output_directory");
        if (string.IsNullOrWhiteSpace(spec.CodeIdentity)) missing.Add("code_identity");
        if (missing.Count > 0)
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                $"The job spec is missing required fields: {string.Join(", ", missing.OrderBy(m => m))}.");

        if (spec.Resources is null || spec.Resources.MaxWallClockSeconds <= 0)
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                "The job spec declares no wall-clock budget.");

        return spec;
    }
}
