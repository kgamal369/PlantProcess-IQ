// T-182 - Deterministic percentile semantics and stable experiment identity.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PlantProcess.ML.Runtime.Benchmarks;

/// <summary>
/// Nearest-rank percentiles on the sorted sample set, one documented method,
/// applied identically everywhere. No interpolation, so the reported value is
/// always a value that was actually observed.
/// </summary>
public static class Percentiles
{
    public const string Methodology = "nearest-rank, ceil(p * n), 1-based, no interpolation";

    public static double NearestRank(IReadOnlyList<double> samples, double percentile)
    {
        if (samples is null || samples.Count == 0)
        {
            throw new ArgumentException("percentile of an empty sample set is undefined", nameof(samples));
        }

        if (percentile <= 0.0 || percentile > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "percentile must be in (0,1]");
        }

        double[] ordered = samples.ToArray();
        Array.Sort(ordered);

        int rank = (int)Math.Ceiling(percentile * ordered.Length);
        if (rank < 1)
        {
            rank = 1;
        }

        if (rank > ordered.Length)
        {
            rank = ordered.Length;
        }

        return ordered[rank - 1];
    }

    public static double P50(IReadOnlyList<double> samples) => NearestRank(samples, 0.50);

    public static double P95(IReadOnlyList<double> samples) => NearestRank(samples, 0.95);

    public static double P99(IReadOnlyList<double> samples) => NearestRank(samples, 0.99);

    public static double Mean(IReadOnlyList<double> samples)
    {
        if (samples is null || samples.Count == 0)
        {
            throw new ArgumentException("mean of an empty sample set is undefined", nameof(samples));
        }

        double total = 0.0;
        foreach (double sample in samples)
        {
            total += sample;
        }

        return total / samples.Count;
    }
}

/// <summary>
/// Experiment identity is a hash of what defines the experiment: the benchmark,
/// the subject, the workload and the execution policy. It deliberately excludes
/// the clock, the environment and the commit, so that the same fixture run twice
/// on two machines is recognisably the same experiment. Those three are recorded
/// separately in the envelope rather than folded into its identity.
/// </summary>
public static class ExperimentIdentity
{
    public static string Compute(
        string benchmarkId,
        SubjectIdentity subject,
        WorkloadSpec workload,
        ExecutionPolicy execution)
    {
        StringBuilder canonical = new StringBuilder();
        canonical.Append("benchmark=").Append(benchmarkId).Append('\n');
        canonical.Append("subject.kind=").Append(subject.Kind).Append('\n');
        canonical.Append("subject.identity=").Append(subject.Identity).Append('\n');
        canonical.Append("subject.version=").Append(subject.Version).Append('\n');
        canonical.Append("subject.fixture_kind=").Append(subject.FixtureKind).Append('\n');
        canonical.Append("workload.fixture=").Append(workload.FixtureIdentity).Append('\n');
        canonical.Append("workload.dataset_hash=").Append(workload.DatasetHash).Append('\n');
        canonical.Append("workload.snapshot_hash=").Append(workload.SnapshotHash).Append('\n');

        List<string> keys = workload.Parameters.Keys.ToList();
        keys.Sort(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            canonical.Append("param.").Append(key).Append('=')
                     .Append(workload.Parameters[key]).Append('\n');
        }

        canonical.Append("execution.warmup=")
                 .Append(execution.WarmupIterations.ToString(CultureInfo.InvariantCulture)).Append('\n');
        canonical.Append("execution.repetitions=")
                 .Append(execution.Repetitions.ToString(CultureInfo.InvariantCulture)).Append('\n');

        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(canonical.ToString()));
        StringBuilder hex = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
        {
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }
}
