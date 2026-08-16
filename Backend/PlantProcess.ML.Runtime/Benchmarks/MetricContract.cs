// T-182 - The metric contract. A metric family admits a closed set of units and
// a closed set of aggregations. Anything outside the table is refused; nothing
// is coerced and no default unit is supplied.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlantProcess.ML.Runtime.Benchmarks;

public static class MetricContract
{
    public const string MillisecondsUnit = "ms";
    public const string SecondsUnit = "s";
    public const string ItemsPerSecondUnit = "items/s";
    public const string TokensPerSecondUnit = "tokens/s";
    public const string MebibytesUnit = "MiB";
    public const string BytesUnit = "bytes";
    public const string RatioUnit = "ratio";
    public const string CountUnit = "count";

    private static readonly Dictionary<MetricFamily, string[]> AllowedUnits = new()
    {
        [MetricFamily.Latency] = new[] { MillisecondsUnit, SecondsUnit },
        [MetricFamily.Throughput] = new[] { ItemsPerSecondUnit, TokensPerSecondUnit },
        [MetricFamily.Memory] = new[] { MebibytesUnit, BytesUnit },
        [MetricFamily.Vram] = new[] { MebibytesUnit, BytesUnit },
        [MetricFamily.Storage] = new[] { MebibytesUnit, BytesUnit, RatioUnit },
        [MetricFamily.Quality] = new[] { RatioUnit },
        [MetricFamily.Count] = new[] { CountUnit },
        [MetricFamily.Duration] = new[] { MillisecondsUnit, SecondsUnit }
    };

    private static readonly Dictionary<MetricFamily, MetricAggregation[]> AllowedAggregations = new()
    {
        [MetricFamily.Latency] = new[]
        {
            MetricAggregation.Sample, MetricAggregation.Mean, MetricAggregation.P50,
            MetricAggregation.P95, MetricAggregation.P99, MetricAggregation.Min, MetricAggregation.Max
        },
        [MetricFamily.Throughput] = new[]
        {
            MetricAggregation.Sample, MetricAggregation.Mean, MetricAggregation.Min,
            MetricAggregation.Max, MetricAggregation.Scalar
        },
        [MetricFamily.Memory] = new[]
        {
            MetricAggregation.Sample, MetricAggregation.Mean, MetricAggregation.Max, MetricAggregation.Scalar
        },
        [MetricFamily.Vram] = new[]
        {
            MetricAggregation.Sample, MetricAggregation.Mean, MetricAggregation.Max, MetricAggregation.Scalar
        },
        [MetricFamily.Storage] = new[]
        {
            MetricAggregation.Scalar, MetricAggregation.Mean, MetricAggregation.Max
        },
        [MetricFamily.Quality] = new[]
        {
            MetricAggregation.Scalar, MetricAggregation.Mean, MetricAggregation.Sample
        },
        [MetricFamily.Count] = new[] { MetricAggregation.Scalar, MetricAggregation.Sum },
        [MetricFamily.Duration] = new[]
        {
            MetricAggregation.Scalar, MetricAggregation.Sample, MetricAggregation.Mean
        }
    };

    /// <summary>
    /// Percentiles are computed only for these families. Asking for a p95 of a
    /// storage amplification ratio is a category error, not a missing feature.
    /// </summary>
    public static bool SupportsPercentiles(MetricFamily family)
    {
        return family == MetricFamily.Latency;
    }

    /// <summary>
    /// The single reduction the runner applies to a per-sample metric of this family.
    /// Total over MetricFamily; a family with no legal reduction is a contract defect,
    /// not a case to be defaulted.
    /// </summary>
    public static MetricAggregation PrimaryReduction(MetricFamily family)
    {
        if (IsAllowedAggregation(family, MetricAggregation.Mean))
        {
            return MetricAggregation.Mean;
        }

        if (IsAllowedAggregation(family, MetricAggregation.Sum))
        {
            return MetricAggregation.Sum;
        }

        if (IsAllowedAggregation(family, MetricAggregation.Max))
        {
            return MetricAggregation.Max;
        }

        throw new InvalidOperationException(
            "family " + family + " admits no reduction; the metric contract is incomplete");
    }

    public static bool IsAllowedUnit(MetricFamily family, string unit)
    {
        if (!AllowedUnits.TryGetValue(family, out string[]? units))
        {
            return false;
        }

        foreach (string candidate in units)
        {
            if (string.Equals(candidate, unit, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAllowedAggregation(MetricFamily family, MetricAggregation aggregation)
    {
        if (!AllowedAggregations.TryGetValue(family, out MetricAggregation[]? aggregations))
        {
            return false;
        }

        foreach (MetricAggregation candidate in aggregations)
        {
            if (candidate == aggregation)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns null when the metric is legal, or a sentence naming the violation.
    /// A non-finite value is a violation, not a value.
    /// </summary>
    public static string? Validate(MetricValue metric)
    {
        if (metric is null)
        {
            return "metric is null; a missing metric is reported by absence, never by a null entry";
        }

        if (string.IsNullOrWhiteSpace(metric.Name))
        {
            return "metric name is empty";
        }

        if (string.IsNullOrWhiteSpace(metric.Unit))
        {
            return "metric '" + metric.Name + "' has no unit; units are always explicit";
        }

        if (double.IsNaN(metric.Value) || double.IsInfinity(metric.Value))
        {
            return "metric '" + metric.Name + "' has a non-finite value";
        }

        if (!IsAllowedUnit(metric.Family, metric.Unit))
        {
            return "metric '" + metric.Name + "' uses unit '" + metric.Unit
                 + "' which is not legal for family " + metric.Family;
        }

        if (!IsAllowedAggregation(metric.Family, metric.Aggregation))
        {
            return "metric '" + metric.Name + "' uses aggregation " + metric.Aggregation
                 + " which is not legal for family " + metric.Family;
        }

        if (metric.Family == MetricFamily.Quality
            && (metric.Value < 0.0 || metric.Value > 1.0))
        {
            return "metric '" + metric.Name + "' is a ratio outside [0,1]: "
                 + metric.Value.ToString("R", CultureInfo.InvariantCulture);
        }

        return null;
    }
}
