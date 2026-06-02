namespace PlantProcess.Application.Assistant;

/// <summary>Pluggable embedding. The default is deterministic; swap in your model's embeddings in Infrastructure.</summary>
public interface IEmbedder
{
    IReadOnlyList<float> Embed(string text);
}