using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// Deterministic, dependency-free default embedder: hashed bag-of-words into a fixed vector, L2-normalised.
/// Good enough for tenant/permission-scoped retrieval structure; replace with your model's embeddings.
/// </summary>
public sealed class HashingEmbedder : IEmbedder
{
    private const int Dim = 256;
    private static readonly Regex Word = new("[a-z0-9]+", RegexOptions.Compiled);

    public IReadOnlyList<float> Embed(string text)
    {
        var v = new float[Dim];
        if (string.IsNullOrWhiteSpace(text)) return v;
        foreach (Match m in Word.Matches(text.ToLowerInvariant()))
        {
            var h = (uint)m.Value.GetHashCode();
            v[h % Dim] += 1f;
        }
        var norm = (float)Math.Sqrt(v.Sum(x => (double)x * x));
        if (norm > 0) for (var i = 0; i < Dim; i++) v[i] /= norm;
        return v;
    }
}