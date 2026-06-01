using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Numerics;

/// <summary>Exact, explainable statistical primitives. Sample (n-1) variance throughout.</summary>
public static class Stats
{
    public static double Mean(IReadOnlyList<double> x)
    {
        if (x.Count == 0) throw new ArgumentException("Mean requires a non-empty sequence.");
        double s = 0; for (int i = 0; i < x.Count; i++) s += x[i];
        return s / x.Count;
    }

    public static double Sum(IReadOnlyList<double> x) { double s = 0; for (int i = 0; i < x.Count; i++) s += x[i]; return s; }

    public static double SampleVariance(IReadOnlyList<double> x)
    {
        if (x.Count < 2) return 0.0;
        double m = Mean(x), s = 0;
        for (int i = 0; i < x.Count; i++) { double d = x[i] - m; s += d * d; }
        return s / (x.Count - 1);
    }

    public static double SampleStdDev(IReadOnlyList<double> x) => Math.Sqrt(SampleVariance(x));

    public static double Median(IReadOnlyList<double> x)
    {
        if (x.Count == 0) throw new ArgumentException("Median requires a non-empty sequence.");
        var s = x.OrderBy(v => v).ToArray();
        int n = s.Length;
        return (n % 2 == 1) ? s[n / 2] : 0.5 * (s[n / 2 - 1] + s[n / 2]);
    }

    public static double Quantile(IReadOnlyList<double> x, double q)
    {
        if (x.Count == 0) throw new ArgumentException("Quantile requires a non-empty sequence.");
        if (q <= 0) return x.Min();
        if (q >= 1) return x.Max();
        var s = x.OrderBy(v => v).ToArray();
        double pos = q * (s.Length - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        return s[lo] + (pos - lo) * (s[hi] - s[lo]);
    }

    /// <summary>Average-rank assignment for ties (1-based ranks).</summary>
    public static double[] Ranks(IReadOnlyList<double> x)
    {
        int n = x.Count;
        var idx = Enumerable.Range(0, n).OrderBy(i => x[i]).ToArray();
        var ranks = new double[n];
        int i = 0;
        while (i < n)
        {
            int j = i;
            while (j + 1 < n && x[idx[j + 1]] == x[idx[i]]) j++;
            double avg = (i + j) / 2.0 + 1.0;
            for (int k = i; k <= j; k++) ranks[idx[k]] = avg;
            i = j + 1;
        }
        return ranks;
    }

    public static double Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count) throw new ArgumentException("Pearson requires equal-length sequences.");
        int n = x.Count;
        if (n < 2) return double.NaN;
        double mx = Mean(x), my = Mean(y), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++) { double dx = x[i] - mx, dy = y[i] - my; sxy += dx * dy; sxx += dx * dx; syy += dy * dy; }
        if (sxx == 0 || syy == 0) return double.NaN;
        return sxy / Math.Sqrt(sxx * syy);
    }

    public static double Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y) => Pearson(Ranks(x), Ranks(y));

    public static double PointBiserial(IReadOnlyList<int> binary, IReadOnlyList<double> y)
    {
        if (binary.Count != y.Count) throw new ArgumentException("Point-biserial requires equal-length sequences.");
        var bd = new double[binary.Count];
        for (int i = 0; i < binary.Count; i++) bd[i] = binary[i];
        return Pearson(bd, y);
    }

    /// <summary>Normal CDF via erf (Abramowitz and Stegun 7.1.26).</summary>
    public static double NormalCdf(double z) => 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));

    public static double Erf(double x)
    {
        double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
        double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return Math.Sign(x) * y;
    }

    /// <summary>Two-sided p-value for a correlation coefficient via the Fisher z-transform.</summary>
    public static double CorrelationPValue(double r, int n)
    {
        if (n < 4 || double.IsNaN(r)) return double.NaN;
        double rr = Math.Max(-0.999999, Math.Min(0.999999, r));
        double z = 0.5 * Math.Log((1 + rr) / (1 - rr)) * Math.Sqrt(n - 3);
        double p = 2.0 * (1.0 - NormalCdf(Math.Abs(z)));
        return Math.Max(0.0, Math.Min(1.0, p));
    }
}