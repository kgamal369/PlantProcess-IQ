// T-182 - Subjects.
//
// Two kinds, and the difference between them is the whole point of this file.
//
// DeterministicFixtureSubject produces fixed, reproducible numbers. It exists to
// falsify the harness: to prove the runner aggregates, the envelope validates and
// the manifest serialises. Its numbers describe nothing about the product and can
// never become a selected value - SelectedValueGuard refuses them by construction.
//
// ProductionSubjectPlaceholder reports that the real subject is not reachable from
// this build, names why, and names the task that would change that. It is not a
// stub that returns zero. It returns no measurement at all.
//
// The two are never collapsed into one record. Every B-ID produces both: a
// synthetic smoke that reaches Measured, and a production state that truthfully
// reaches CapabilityUnavailable with an owner.

using System;
using System.Collections.Generic;

namespace PlantProcess.ML.Runtime.Benchmarks;

/// <summary>
/// A metric this fixture emits, and the fixed arithmetic that produces its value.
/// No random source is involved, so two runs of the same fixture are identical.
/// </summary>
public sealed record FixtureMetricSpec
{
    public required string Name { get; init; }
    public required MetricFamily Family { get; init; }
    public required string Unit { get; init; }
    public required MetricAggregation Aggregation { get; init; }
    public required double Base { get; init; }
    public required double Step { get; init; }

    public double ValueAt(int iterationIndex)
    {
        int index = iterationIndex < 0 ? 0 : iterationIndex;
        return Base + (Step * index);
    }
}

public sealed class DeterministicFixtureSubject : IBenchmarkSubject
{
    private readonly IReadOnlyList<FixtureMetricSpec> _perSample;
    private readonly IReadOnlyList<FixtureMetricSpec> _scalars;

    public DeterministicFixtureSubject(
        string benchmarkId,
        string fixtureIdentity,
        SubjectFixtureKind fixtureKind,
        IReadOnlyList<FixtureMetricSpec> perSample,
        IReadOnlyList<FixtureMetricSpec> scalars)
    {
        if (fixtureKind == SubjectFixtureKind.RealSubject)
        {
            throw new ArgumentException(
                "a deterministic fixture cannot declare itself a real subject", nameof(fixtureKind));
        }

        BenchmarkId = benchmarkId;
        _perSample = perSample;
        _scalars = scalars;
        Identity = new SubjectIdentity
        {
            Kind = BenchmarkCatalogue.Get(benchmarkId).SubjectKind,
            Identity = fixtureIdentity,
            Version = "1",
            FixtureKind = fixtureKind
        };
    }

    public string BenchmarkId { get; }

    public SubjectIdentity Identity { get; }

    public MeasurementScope Scope => MeasurementScope.Synthetic;

    public bool IsAvailable(out string unavailableReason, out string ownerTrigger)
    {
        unavailableReason = string.Empty;
        ownerTrigger = string.Empty;
        return true;
    }

    public IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex)
    {
        List<MetricValue> metrics = new();
        foreach (FixtureMetricSpec spec in _perSample)
        {
            metrics.Add(MetricValue.Create(
                spec.Name, spec.Family, spec.ValueAt(iterationIndex), spec.Unit, spec.Aggregation));
        }

        return metrics;
    }

    public IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload)
    {
        List<MetricValue> metrics = new();
        foreach (FixtureMetricSpec spec in _scalars)
        {
            metrics.Add(MetricValue.Create(
                spec.Name, spec.Family, spec.Base, spec.Unit, spec.Aggregation));
        }

        return metrics;
    }
}

/// <summary>
/// Stands where a real subject will stand. Reports unavailable with a reason and
/// an owner. Calling it for a sample is a defect and throws rather than returning
/// a plausible number.
/// </summary>
public sealed class ProductionSubjectPlaceholder : IBenchmarkSubject
{
    private readonly string _reason;
    private readonly string _ownerTrigger;

    public ProductionSubjectPlaceholder(
        string benchmarkId,
        string reason,
        string ownerTrigger)
    {
        BenchmarkId = benchmarkId;
        _reason = reason;
        _ownerTrigger = ownerTrigger;
        Identity = new SubjectIdentity
        {
            Kind = BenchmarkCatalogue.Get(benchmarkId).SubjectKind,
            Identity = "not-reachable-from-this-build",
            Version = "none",
            FixtureKind = SubjectFixtureKind.RealSubject
        };
    }

    public string BenchmarkId { get; }

    public SubjectIdentity Identity { get; }

    public MeasurementScope Scope => MeasurementScope.Production;

    public bool IsAvailable(out string unavailableReason, out string ownerTrigger)
    {
        unavailableReason = _reason;
        ownerTrigger = _ownerTrigger;
        return false;
    }

    public IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex)
    {
        throw new InvalidOperationException(
            "ExecuteSample called on an unavailable production subject for " + BenchmarkId
            + "; the runner must refuse before reaching here");
    }

    public IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload)
    {
        throw new InvalidOperationException(
            "ScalarMetrics called on an unavailable production subject for " + BenchmarkId);
    }
}

/// <summary>
/// The fixture set. One deterministic subject per B-01..B-09, each emitting only
/// metric families its frozen definition admits, and each named after the quantity
/// its frozen method says to measure.
/// </summary>
public static class BenchmarkFixtures
{
    public static WorkloadSpec StandardWorkload(string benchmarkId)
    {
        return new WorkloadSpec
        {
            FixtureIdentity = "t182-fixture/" + benchmarkId,
            DatasetHash = "sha256:fixture-dataset-" + benchmarkId.ToLowerInvariant(),
            SnapshotHash = "sha256:fixture-snapshot-" + benchmarkId.ToLowerInvariant(),
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["profile"] = "smoke",
                ["scale"] = "1"
            }
        };
    }

    public static IBenchmarkSubject Synthetic(string benchmarkId)
    {
        return benchmarkId switch
        {
            // Loader throughput, storage amplification and random-access cost.
            BenchmarkCatalogue.B01 => Fixture(
                BenchmarkCatalogue.B01, "lane-resource-sizing-fixture",
                new[]
                {
                    Sample("loader_throughput", MetricFamily.Throughput,
                        MetricContract.ItemsPerSecondUnit, 1200.0, 10.0),
                    Sample("random_access_cost", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 4.0, 0.5)
                },
                new[]
                {
                    Scalar("storage_amplification", MetricFamily.Storage,
                        MetricContract.RatioUnit, 1.35),
                    Scalar("loader_peak_memory", MetricFamily.Memory,
                        MetricContract.MebibytesUnit, 256.0)
                }),

            // Scoring latency at the target arrival rate under saturated batch load.
            BenchmarkCatalogue.B02 => Fixture(
                BenchmarkCatalogue.B02, "online-scoring-reservation-fixture",
                new[]
                {
                    Sample("online_scoring_latency", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 60.0, 3.0)
                },
                new[]
                {
                    Scalar("arrival_rate", MetricFamily.Throughput,
                        MetricContract.ItemsPerSecondUnit, 200.0),
                    Scalar("deadline_breach_count", MetricFamily.Count,
                        MetricContract.CountUnit, 0.0)
                }),

            // Epoch and load time, peak RAM, storage size and seal time.
            BenchmarkCatalogue.B03 => Fixture(
                BenchmarkCatalogue.B03, "columnar-snapshot-format-fixture",
                new[]
                {
                    Sample("epoch_load_time", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 850.0, 15.0)
                },
                new[]
                {
                    Scalar("seal_time", MetricFamily.Duration,
                        MetricContract.SecondsUnit, 6.0),
                    Scalar("peak_resident_memory", MetricFamily.Memory,
                        MetricContract.MebibytesUnit, 768.0),
                    Scalar("artifact_storage_size", MetricFamily.Storage,
                        MetricContract.MebibytesUnit, 410.0)
                }),

            // Loader throughput, storage amplification and random-access cost.
            BenchmarkCatalogue.B04 => Fixture(
                BenchmarkCatalogue.B04, "sequence-chunking-fixture",
                new[]
                {
                    Sample("loader_throughput", MetricFamily.Throughput,
                        MetricContract.ItemsPerSecondUnit, 940.0, 8.0),
                    Sample("random_access_cost", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 6.5, 0.4)
                },
                new[]
                {
                    Scalar("storage_amplification", MetricFamily.Storage,
                        MetricContract.RatioUnit, 1.18),
                    Scalar("loader_peak_memory", MetricFamily.Memory,
                        MetricContract.MebibytesUnit, 192.0)
                }),

            // Model lift, p95 latency, artifact size and VRAM, with and without embeddings.
            BenchmarkCatalogue.B05 => Fixture(
                BenchmarkCatalogue.B05, "encoder-cost-fixture",
                new[]
                {
                    Sample("inference_latency_with_embeddings", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 18.0, 1.0),
                    Sample("inference_latency_without_embeddings", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 11.0, 0.5)
                },
                new[]
                {
                    Scalar("model_quality_with_embeddings", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.74),
                    Scalar("model_quality_without_embeddings", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.69),
                    Scalar("artifact_size", MetricFamily.Storage,
                        MetricContract.MebibytesUnit, 92.0),
                    Scalar("peak_vram", MetricFamily.Vram,
                        MetricContract.MebibytesUnit, 1024.0)
                }),

            // Recall at k against exact Flat, p95 latency, build time and RAM.
            BenchmarkCatalogue.B06 => Fixture(
                BenchmarkCatalogue.B06, "ann-family-fixture",
                new[]
                {
                    Sample("query_latency", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 3.0, 0.25)
                },
                new[]
                {
                    Scalar("recall_at_k_against_exact_flat", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.97),
                    Scalar("index_build_time", MetricFamily.Duration,
                        MetricContract.SecondsUnit, 12.0),
                    Scalar("index_resident_memory", MetricFamily.Memory,
                        MetricContract.MebibytesUnit, 512.0)
                }),

            // Groundedness, citation correctness and answer latency against evidence size.
            BenchmarkCatalogue.B07 => Fixture(
                BenchmarkCatalogue.B07, "evidence-budget-fixture",
                new[]
                {
                    Sample("answer_latency", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 240.0, 8.0)
                },
                new[]
                {
                    Scalar("groundedness", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.91),
                    Scalar("citation_correctness", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.88),
                    Scalar("packed_evidence_tokens", MetricFamily.Count,
                        MetricContract.CountUnit, 3072.0)
                }),

            // The same request with and without the seam, reported side by side.
            BenchmarkCatalogue.B08 => Fixture(
                BenchmarkCatalogue.B08, "reranking-seam-fixture",
                new[]
                {
                    Sample("retrieval_latency_with_reranking", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 96.0, 2.0),
                    Sample("retrieval_latency_without_reranking", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 41.0, 1.0)
                },
                new[]
                {
                    Scalar("citation_correctness_with_reranking", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.90),
                    Scalar("citation_correctness_without_reranking", MetricFamily.Quality,
                        MetricContract.RatioUnit, 0.82)
                }),

            // Time-to-first-token, throughput and VRAM per session. A stub, not a fixture,
            // because the thing it stands in for is a runtime that has not been started.
            BenchmarkCatalogue.B09 => new DeterministicFixtureSubject(
                BenchmarkCatalogue.B09,
                "serving-runtime-stub",
                SubjectFixtureKind.DeterministicStub,
                new[]
                {
                    Sample("time_to_first_token", MetricFamily.Latency,
                        MetricContract.MillisecondsUnit, 320.0, 12.0)
                },
                new[]
                {
                    Scalar("generation_throughput", MetricFamily.Throughput,
                        MetricContract.TokensPerSecondUnit, 48.0),
                    Scalar("vram_per_session", MetricFamily.Vram,
                        MetricContract.MebibytesUnit, 6144.0)
                }),

            _ => throw new ArgumentException(
                "'" + benchmarkId + "' is not one of B-01..B-09", nameof(benchmarkId))
        };
    }

    /// <summary>
    /// The real subject for each benchmark, none of which is reachable from this
    /// build. Each names the task that would make it reachable.
    /// </summary>
    public static IBenchmarkSubject Production(string benchmarkId)
    {
        BenchmarkDefinition definition = BenchmarkCatalogue.Get(benchmarkId);

        string owner = benchmarkId == BenchmarkCatalogue.B09
            ? "T-138 / later site benchmark"
            : definition.HookOwner + " hook wiring / later site benchmark";

        return new ProductionSubjectPlaceholder(
            benchmarkId,
            "no real " + definition.SubjectKind + " is reachable from this build; "
                + "T-182 delivers measurement machinery only",
            owner);
    }

    private static DeterministicFixtureSubject Fixture(
        string benchmarkId,
        string fixtureIdentity,
        IReadOnlyList<FixtureMetricSpec> perSample,
        IReadOnlyList<FixtureMetricSpec> scalars)
    {
        return new DeterministicFixtureSubject(
            benchmarkId, fixtureIdentity, SubjectFixtureKind.DeterministicFixture, perSample, scalars);
    }

    private static FixtureMetricSpec Sample(
        string name, MetricFamily family, string unit, double baseValue, double step)
    {
        return new FixtureMetricSpec
        {
            Name = name,
            Family = family,
            Unit = unit,
            Aggregation = MetricAggregation.Sample,
            Base = baseValue,
            Step = step
        };
    }

    private static FixtureMetricSpec Scalar(
        string name, MetricFamily family, string unit, double value)
    {
        return new FixtureMetricSpec
        {
            Name = name,
            Family = family,
            Unit = unit,
            Aggregation = MetricAggregation.Scalar,
            Base = value,
            Step = 0.0
        };
    }
}
