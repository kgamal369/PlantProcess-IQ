namespace PlantProcess.Application.Assistant;

/// <summary>
/// Pluggable embedding model boundary for the production assistant.
/// Implementations may be local ONNX, internal HTTP embedding endpoint, or certified air-gapped local model.
/// The production contract is: deterministic vector shape, no tenant leakage, provider/model metadata documented,
/// and zero external network access when configured for air-gapped mode.
/// </summary>
public interface IEmbedder
{
    IReadOnlyList<float> Embed(string text);
}