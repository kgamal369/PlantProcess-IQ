// T-182 - The machine-readable manifest, and the guard that stands between a
// measurement and a published decision.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PlantProcess.ML.Runtime.Benchmarks;

/// <summary>
/// A value the product has chosen to act on - a chunk size, an index policy, a
/// token budget. It cannot exist without an envelope that measured it.
/// </summary>
public sealed record SelectedBenchmarkValue
{
    public required string BenchmarkId { get; init; }
    public required string MetricName { get; init; }
    public required double Value { get; init; }
    public required string Unit { get; init; }
    public required MetricAggregation Aggregation { get; init; }
    public required string EvidenceExperimentId { get; init; }
    public required string EvidenceGitCommit { get; init; }
}

/// <summary>
/// The single path by which a benchmark result becomes a decision. Every refusal
/// path returns a sentence naming the reason; there is no overload that skips it.
/// </summary>
public static class SelectedValueGuard
{
    public static bool TryPublish(
        BenchmarkEnvelope evidence,
        string metricName,
        MetricAggregation aggregation,
        out SelectedBenchmarkValue? selected,
        out string refusalReason)
    {
        selected = null;

        if (evidence is null)
        {
            refusalReason = "no evidence envelope was supplied";
            return false;
        }

        string? invariant = evidence.Validate();
        if (invariant is not null)
        {
            refusalReason = "evidence envelope is invalid: " + invariant;
            return false;
        }

        if (evidence.TerminalState != BenchmarkTerminalState.Measured)
        {
            refusalReason = "evidence terminal state is " + evidence.TerminalState
                          + "; a selected value requires a Measured result";
            return false;
        }

        if (evidence.Subject.FixtureKind != SubjectFixtureKind.RealSubject)
        {
            refusalReason = "evidence was produced by a "
                          + evidence.Subject.FixtureKind
                          + "; a fixture verifies the harness and never establishes a product value";
            return false;
        }

        if (evidence.Scope != MeasurementScope.Production)
        {
            refusalReason = "evidence scope is " + evidence.Scope
                          + "; a selected value requires a Production measurement";
            return false;
        }

        if (!evidence.TryGetMetric(metricName, aggregation, out MetricValue metric))
        {
            refusalReason = "evidence carries no metric '" + metricName + "' at aggregation "
                          + aggregation + "; an absent metric is not zero";
            return false;
        }

        selected = new SelectedBenchmarkValue
        {
            BenchmarkId = evidence.BenchmarkId,
            MetricName = metric.Name,
            Value = metric.Value,
            Unit = metric.Unit,
            Aggregation = metric.Aggregation,
            EvidenceExperimentId = evidence.ExperimentId,
            EvidenceGitCommit = evidence.Reproducibility.GitCommit
        };

        refusalReason = string.Empty;
        return true;
    }
}

/// <summary>
/// Writes the manifest. Field order is fixed in code rather than left to reflection,
/// so the same result set serialises byte-identically on every run.
/// </summary>
public static class BenchmarkManifestWriter
{
    public const string ManifestSchemaVersion = "ppiq.benchmark.manifest/1";

    public static string Write(IReadOnlyList<BenchmarkEnvelope> envelopes)
    {
        using MemoryStream stream = new();
        JsonWriterOptions options = new() { Indented = true };

        using (Utf8JsonWriter writer = new(stream, options))
        {
            writer.WriteStartObject();
            writer.WriteString("manifest_schema_version", ManifestSchemaVersion);
            writer.WriteString("harness_version", BenchmarkRunner.HarnessVersion);
            writer.WriteString("percentile_methodology", Percentiles.Methodology);

            writer.WriteStartArray("results");
            foreach (BenchmarkEnvelope envelope in envelopes)
            {
                WriteEnvelope(writer, envelope);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WritePercentile(
        Utf8JsonWriter writer,
        BenchmarkEnvelope envelope,
        string metricName,
        string label,
        MetricAggregation aggregation)
    {
        if (envelope.TryGetMetric(metricName, aggregation, out MetricValue metric))
        {
            writer.WriteString(label, metric.Value.ToString("R", CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteString(label, "not-applicable");
        }
    }

    private static void WriteEnvelope(Utf8JsonWriter writer, BenchmarkEnvelope envelope)
    {
        BenchmarkDefinition definition = BenchmarkCatalogue.Get(envelope.BenchmarkId);

        writer.WriteStartObject();
        writer.WriteString("schema_version", envelope.SchemaVersion);
        writer.WriteString("benchmark_id", envelope.BenchmarkId);
        writer.WriteString("experiment_id", envelope.ExperimentId);
        writer.WriteString("git_commit", envelope.Reproducibility.GitCommit);
        writer.WriteString("measurement_scope", envelope.Scope.ToString());

        writer.WriteStartObject("definition");
        writer.WriteString("title", definition.Title);
        writer.WriteString("question", definition.Question);
        writer.WriteString("method", definition.Method);
        writer.WriteString("decides", definition.Decides);
        writer.WriteString("decision_owner", definition.DecisionOwner);
        writer.WriteString("definition_source", definition.DefinitionSource);
        writer.WriteString("hook_owner", definition.HookOwner);
        writer.WriteEndObject();

        writer.WriteStartObject("subject");
        writer.WriteString("kind", envelope.Subject.Kind);
        writer.WriteString("identity", envelope.Subject.Identity);
        writer.WriteString("version", envelope.Subject.Version);
        writer.WriteString("fixture_kind", envelope.Subject.FixtureKind.ToString());
        writer.WriteEndObject();

        writer.WriteStartObject("environment");
        writer.WriteString("operating_system", envelope.Environment.OperatingSystem);
        writer.WriteString("runtime_version", envelope.Environment.RuntimeVersion);
        writer.WriteString("process_architecture", envelope.Environment.ProcessArchitecture);
        writer.WriteString("cpu_identity", envelope.Environment.CpuIdentity);
        writer.WriteNumber("logical_core_count", envelope.Environment.LogicalCoreCount);
        writer.WriteString("total_memory", envelope.Environment.TotalMemory);
        writer.WriteString("gpu_identity", envelope.Environment.GpuIdentity);
        writer.WriteString("total_vram", envelope.Environment.TotalVram);
        writer.WriteEndObject();

        writer.WriteStartObject("workload");
        writer.WriteString("identity", envelope.Workload.FixtureIdentity);
        writer.WriteString("dataset_hash", envelope.Workload.DatasetHash);
        writer.WriteString("snapshot_hash", envelope.Workload.SnapshotHash);
        writer.WriteStartObject("parameters");
        foreach (string key in envelope.Workload.Parameters.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            writer.WriteString(key, envelope.Workload.Parameters[key]);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartObject("execution");
        writer.WriteNumber("warmup_iterations", envelope.Execution.WarmupIterations);
        writer.WriteNumber("repetitions", envelope.Execution.Repetitions);
        writer.WriteNumber("sample_count", envelope.SampleCount);
        writer.WriteEndObject();

        writer.WriteStartArray("measurements");
        foreach (MetricValue metric in envelope.Measurements)
        {
            writer.WriteStartObject();
            writer.WriteString("name", metric.Name);
            writer.WriteString("family", metric.Family.ToString());
            writer.WriteString("aggregation", metric.Aggregation.ToString());
            writer.WriteString("value", metric.Value.ToString("R", CultureInfo.InvariantCulture));
            writer.WriteString("unit", metric.Unit);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        // Percentiles are emitted only where the frozen definition admits a family
        // that has them. An absent block means "not applicable", not "zero".
        writer.WriteStartArray("percentiles");
        foreach (MetricValue metric in envelope.Measurements)
        {
            if (metric.Aggregation != MetricAggregation.P50) { continue; }

            writer.WriteStartObject();
            writer.WriteString("metric", metric.Name);
            writer.WriteString("unit", metric.Unit);
            writer.WriteString("methodology", Percentiles.Methodology);
            WritePercentile(writer, envelope, metric.Name, "p50", MetricAggregation.P50);
            WritePercentile(writer, envelope, metric.Name, "p95", MetricAggregation.P95);
            WritePercentile(writer, envelope, metric.Name, "p99", MetricAggregation.P99);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteString("terminal_state", envelope.TerminalState.ToString());

        if (envelope.RefusalReason is null)
        {
            writer.WriteNull("refusal_reason");
        }
        else
        {
            writer.WriteString("refusal_reason", envelope.RefusalReason);
        }

        if (envelope.OwnerTrigger is null)
        {
            writer.WriteNull("owner_trigger");
        }
        else
        {
            writer.WriteString("owner_trigger", envelope.OwnerTrigger);
        }

        writer.WriteStartArray("warnings");
        foreach (string warning in envelope.Warnings)
        {
            writer.WriteStringValue(warning);
        }

        writer.WriteEndArray();

        writer.WriteStartObject("readiness");
        writer.WriteBoolean("harness_implemented", envelope.HarnessImplemented);
        writer.WriteBoolean("synthetic_smoke_measured", envelope.SyntheticSmokeMeasured);
        writer.WriteBoolean("production_measured", envelope.ProductionMeasured);
        writer.WriteBoolean("selected_value", envelope.SelectedValue);
        writer.WriteEndObject();

        writer.WriteStartObject("reproducibility");
        writer.WriteString("git_commit", envelope.Reproducibility.GitCommit);
        writer.WriteString("harness_version", envelope.Reproducibility.HarnessVersion);
        writer.WriteString("captured_at_utc", envelope.Reproducibility.CapturedAtUtc);
        writer.WriteString("machine_class", envelope.Reproducibility.MachineClass);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
