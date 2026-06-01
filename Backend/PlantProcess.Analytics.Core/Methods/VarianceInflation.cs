using System.Collections.Generic;
using PlantProcess.Analytics.Core.Numerics;

namespace PlantProcess.Analytics.Core.Methods;

public sealed record VifResult(IReadOnlyDictionary<int, double> Vif, int[] Flagged, double Threshold);

/// <summary>Variance inflation factor per feature: VIF_j = 1 / (1 - R^2_j) from regressing feature j on the others.</summary>
public static class VarianceInflation
{
    public static VifResult Compute(double[][] x, double threshold = 5.0)
    {
        int n = x.Length;
        int p = x.Length == 0 ? 0 : x[0].Length;
        var vif = new Dictionary<int, double>();
        var flagged = new List<int>();
        for (int j = 0; j < p; j++)
        {
            double r2 = 0;
            if (p > 1)
            {
                var others = new double[n][];
                var target = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var row = new double[p - 1]; int c = 0;
                    for (int k = 0; k < p; k++) { if (k == j) continue; row[c++] = x[i][k]; }
                    others[i] = row; target[i] = x[i][j];
                }
                r2 = LinearAlgebra.OlsFit(others, target)?.R2 ?? 0;
            }
            double v = r2 >= 0.999999 ? double.PositiveInfinity : 1.0 / (1.0 - r2);
            vif[j] = v;
            if (v >= threshold) flagged.Add(j);
        }
        return new VifResult(vif, flagged.ToArray(), threshold);
    }
}