namespace PlantProcess.Analytics.Core.Numerics;

public static class LinearAlgebra
{
    /// <summary>Solves A x = b for square A via Gaussian elimination with partial pivoting. Null if singular.</summary>
    public static double[]? Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var x = (double[])b.Clone();
        for (int col = 0; col < n; col++)
        {
            int piv = col; double best = Math.Abs(m[col, col]);
            for (int r = col + 1; r < n; r++) { double v = Math.Abs(m[r, col]); if (v > best) { best = v; piv = r; } }
            if (best < 1e-12) return null;
            if (piv != col) { for (int c = 0; c < n; c++) { (m[col, c], m[piv, c]) = (m[piv, c], m[col, c]); } (x[col], x[piv]) = (x[piv], x[col]); }
            for (int r = col + 1; r < n; r++)
            {
                double f = m[r, col] / m[col, col];
                if (f == 0) continue;
                for (int c = col; c < n; c++) m[r, c] -= f * m[col, c];
                x[r] -= f * x[col];
            }
        }
        var sol = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = x[i];
            for (int c = i + 1; c < n; c++) s -= m[i, c] * sol[c];
            sol[i] = s / m[i, i];
        }
        return sol;
    }

    /// <summary>OLS with intercept (y ~ [1, X]); returns coefficients (intercept at [0]) and R-squared. Null if singular.</summary>
    public static (double[] Coef, double R2)? OlsFit(double[][] x, double[] y)
    {
        int n = y.Length;
        int p = x.Length == 0 ? 0 : x[0].Length;
        int k = p + 1;
        var z = new double[n][];
        for (int i = 0; i < n; i++) { var row = new double[k]; row[0] = 1.0; for (int j = 0; j < p; j++) row[j + 1] = x[i][j]; z[i] = row; }
        var ztz = new double[k, k];
        var zty = new double[k];
        for (int i = 0; i < n; i++)
            for (int a = 0; a < k; a++)
            {
                zty[a] += z[i][a] * y[i];
                for (int b = 0; b < k; b++) ztz[a, b] += z[i][a] * z[i][b];
            }
        var beta = Solve(ztz, zty);
        if (beta == null) return null;
        double meanY = 0; for (int i = 0; i < n; i++) meanY += y[i]; meanY /= n;
        double ssRes = 0, ssTot = 0;
        for (int i = 0; i < n; i++)
        {
            double pred = 0; for (int a = 0; a < k; a++) pred += beta[a] * z[i][a];
            ssRes += (y[i] - pred) * (y[i] - pred);
            ssTot += (y[i] - meanY) * (y[i] - meanY);
        }
        double r2 = ssTot <= 0 ? 0 : 1.0 - ssRes / ssTot;
        return (beta, r2);
    }
}