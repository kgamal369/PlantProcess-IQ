namespace PlantProcess.Application.Analytics.Suggestions;

/// <summary>
/// Confidence is derived from data quality, sample size and stability and is monotonic in each.
/// Pure and bounded to [0,1].
/// </summary>
public static class SuggestionConfidence
{
    public static double Compute(double dataQuality, int sampleSize, double stability)
    {
        var dq = Clamp01(dataQuality);
        var st = Clamp01(stability);
        var sampleFactor = sampleSize <= 0 ? 0.0 : Math.Min(1.0, Math.Log10(sampleSize + 1) / 2.0); // ~1.0 at n>=99
        var raw = dq * st * sampleFactor;
        return Math.Round(Clamp01(raw), 4, MidpointRounding.AwayFromZero);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}