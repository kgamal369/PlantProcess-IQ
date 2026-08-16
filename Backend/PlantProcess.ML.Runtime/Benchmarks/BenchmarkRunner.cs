// T-182 - The common runner. It owns warm-up, repetition, aggregation, terminal
// state and envelope construction. It owns no benchmark-specific measurement:
// model-specific hooks delivered by earlier tasks plug in through
// IBenchmarkSubject and are neither modified nor reimplemented here.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace PlantProcess.ML.Runtime.Benchmarks;

/// <summary>
/// A subject reports its own availability and produces one sample's metrics per
/// call. It never reports a value it did not measure, and it signals absence by
/// returning false from <see cref="IsAvailable"/>, not by returning zeros.
/// </summary>
public interface IBenchmarkSubject
{
    string BenchmarkId { get; }

    SubjectIdentity Identity { get; }

    MeasurementScope Scope { get; }

    /// <summary>
    /// False when the thing this benchmark measures does not exist in this build.
    /// The reason and the owner that would change that are both required.
    /// </summary>
    bool IsAvailable(out string unavailableReason, out string ownerTrigger);

    /// <summary>One repetition. Returns the metrics observed, which may be empty.</summary>
    IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex);

    /// <summary>
    /// Run-level metrics that are not per-sample, such as an artifact size or a
    /// build time. Returns an empty list when there are none.
    /// </summary>
    IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload);
}

public interface IEnvironmentProbe
{
    EnvironmentIdentity Capture();
}

/// <summary>
/// Reads what the host actually reports. Anything it cannot read is "unknown";
/// anything that does not apply to this machine class is "not-applicable". The
/// probe never guesses a plausible value for either.
/// </summary>
public sealed class HostEnvironmentProbe : IEnvironmentProbe
{
    public EnvironmentIdentity Capture()
    {
        return new EnvironmentIdentity
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            CpuIdentity = EnvironmentIdentity.Unknown,
            LogicalCoreCount = Environment.ProcessorCount,
            TotalMemory = EnvironmentIdentity.Unknown,
            GpuIdentity = EnvironmentIdentity.Unknown,
            TotalVram = EnvironmentIdentity.Unknown
        };
    }
}

/// <summary>A probe with fixed values, so a fixture run is reproducible in a test.</summary>
public sealed class FixedEnvironmentProbe : IEnvironmentProbe
{
    private readonly EnvironmentIdentity _identity;

    public FixedEnvironmentProbe(EnvironmentIdentity identity)
    {
        _identity = identity;
    }

    public EnvironmentIdentity Capture() => _identity;

    public static FixedEnvironmentProbe Deterministic()
    {
        return new FixedEnvironmentProbe(new EnvironmentIdentity
        {
            OperatingSystem = "fixture-os",
            RuntimeVersion = "fixture-runtime",
            ProcessArchitecture = "fixture-arch",
            CpuIdentity = "fixture-cpu",
            LogicalCoreCount = 1,
            TotalMemory = "fixture-memory",
            GpuIdentity = EnvironmentIdentity.NotApplicable,
            TotalVram = EnvironmentIdentity.NotApplicable
        });
    }
}

public sealed record BenchmarkRequest
{
    public required string BenchmarkId { get; init; }
    public required WorkloadSpec Workload { get; init; }
    public required ExecutionPolicy Execution { get; init; }
    public required string GitCommit { get; init; }
    public required string MachineClass { get; init; }
    public string CapturedAtUtc { get; init; } = "not-recorded";
}

public sealed class BenchmarkRunner
{
    public const string HarnessVersion = "T-182/1";

    private readonly IEnvironmentProbe _environmentProbe;

    public BenchmarkRunner(IEnvironmentProbe environmentProbe)
    {
        _environmentProbe = environmentProbe;
    }

    public BenchmarkEnvelope Run(IBenchmarkSubject subject, BenchmarkRequest request)
    {
        if (subject is null)
        {
            throw new ArgumentNullException(nameof(subject));
        }

        BenchmarkDefinition definition = BenchmarkCatalogue.Get(request.BenchmarkId);

        if (!string.Equals(subject.BenchmarkId, request.BenchmarkId, StringComparison.Ordinal))
        {
            return Refuse(
                subject,
                request,
                BenchmarkTerminalState.InvalidInput,
                "subject is registered for " + subject.BenchmarkId
                    + " but was requested for " + request.BenchmarkId,
                "caller");
        }

        if (request.Execution.Repetitions <= 0 || request.Execution.WarmupIterations < 0)
        {
            return Refuse(
                subject,
                request,
                BenchmarkTerminalState.InvalidInput,
                "execution policy requires repetitions >= 1 and warmup >= 0",
                "caller");
        }

        if (string.IsNullOrWhiteSpace(request.Workload.DatasetHash)
            || string.IsNullOrWhiteSpace(request.Workload.SnapshotHash))
        {
            return Refuse(
                subject,
                request,
                BenchmarkTerminalState.InvalidInput,
                "workload dataset_hash or snapshot_hash is empty; state 'none' explicitly instead",
                "caller");
        }

        if (!subject.IsAvailable(out string unavailableReason, out string ownerTrigger))
        {
            return Refuse(
                subject,
                request,
                BenchmarkTerminalState.CapabilityUnavailable,
                string.IsNullOrWhiteSpace(unavailableReason)
                    ? "subject reported unavailable without a reason"
                    : unavailableReason,
                string.IsNullOrWhiteSpace(ownerTrigger) ? "unassigned" : ownerTrigger);
        }

        List<IReadOnlyList<MetricValue>> samples = new();
        List<MetricValue> scalars;

        try
        {
            for (int warmup = 0; warmup < request.Execution.WarmupIterations; warmup++)
            {
                subject.ExecuteSample(request.Workload, -1 - warmup);
            }

            for (int iteration = 0; iteration < request.Execution.Repetitions; iteration++)
            {
                samples.Add(subject.ExecuteSample(request.Workload, iteration));
            }

            scalars = subject.ScalarMetrics(request.Workload).ToList();
        }
        catch (Exception exception)
        {
            return Refuse(
                subject,
                request,
                BenchmarkTerminalState.Failed,
                "subject threw during execution: " + exception.GetType().Name + ": " + exception.Message,
                subject.BenchmarkId + " subject owner");
        }

        List<MetricValue> produced = new();
        foreach (IReadOnlyList<MetricValue> sample in samples)
        {
            produced.AddRange(sample);
        }

        produced.AddRange(scalars);

        foreach (MetricValue metric in produced)
        {
            string? metricError = MetricContract.Validate(metric);
            if (metricError is not null)
            {
                return Refuse(subject, request, BenchmarkTerminalState.InvalidInput, metricError, "subject");
            }

            if (!definition.Admits(metric.Family))
            {
                return Refuse(
                    subject,
                    request,
                    BenchmarkTerminalState.InvalidInput,
                    "metric '" + metric.Name + "' is of family " + metric.Family
                        + " which " + definition.BenchmarkId + " does not admit",
                    "subject");
            }
        }

        if (produced.Count == 0)
        {
            return Refuse(
                subject,
                request,
                BenchmarkTerminalState.NotMeasured,
                "subject reported availability but produced no metric; "
                    + "an absent metric is never reported as zero",
                subject.BenchmarkId + " subject owner");
        }

        List<MetricValue> aggregated = Aggregate(definition, samples, scalars);

        BenchmarkEnvelope envelope = new()
        {
            BenchmarkId = request.BenchmarkId,
            ExperimentId = ExperimentIdentity.Compute(
                request.BenchmarkId, subject.Identity, request.Workload, request.Execution),
            Subject = subject.Identity,
            Environment = _environmentProbe.Capture(),
            Workload = request.Workload,
            Execution = request.Execution,
            SampleCount = samples.Count,
            Scope = subject.Scope,
            TerminalState = BenchmarkTerminalState.Measured,
            Reproducibility = new ReproducibilityMetadata
            {
                GitCommit = request.GitCommit,
                HarnessVersion = HarnessVersion,
                CapturedAtUtc = request.CapturedAtUtc,
                MachineClass = request.MachineClass
            },
            Measurements = aggregated,
            RefusalReason = null,
            OwnerTrigger = null,
            Warnings = Array.Empty<string>()
        };

        string? invariant = envelope.Validate();
        if (invariant is not null)
        {
            throw new InvalidOperationException(
                "runner produced an invalid envelope: " + invariant);
        }

        return envelope;
    }

    /// <summary>
    /// Reduces the sample set. Percentiles are computed only for metrics whose
    /// family supports them and only when the benchmark admits that family. No
    /// metric is fabricated for a family the subject did not report.
    /// </summary>
    private static List<MetricValue> Aggregate(
        BenchmarkDefinition definition,
        IReadOnlyList<IReadOnlyList<MetricValue>> samples,
        IReadOnlyList<MetricValue> scalars)
    {
        Dictionary<string, List<MetricValue>> byName = new(StringComparer.Ordinal);
        List<string> order = new();

        foreach (IReadOnlyList<MetricValue> sample in samples)
        {
            foreach (MetricValue metric in sample)
            {
                if (!byName.TryGetValue(metric.Name, out List<MetricValue>? bucket))
                {
                    bucket = new List<MetricValue>();
                    byName[metric.Name] = bucket;
                    order.Add(metric.Name);
                }

                bucket.Add(metric);
            }
        }

        List<MetricValue> aggregated = new();

        foreach (string name in order)
        {
            List<MetricValue> bucket = byName[name];
            MetricFamily family = bucket[0].Family;
            string unit = bucket[0].Unit;
            List<double> values = bucket.Select(m => m.Value).ToList();

            MetricAggregation reduction = MetricContract.PrimaryReduction(family);
            double reduced = reduction switch
            {
                MetricAggregation.Mean => Percentiles.Mean(values),
                MetricAggregation.Sum => values.Sum(),
                MetricAggregation.Max => values.Max(),
                _ => throw new InvalidOperationException(
                    "unhandled reduction " + reduction + " for family " + family)
            };

            aggregated.Add(MetricValue.Create(name, family, reduced, unit, reduction));

            if (MetricContract.SupportsPercentiles(family) && definition.Admits(family))
            {
                aggregated.Add(MetricValue.Create(
                    name, family, Percentiles.P50(values), unit, MetricAggregation.P50));
                aggregated.Add(MetricValue.Create(
                    name, family, Percentiles.P95(values), unit, MetricAggregation.P95));
                aggregated.Add(MetricValue.Create(
                    name, family, Percentiles.P99(values), unit, MetricAggregation.P99));
            }
        }

        aggregated.AddRange(scalars);
        return aggregated;
    }

    private BenchmarkEnvelope Refuse(
        IBenchmarkSubject subject,
        BenchmarkRequest request,
        BenchmarkTerminalState state,
        string reason,
        string ownerTrigger)
    {
        if (state == BenchmarkTerminalState.Measured)
        {
            throw new ArgumentException("Refuse cannot produce a Measured envelope", nameof(state));
        }

        WorkloadSpec workload = request.Workload;
        if (string.IsNullOrWhiteSpace(workload.DatasetHash))
        {
            workload = workload with { DatasetHash = "none" };
        }

        if (string.IsNullOrWhiteSpace(workload.SnapshotHash))
        {
            workload = workload with { SnapshotHash = "none" };
        }

        BenchmarkEnvelope envelope = new()
        {
            BenchmarkId = request.BenchmarkId,
            ExperimentId = ExperimentIdentity.Compute(
                request.BenchmarkId, subject.Identity, workload, request.Execution),
            Subject = subject.Identity,
            Environment = _environmentProbe.Capture(),
            Workload = workload,
            Execution = request.Execution,
            SampleCount = 0,
            Scope = subject.Scope,
            TerminalState = state,
            Reproducibility = new ReproducibilityMetadata
            {
                GitCommit = request.GitCommit,
                HarnessVersion = HarnessVersion,
                CapturedAtUtc = request.CapturedAtUtc,
                MachineClass = request.MachineClass
            },
            Measurements = Array.Empty<MetricValue>(),
            RefusalReason = reason,
            OwnerTrigger = ownerTrigger,
            Warnings = Array.Empty<string>()
        };

        string? invariant = envelope.Validate();
        if (invariant is not null)
        {
            throw new InvalidOperationException(
                "runner produced an invalid refusal envelope: " + invariant);
        }

        return envelope;
    }
}
