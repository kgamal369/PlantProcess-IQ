using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface IEmbeddingProvider
{
    string ProviderKey { get; }

    Task<EmbeddingResult> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken);
}