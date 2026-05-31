
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface INarrativeProvider
{
    string ProviderKey { get; }

    Task<NarrativeResult> GenerateAsync(
        NarrativeRequest request,
        CancellationToken cancellationToken);
}
