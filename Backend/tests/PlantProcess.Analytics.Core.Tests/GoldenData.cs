using PlantProcess.Analytics.Core.Common;
namespace PlantProcess.Analytics.Core.Tests;
/// <summary>Deterministic synthetic data with planted true signals and decoys (reproducible on any machine).</summary>
internal static class GoldenData
{
public static (double[] x, double[] y) MonotonicStrong(int n = 200, ulong seed = 1001UL)
{
var rng = new DeterministicRandom(seed);
var x = new double[n]; var y = new double[n];
for (int i = 0; i < n; i++) { x[i] = rng.NextUniform(0, 10); y[i] = Math.Exp(x[i] * 0.3) + rng.NextGaussian(0, 0.05); }
return (x, y);
}
public static (double[] x, double[] y) NonlinearSymmetric(int n = 600, ulong seed = 2002UL)
{
    var rng = new DeterministicRandom(seed);
    var x = new double[n]; var y = new double[n];
    for (int i = 0; i < n; i++) { x[i] = rng.NextUniform(-1, 1); y[i] = x[i] * x[i]; } // functional, symmetric -> ~0 Spearman, high MI
    return (x, y);
}

public static (double[] a, double[] b) Independent(int n = 600, ulong seed = 3003UL)
{
    var rng = new DeterministicRandom(seed);
    var a = new double[n]; var b = new double[n];
    for (int i = 0; i < n; i++) { a[i] = rng.NextGaussian(); b[i] = rng.NextGaussian(); }
    return (a, b);
}

/// <summary>Design matrix with informative cols 0,1 and noise cols 2..; y = 2*x0 - 3*x1 + small noise.</summary>
public static (double[][] x, double[] y, int informativeCount) LassoDesign(int n = 300, int noiseCols = 6, ulong seed = 4004UL)
{
    var rng = new DeterministicRandom(seed);
    int p = 2 + noiseCols;
    var x = new double[n][]; var y = new double[n];
    for (int i = 0; i < n; i++)
    {
        var row = new double[p];
        for (int j = 0; j < p; j++) row[j] = rng.NextGaussian();
        y[i] = 2.0 * row[0] - 3.0 * row[1] + rng.NextGaussian(0, 0.05);
        x[i] = row;
    }
    return (x, y, 2);
}

/// <summary>Cols: 0 and 2 are collinear (x2 = x0 + tiny noise); col 1 is independent.</summary>
public static double[][] CollinearDesign(int n = 200, ulong seed = 5005UL)
{
    var rng = new DeterministicRandom(seed);
    var x = new double[n][];
    for (int i = 0; i < n; i++)
    {
        double x0 = rng.NextGaussian();
        double x1 = rng.NextGaussian();
        double x2 = x0 + rng.NextGaussian(0, 0.01);
        x[i] = new[] { x0, x1, x2 };
    }
    return x;
}
}