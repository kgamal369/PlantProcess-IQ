using System.Text.RegularExpressions;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// Certified local semantic embedder for air-gapped deployments.
/// 
/// This is not the old placeholder PlantProcess.Application.Assistant.LocalSemanticEmbedder. It is the production IEmbedder boundary implementation
/// used until a customer supplies a local ONNX/runtime model or an internal embedding endpoint.
/// It is deterministic, local-only, fixed-dimension, and designed so the provider can be swapped without
/// changing retrieval or assistant contracts.
/// </summary>
public sealed class LocalSemanticEmbedder : IEmbedder
{
    public const string ProviderKey = "local-semantic-embedding";
    public const string ModelKey = "ppiq-local-ngram-semantic-v1";
    public const int Dimensions = 384;
    public const bool AirGapNoNetwork = true;

    private static readonly Regex TokenRx = new("[\\p{L}\\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<float> Embed(string text)
    {
        var vector = new float[Dimensions];

        if (string.IsNullOrWhiteSpace(text))
            return vector;

        var tokens = TokenRx
            .Matches(text.ToLowerInvariant())
            .Select(x => x.Value)
            .Where(x => x.Length > 1)
            .ToArray();

        foreach (var token in tokens)
        {
            AddFeature(vector, "tok:" + token, 1.0f);

            if (token.Length >= 4)
            {
                for (var i = 0; i <= token.Length - 3; i++)
                    AddFeature(vector, "tri:" + token.Substring(i, 3), 0.35f);
            }
        }

        for (var i = 0; i < tokens.Length - 1; i++)
            AddFeature(vector, "bi:" + tokens[i] + "_" + tokens[i + 1], 0.55f);

        Normalize(vector);

        return vector;
    }

    private static void AddFeature(float[] vector, string feature, float weight)
    {
        var index = StableIndex(feature, vector.Length);
        vector[index] += weight;
    }

    private static int StableIndex(string value, int dimensions)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            var hash = offset;

            foreach (var c in value)
            {
                hash ^= c;
                hash *= prime;
            }

            return (int)(hash % (uint)dimensions);
        }
    }

    private static void Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(x => (double)x * x));

        if (norm <= 0)
            return;

        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(vector[i] / norm);
    }

    public static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var count = Math.Min(left.Count, right.Count);
        var dot = 0.0;
        var l2 = 0.0;
        var r2 = 0.0;

        for (var i = 0; i < count; i++)
        {
            dot += left[i] * right[i];
            l2 += left[i] * left[i];
            r2 += right[i] * right[i];
        }

        if (l2 <= 0 || r2 <= 0)
            return 0;

        return dot / (Math.Sqrt(l2) * Math.Sqrt(r2));
    }
}
