using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Methods;

public static class CategoricalAssociation
{
    /// <summary>Cramer's V from a chi-square contingency table (in [0,1]).</summary>
    public static double CramersV<T>(IReadOnlyList<T> x, IReadOnlyList<T> y) where T : notnull
    {
        if (x.Count != y.Count) throw new ArgumentException("Cramer's V requires equal-length sequences.");
        int n = x.Count;
        var rows = x.Distinct().ToList();
        var cols = y.Distinct().ToList();
        int r = rows.Count, c = cols.Count;
        if (r < 2 || c < 2) return 0.0;
        var ri = rows.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);
        var ci = cols.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);
        var obs = new double[r, c]; var rs = new double[r]; var cs = new double[c];
        for (int i = 0; i < n; i++) { int a = ri[x[i]], b = ci[y[i]]; obs[a, b]++; rs[a]++; cs[b]++; }
        double chi = 0;
        for (int i = 0; i < r; i++)
            for (int j = 0; j < c; j++)
            {
                double e = rs[i] * cs[j] / n;
                if (e > 0) { double d = obs[i, j] - e; chi += d * d / e; }
            }
        double v = Math.Sqrt(chi / (n * Math.Min(r - 1, c - 1)));
        return Math.Max(0.0, Math.Min(1.0, v));
    }
}