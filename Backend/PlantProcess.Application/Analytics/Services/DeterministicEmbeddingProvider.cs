using System.Security.Cryptography;
using System.Text;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Application.Analytics.Services;

/// <summary>
/// Safe local embedding provider used until a real embedding model/provider is configured.
/// This is deterministic and suitable for proving the KB pipeline contract.
/// </summary>
public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    public string ProviderKey => "deterministic-local-v1";

    public Task<EmbeddingResult> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Embedding text is required.", nameof(request));

        var dimensions = Math.Clamp(request.Dimensions, 32, 1536);
        var values = new double[dimensions];

        var tokens = request.Text
            .ToLowerInvariant()
            .Split(
                new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt32(bytes, 0) % dimensions;
            var sign = (bytes[4] & 1) == 0 ? 1.0 : -1.0;
            values[index] += sign;
        }

        var norm = Math.Sqrt(values.Sum(x => x * x));
        if (norm > 0)
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = values[i] / norm;
        }

        return Task.FromResult(new EmbeddingResult(values, ProviderKey, "hashing-vectorizer"));
    }
}