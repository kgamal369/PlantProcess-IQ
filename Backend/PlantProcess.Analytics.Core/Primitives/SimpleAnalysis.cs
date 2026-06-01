using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Numerics;

namespace PlantProcess.Analytics.Core.Primitives;

public enum ThresholdMode { LowerBoundFloor, UpperBoundCeiling }
public enum TrendDirection { Up, Down, Flat }

/// <summary>The v4 6.1 transparent primitives. Each returns value + complete metadata; edge cases never divide by zero.</summary>
public static class SimpleAnalysis
{
    private static List<double> Clean(IEnumerable<double?> data) => data.Where(v => v.HasValue).Select(v => v!.Value).ToList();

    private static AnalysisMetadata Meta(string formula, AnalysisContext ctx, int n) =>
        new(formula, ctx.Dataset, ctx.Filters, ctx.TimeWindow, ctx.RefreshedAtUtc, n, ctx.Unit);

    private static AnalysisResult Insufficient(string primitive, string formula, AnalysisContext ctx, int n, string why) =>
        new(primitive, null, AnalysisStatus.InsufficientData, Meta(formula, ctx, n), Message: why);

    public static AnalysisResult Count(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        return new("count", v.Count, AnalysisStatus.Ok, Meta("COUNT(x)", ctx, v.Count));
    }

    public static AnalysisResult SumOf(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count == 0) return Insufficient("sum", "SUM(x)", ctx, 0, "No non-null values.");
        return new("sum", Stats.Sum(v), AnalysisStatus.Ok, Meta("SUM(x)", ctx, v.Count));
    }

    public static AnalysisResult Average(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count == 0) return Insufficient("average", "SUM(x)/COUNT(x)", ctx, 0, "No non-null values.");
        return new("average", Stats.Mean(v), AnalysisStatus.Ok, Meta("SUM(x)/COUNT(x)", ctx, v.Count));
    }

    public static AnalysisResult Min(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count == 0) return Insufficient("min", "MIN(x)", ctx, 0, "No non-null values.");
        return new("min", v.Min(), AnalysisStatus.Ok, Meta("MIN(x)", ctx, v.Count));
    }

    public static AnalysisResult Max(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count == 0) return Insufficient("max", "MAX(x)", ctx, 0, "No non-null values.");
        return new("max", v.Max(), AnalysisStatus.Ok, Meta("MAX(x)", ctx, v.Count));
    }

    public static AnalysisResult MedianOf(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count == 0) return Insufficient("median", "MEDIAN(x)", ctx, 0, "No non-null values.");
        return new("median", Stats.Median(v), AnalysisStatus.Ok, Meta("MEDIAN(x)", ctx, v.Count));
    }

    public static AnalysisResult StdDev(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count < 2) return Insufficient("stdev", "SQRT(SUM((x-mean)^2)/(n-1))", ctx, v.Count, "Sample standard deviation needs at least 2 values.");
        return new("stdev", Stats.SampleStdDev(v), AnalysisStatus.Ok, Meta("SQRT(SUM((x-mean)^2)/(n-1))", ctx, v.Count));
    }

    public static AnalysisResult Ratio(IEnumerable<double?> numerator, IEnumerable<double?> denominator, AnalysisContext ctx)
    {
        var a = Clean(numerator); var b = Clean(denominator);
        double db = Stats.Sum(b);
        if (b.Count == 0 || db == 0) return Insufficient("ratio", "SUM(a)/SUM(b)", ctx, a.Count + b.Count, "Denominator is empty or zero.");
        double da = Stats.Sum(a);
        return new("ratio", da / db, AnalysisStatus.Ok, Meta("SUM(a)/SUM(b)", ctx, a.Count + b.Count),
            new Dictionary<string, double> { ["numerator"] = da, ["denominator"] = db });
    }

    public static AnalysisResult Rate(double eventCount, double exposure, double scale, AnalysisContext ctx)
    {
        if (exposure == 0) return Insufficient("rate", "events/exposure*scale", ctx, 0, "Exposure is zero.");
        double r = eventCount / exposure * scale;
        return new("rate", r, AnalysisStatus.Ok, Meta("events/exposure*scale", ctx, (int)Math.Round(eventCount)),
            new Dictionary<string, double> { ["events"] = eventCount, ["exposure"] = exposure, ["scale"] = scale });
    }

    /// <summary>Ordinary least squares slope over t = 0..n-1.</summary>
    public static AnalysisResult Trend(IEnumerable<double?> series, AnalysisContext ctx, double flatTolerance = 1e-9)
    {
        var v = Clean(series);
        if (v.Count < 2) return Insufficient("trend", "SLOPE(OLS(y~t))", ctx, v.Count, "Trend needs at least 2 points.");
        int n = v.Count;
        double mt = (n - 1) / 2.0, my = Stats.Mean(v), sxy = 0, sxx = 0;
        for (int i = 0; i < n; i++) { double dt = i - mt, dy = v[i] - my; sxy += dt * dy; sxx += dt * dt; }
        double slope = sxx == 0 ? 0 : sxy / sxx;
        double intercept = my - slope * mt;
        var dir = slope > flatTolerance ? TrendDirection.Up : (slope < -flatTolerance ? TrendDirection.Down : TrendDirection.Flat);
        return new("trend", slope, AnalysisStatus.Ok, Meta("SLOPE(OLS(y~t))", ctx, n),
            new Dictionary<string, double> { ["slope"] = slope, ["intercept"] = intercept }, dir.ToString());
    }

    public static AnalysisResult Threshold(double measured, double target, ThresholdMode mode, AnalysisContext ctx)
    {
        bool breach = mode == ThresholdMode.LowerBoundFloor ? measured < target : measured > target;
        return new("threshold", measured, AnalysisStatus.Ok, Meta($"value {(mode == ThresholdMode.LowerBoundFloor ? ">=" : "<=")} target", ctx, 1),
            new Dictionary<string, double> { ["target"] = target }, breach ? "Breach" : "OK");
    }

    public static AnalysisResult Distribution(IEnumerable<double?> data, AnalysisContext ctx)
    {
        var v = Clean(data);
        if (v.Count == 0) return Insufficient("distribution", "quantiles(x)", ctx, 0, "No non-null values.");
        var extras = new Dictionary<string, double>
        {
            ["min"] = v.Min(), ["p10"] = Stats.Quantile(v, 0.10), ["p25"] = Stats.Quantile(v, 0.25),
            ["p50"] = Stats.Quantile(v, 0.50), ["p75"] = Stats.Quantile(v, 0.75), ["p90"] = Stats.Quantile(v, 0.90),
            ["max"] = v.Max(), ["count"] = v.Count
        };
        return new("distribution", extras["p50"], AnalysisStatus.Ok, Meta("quantiles(x)", ctx, v.Count), extras);
    }

    public static AnalysisResult Comparison(IEnumerable<double?> groupA, IEnumerable<double?> groupB, AnalysisContext ctx)
    {
        var a = Clean(groupA); var b = Clean(groupB);
        if (a.Count == 0 || b.Count == 0) return Insufficient("comparison", "mean(A)-mean(B)", ctx, a.Count + b.Count, "Both groups must be non-empty.");
        double ma = Stats.Mean(a), mb = Stats.Mean(b), diff = ma - mb;
        double pct = mb == 0 ? double.NaN : diff / mb * 100.0;
        return new("comparison", diff, AnalysisStatus.Ok, Meta("mean(A)-mean(B)", ctx, a.Count + b.Count),
            new Dictionary<string, double> { ["meanA"] = ma, ["meanB"] = mb, ["percentChange"] = pct, ["nA"] = a.Count, ["nB"] = b.Count },
            ma >= mb ? "A>=B" : "A<B");
    }

    /// <summary>RAG status assuming higher is better.</summary>
    public static AnalysisResult Status(double value, double greenAtOrAbove, double amberAtOrAbove, AnalysisContext ctx)
    {
        string label = value >= greenAtOrAbove ? "Green" : (value >= amberAtOrAbove ? "Amber" : "Red");
        return new("status", value, AnalysisStatus.Ok, Meta("RAG(value; green, amber)", ctx, 1),
            new Dictionary<string, double> { ["greenAtOrAbove"] = greenAtOrAbove, ["amberAtOrAbove"] = amberAtOrAbove }, label);
    }
}