using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Methods;

public sealed record LassoResult(double[] Coefficients, int[] SelectedFeatures, double Lambda, int Iterations);

/// <summary>L1-regularized regression via cyclic coordinate descent on standardized features.</summary>
public static class Lasso
{
    public static LassoResult Fit(double[][] x, double[] y, double lambda, int maxIter = 2000, double tol = 1e-8)
    {
        int n = y.Length;
        int p = x.Length == 0 ? 0 : x[0].Length;
        var col = new double[p][];
        for (int j = 0; j < p; j++)
        {
            var cj = new double[n]; double m = 0;
            for (int i = 0; i < n; i++) { cj[i] = x[i][j]; m += cj[i]; }
            m /= n;
            double v = 0; for (int i = 0; i < n; i++) { double d = cj[i] - m; v += d * d; }
            v = Math.Sqrt(v / Math.Max(1, n - 1)); if (v < 1e-12) v = 1.0;
            for (int i = 0; i < n; i++) cj[i] = (cj[i] - m) / v;
            col[j] = cj;
        }
        double my = 0; for (int i = 0; i < n; i++) my += y[i]; my /= n;
        var r = new double[n]; for (int i = 0; i < n; i++) r[i] = y[i] - my;

        var beta = new double[p];
        int iter = 0;
        for (; iter < maxIter; iter++)
        {
            double maxChange = 0;
            for (int j = 0; j < p; j++)
            {
                var cj = col[j];
                double rho = 0; for (int i = 0; i < n; i++) rho += cj[i] * (r[i] + beta[j] * cj[i]);
                rho /= n;
                double nb = SoftThreshold(rho, lambda);
                double change = nb - beta[j];
                if (change != 0)
                {
                    for (int i = 0; i < n; i++) r[i] -= change * cj[i];
                    beta[j] = nb;
                    maxChange = Math.Max(maxChange, Math.Abs(change));
                }
            }
            if (maxChange < tol) { iter++; break; }
        }
        var selected = Enumerable.Range(0, p).Where(j => Math.Abs(beta[j]) > 1e-6).ToArray();
        return new LassoResult(beta, selected, lambda, iter);
    }

    private static double SoftThreshold(double z, double g) => z > g ? z - g : (z < -g ? z + g : 0.0);
}