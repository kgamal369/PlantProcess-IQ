using System.Collections.Generic;
using PlantProcess.Analytics.Core.Numerics;

namespace PlantProcess.Analytics.Core.Methods;

/// <summary>Normalized mutual information (NMI in [0,1]) for numeric (binned) and categorical pairs.</summary>
public static class MutualInformation
{
    public static int SuggestBins(int n) => Math.Max(2, Math.Min(12, (int)Math.Round(Math.Sqrt(n))));

    public static double NormalizedNumeric(IReadOnlyList<double> x, IReadOnlyList<double> y, int? bins = null)
    {
        if (x.Count != y.Count) throw new ArgumentException("NMI requires equal-length sequences.");
        if (x.Count < 4) return double.NaN;
        int b = bins ?? SuggestBins(x.Count);
        return NormalizedCategorical(Discretize(x, b), Discretize(y, b));
    }

    public static double NormalizedCategorical<T>(IReadOnlyList<T> x, IReadOnlyList<T> y) where T : notnull
    {
        if (x.Count != y.Count) throw new ArgumentException("NMI requires equal-length sequences.");
        int n = x.Count;
        var joint = new Dictionary<(T, T), int>();
        var mx = new Dictionary<T, int>();
        var my = new Dictionary<T, int>();
        for (int i = 0; i < n; i++)
        {
            var key = (x[i], y[i]);
            joint[key] = joint.TryGetValue(key, out var c) ? c + 1 : 1;
            mx[x[i]] = mx.TryGetValue(x[i], out var cx) ? cx + 1 : 1;
            my[y[i]] = my.TryGetValue(y[i], out var cy) ? cy + 1 : 1;
        }
        double mi = 0;
        foreach (var kv in joint)
        {
            double pxy = (double)kv.Value / n;
            double px = (double)mx[kv.Key.Item1] / n;
            double py = (double)my[kv.Key.Item2] / n;
            mi += pxy * Math.Log(pxy / (px * py));
        }
        double hx = Entropy(mx.Values, n), hy = Entropy(my.Values, n);
        double denom = Math.Sqrt(hx * hy);
        if (denom <= 1e-12) return 0.0;
        return Math.Max(0.0, Math.Min(1.0, mi / denom));
    }

    private static double Entropy(IEnumerable<int> counts, int n)
    {
        double h = 0;
        foreach (var c in counts) { if (c <= 0) continue; double p = (double)c / n; h -= p * Math.Log(p); }
        return h;
    }

    public static int[] Discretize(IReadOnlyList<double> x, int bins)
    {
        var edges = new double[bins + 1];
        for (int i = 0; i <= bins; i++) edges[i] = Stats.Quantile(x, (double)i / bins);
        var res = new int[x.Count];
        for (int i = 0; i < x.Count; i++)
        {
            int bidx = bins - 1;
            for (int e = 1; e < bins; e++) { if (x[i] <= edges[e]) { bidx = e - 1; break; } }
            res[i] = bidx;
        }
        return res;
    }
}