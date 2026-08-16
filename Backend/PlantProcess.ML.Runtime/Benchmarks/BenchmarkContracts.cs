// T-182 - Common benchmark harness and result manifest for B-01..B-09.
// Measurement machinery only. This file selects no production value and
// registers nothing into any production dependency graph.

using System;
using System.Collections.Generic;

namespace PlantProcess.ML.Runtime.Benchmarks;

/// <summary>
/// Total terminal state of a benchmark execution. Every execution ends in
/// exactly one of these. There is no implicit success state and no default.
/// </summary>
public enum BenchmarkTerminalState
{
    /// <summary>The benchmark ran and produced at least one measurement.</summary>
    Measured = 1,

    /// <summary>The subject exists but the measurement was not taken. Carries a reason and an owner.</summary>
    NotMeasured = 2,

    /// <summary>The subject required by this benchmark does not exist in this build. Carries a reason and an owner.</summary>
    CapabilityUnavailable = 3,

    /// <summary>The request or a produced metric violated the metric contract. Fails closed.</summary>
    InvalidInput = 4,

    /// <summary>The subject threw or aborted during execution.</summary>
    Failed = 5
}

/// <summary>Family of a metric. Determines which units and aggregations are legal.</summary>
public enum MetricFamily
{
    Latency = 1,
    Throughput = 2,
    Memory = 3,
    Vram = 4,
    Storage = 5,
    Quality = 6,
    Count = 7,
    Duration = 8
}

/// <summary>How a reported value was reduced from the sample set.</summary>
public enum MetricAggregation
{
    /// <summary>One value per sample, not reduced.</summary>
    Sample = 1,
    Mean = 2,
    P50 = 3,
    P95 = 4,
    P99 = 5,
    Min = 6,
    Max = 7,
    Sum = 8,
    /// <summary>A property of the run, not of a sample (artifact size, build time).</summary>
    Scalar = 9
}

/// <summary>
/// Whether a result came from a deterministic fixture or from a real workload.
/// A synthetic result can never become a selected production value.
/// </summary>
public enum MeasurementScope
{
    Synthetic = 1,
    Production = 2
}

/// <summary>Whether the subject under measurement is a fixture or the real thing.</summary>
public enum SubjectFixtureKind
{
    /// <summary>A fixed-arithmetic fixture. Verifies the harness, measures nothing real.</summary>
    DeterministicFixture = 1,

    /// <summary>A stub standing in for a runtime that is not started. Same standing as a fixture.</summary>
    DeterministicStub = 2,

    /// <summary>The real thing. The only kind a selected value may rest on.</summary>
    RealSubject = 3
}

/// <summary>
/// A single measured value. Name, unit and aggregation are always explicit.
/// There is no constructor path that produces a value without a unit.
/// </summary>
public sealed record MetricValue
{
    public required string Name { get; init; }
    public required MetricFamily Family { get; init; }
    public required double Value { get; init; }
    public required string Unit { get; init; }
    public required MetricAggregation Aggregation { get; init; }

    public static MetricValue Create(
        string name,
        MetricFamily family,
        double value,
        string unit,
        MetricAggregation aggregation)
    {
        return new MetricValue
        {
            Name = name,
            Family = family,
            Value = value,
            Unit = unit,
            Aggregation = aggregation
        };
    }
}

/// <summary>Identity of the thing being measured.</summary>
public sealed record SubjectIdentity
{
    /// <summary>The domain kind of the subject, e.g. ModelServingRuntime. Independent of FixtureKind.</summary>
    public required string Kind { get; init; }
    public required string Identity { get; init; }
    public required string Version { get; init; }
    public required SubjectFixtureKind FixtureKind { get; init; }
}

/// <summary>Identity of the workload the subject was measured against.</summary>
public sealed record WorkloadSpec
{
    public required string FixtureIdentity { get; init; }

    /// <summary>Hash of the dataset. Empty string is not permitted; use "none" explicitly.</summary>
    public required string DatasetHash { get; init; }

    /// <summary>Hash of the sealed snapshot artifact. Empty string is not permitted; use "none" explicitly.</summary>
    public required string SnapshotHash { get; init; }

    /// <summary>Ordered parameter set. Ordering is normalised before identity is computed.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Warm-up and repetition policy. Part of experiment identity.</summary>
public sealed record ExecutionPolicy
{
    public required int WarmupIterations { get; init; }
    public required int Repetitions { get; init; }

    public static ExecutionPolicy Of(int warmup, int repetitions)
    {
        return new ExecutionPolicy { WarmupIterations = warmup, Repetitions = repetitions };
    }
}

/// <summary>
/// Hardware and runtime identity. Recorded, never inferred. Fields the host
/// cannot report carry the literal "unknown"; fields that do not apply to this
/// machine class carry "not-applicable". The two are not the same statement.
/// </summary>
public sealed record EnvironmentIdentity
{
    public required string OperatingSystem { get; init; }
    public required string RuntimeVersion { get; init; }
    public required string ProcessArchitecture { get; init; }
    public required string CpuIdentity { get; init; }
    public required int LogicalCoreCount { get; init; }
    public required string TotalMemory { get; init; }
    public required string GpuIdentity { get; init; }
    public required string TotalVram { get; init; }

    public const string Unknown = "unknown";
    public const string NotApplicable = "not-applicable";
}

/// <summary>Everything needed to re-run the same experiment.</summary>
public sealed record ReproducibilityMetadata
{
    public required string GitCommit { get; init; }
    public required string HarnessVersion { get; init; }
    public required string CapturedAtUtc { get; init; }
    public required string MachineClass { get; init; }
}

/// <summary>
/// The one common result envelope for B-01..B-09. Its invariants are checked by
/// <see cref="Validate"/> and every construction path in the runner calls it.
/// </summary>
public sealed record BenchmarkEnvelope
{
    public const string CurrentSchemaVersion = "ppiq.benchmark.envelope/1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string BenchmarkId { get; init; }
    public required string ExperimentId { get; init; }
    public required SubjectIdentity Subject { get; init; }
    public required EnvironmentIdentity Environment { get; init; }
    public required WorkloadSpec Workload { get; init; }
    public required ExecutionPolicy Execution { get; init; }
    public required int SampleCount { get; init; }
    public required MeasurementScope Scope { get; init; }
    public required BenchmarkTerminalState TerminalState { get; init; }
    public required ReproducibilityMetadata Reproducibility { get; init; }

    public IReadOnlyList<MetricValue> Measurements { get; init; } = Array.Empty<MetricValue>();

    /// <summary>Set when and only when TerminalState is not Measured.</summary>
    public string? RefusalReason { get; init; }

    /// <summary>The task or actor that would make this measurable. Set with RefusalReason.</summary>
    public string? OwnerTrigger { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Total invariant check. Returns null when the envelope is well formed, or a
    /// sentence naming the violation. There is no partially valid envelope.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemaVersion))
        {
            return "schema_version is empty";
        }

        if (!BenchmarkCatalogue.IsKnownBenchmarkId(BenchmarkId))
        {
            return "benchmark_id '" + BenchmarkId + "' is not one of B-01..B-09";
        }

        if (string.IsNullOrWhiteSpace(ExperimentId))
        {
            return "experiment_id is empty";
        }

        if (string.IsNullOrWhiteSpace(Workload.DatasetHash))
        {
            return "workload.dataset_hash is empty; state 'none' explicitly instead";
        }

        if (string.IsNullOrWhiteSpace(Workload.SnapshotHash))
        {
            return "workload.snapshot_hash is empty; state 'none' explicitly instead";
        }

        if (SampleCount < 0)
        {
            return "sample_count is negative";
        }

        if (TerminalState == BenchmarkTerminalState.Measured)
        {
            if (Measurements.Count == 0)
            {
                return "terminal_state is Measured but no measurement is present; "
                     + "an absent metric is never reported as zero";
            }

            if (RefusalReason is not null || OwnerTrigger is not null)
            {
                return "terminal_state is Measured but a refusal_reason or owner_trigger is set";
            }

            if (SampleCount == 0)
            {
                return "terminal_state is Measured but sample_count is zero";
            }
        }
        else
        {
            if (Measurements.Count != 0)
            {
                return "terminal_state is " + TerminalState + " but measurements are attached";
            }

            if (string.IsNullOrWhiteSpace(RefusalReason))
            {
                return "terminal_state is " + TerminalState + " but refusal_reason is empty";
            }

            if (string.IsNullOrWhiteSpace(OwnerTrigger))
            {
                return "terminal_state is " + TerminalState + " but owner_trigger is empty";
            }
        }

        foreach (MetricValue metric in Measurements)
        {
            string? metricError = MetricContract.Validate(metric);
            if (metricError is not null)
            {
                return metricError;
            }
        }

        return null;
    }

    /// <summary>The harness ran this benchmark. True for every envelope this runner produced.</summary>
    public bool HarnessImplemented => true;

    /// <summary>A fixture or stub was measured. Verifies machinery, establishes no product value.</summary>
    public bool SyntheticSmokeMeasured =>
        TerminalState == BenchmarkTerminalState.Measured
        && Subject.FixtureKind != SubjectFixtureKind.RealSubject;

    /// <summary>A real subject was measured on a real workload.</summary>
    public bool ProductionMeasured => IsProductionEvidence();

    /// <summary>
    /// Always false on an envelope. A selected value is a separate artifact produced
    /// only by SelectedValueGuard, so no measurement can quietly become a decision.
    /// </summary>
    public bool SelectedValue => false;

    /// <summary>True only for a real measurement of a real subject.</summary>
    public bool IsProductionEvidence()
    {
        return TerminalState == BenchmarkTerminalState.Measured
            && Scope == MeasurementScope.Production
            && Subject.FixtureKind == SubjectFixtureKind.RealSubject;
    }

    public bool TryGetMetric(string name, MetricAggregation aggregation, out MetricValue metric)
    {
        foreach (MetricValue candidate in Measurements)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal)
                && candidate.Aggregation == aggregation)
            {
                metric = candidate;
                return true;
            }
        }

        metric = null!;
        return false;
    }
}

/// <summary>
/// The four readiness states T-182 must distinguish per benchmark. They are
/// independent: a harness that runs a fixture perfectly attains the first two
/// and neither of the last two.
/// </summary>
public sealed record BenchmarkReadiness
{
    public required string BenchmarkId { get; init; }
    public required bool HarnessImplemented { get; init; }
    public required bool SyntheticSmokeMeasured { get; init; }
    public required bool ProductionMeasured { get; init; }
    public required bool SelectedValue { get; init; }
    public required string OwnerTrigger { get; init; }
}
