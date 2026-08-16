// T-182 - The catalogue of B-01..B-09.
//
// The question, method and decision text below is the frozen benchmark register
// supplied from PPIQ_Backlog_v2_10_1 (12 Aug 2026) and the AI/ML/LLM Target
// Architecture Optimisation document. It is recorded here, not authored here, and
// this task does not redesign it. There is no placeholder entry: every one of the
// nine carries its real question and its real method.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.ML.Runtime.Benchmarks;

/// <summary>
/// A benchmark's frozen statement of purpose and the metric families it admits.
/// The family set is what stops a meaningless metric being forced onto a
/// benchmark: a benchmark that does not admit the Vram family cannot report VRAM.
/// </summary>
public sealed record BenchmarkDefinition
{
    public required string BenchmarkId { get; init; }
    public required string Title { get; init; }
    public required string Question { get; init; }
    public required string Method { get; init; }
    public required string Decides { get; init; }
    public required string DecisionOwner { get; init; }
    public required string DefinitionSource { get; init; }

    /// <summary>The task owning the model-specific measurement hook for this benchmark.</summary>
    public required string HookOwner { get; init; }

    /// <summary>The domain kind of the thing this benchmark measures.</summary>
    public required string SubjectKind { get; init; }

    public required IReadOnlyList<MetricFamily> ApplicableFamilies { get; init; }

    public bool Admits(MetricFamily family)
    {
        foreach (MetricFamily candidate in ApplicableFamilies)
        {
            if (candidate == family)
            {
                return true;
            }
        }

        return false;
    }

    public bool AdmitsPercentiles()
    {
        foreach (MetricFamily family in ApplicableFamilies)
        {
            if (MetricContract.SupportsPercentiles(family))
            {
                return true;
            }
        }

        return false;
    }
}

public static class BenchmarkCatalogue
{
    public const string B01 = "B-01";
    public const string B02 = "B-02";
    public const string B03 = "B-03";
    public const string B04 = "B-04";
    public const string B05 = "B-05";
    public const string B06 = "B-06";
    public const string B07 = "B-07";
    public const string B08 = "B-08";
    public const string B09 = "B-09";

    /// <summary>The register the frozen definitions came from.</summary>
    public const string FrozenRegister = "PPIQ_Backlog_v2_10_1_12Aug2026";

    /// <summary>
    /// Every decision the register names is a product-owner ruling. A benchmark
    /// supplies evidence for it and never takes it.
    /// </summary>
    public const string ProductOwnerRuling = "product-owner ruling";

    private static readonly string[] AllIds = { B01, B02, B03, B04, B05, B06, B07, B08, B09 };

    private static readonly Dictionary<string, BenchmarkDefinition> Definitions = Build();

    public static IReadOnlyList<string> AllBenchmarkIds => AllIds;

    public static bool IsKnownBenchmarkId(string? benchmarkId)
    {
        if (benchmarkId is null)
        {
            return false;
        }

        foreach (string candidate in AllIds)
        {
            if (string.Equals(candidate, benchmarkId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static BenchmarkDefinition Get(string benchmarkId)
    {
        if (!Definitions.TryGetValue(benchmarkId, out BenchmarkDefinition? definition))
        {
            throw new ArgumentException(
                "'" + benchmarkId + "' is not one of B-01..B-09", nameof(benchmarkId));
        }

        return definition;
    }

    public static IReadOnlyList<BenchmarkDefinition> All()
    {
        return AllIds.Select(Get).ToList();
    }

    private static Dictionary<string, BenchmarkDefinition> Build()
    {
        Dictionary<string, BenchmarkDefinition> map = new(StringComparer.Ordinal);

        map[B01] = new BenchmarkDefinition
        {
            BenchmarkId = B01,
            Title = "Lane resource sizing - chunk size and compression",
            Question = "Chunk size and compression for lane resource sizing.",
            Method = "Vary chunk size and compression; measure loader throughput, "
                   + "storage amplification and random-access cost.",
            Decides = "Sequence chunk policy.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-170",
            SubjectKind = "SequenceLaneLoader",
            ApplicableFamilies = new[]
            {
                MetricFamily.Throughput, MetricFamily.Storage, MetricFamily.Latency, MetricFamily.Memory
            }
        };

        map[B02] = new BenchmarkDefinition
        {
            BenchmarkId = B02,
            Title = "Online scoring reservation",
            Question = "Reserved share for ml.online_scoring.",
            Method = "Load-test online and event scoring at the target arrival rate while "
                   + "training and batch load is saturated; observe whether p95 remains "
                   + "inside the actionable-deadline budget.",
            Decides = "Reservation fraction.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-182 adapter",
            SubjectKind = "OnlineScoringLane",
            ApplicableFamilies = new[]
            {
                MetricFamily.Latency, MetricFamily.Throughput, MetricFamily.Count
            }
        };

        map[B03] = new BenchmarkDefinition
        {
            BenchmarkId = B03,
            Title = "Columnar snapshot format",
            Question = "Parquet against Arrow IPC, and whether feature_snapshot_rows can be demoted.",
            Method = "Same population through candidate artifact paths; measure epoch and load "
                   + "time, peak RAM, storage size and seal time.",
            Decides = "Snapshot format and storage policy.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-169",
            SubjectKind = "ColumnarSnapshotArtifact",
            ApplicableFamilies = new[]
            {
                MetricFamily.Duration, MetricFamily.Memory, MetricFamily.Storage, MetricFamily.Latency
            }
        };

        map[B04] = new BenchmarkDefinition
        {
            BenchmarkId = B04,
            Title = "Sequence chunking and compression",
            Question = "Chunk size and compression.",
            Method = "Vary chunk size and compression; measure loader throughput, "
                   + "storage amplification and random-access cost.",
            Decides = "Sequence chunk policy.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-170",
            SubjectKind = "SequenceChunkLibrary",
            ApplicableFamilies = new[]
            {
                MetricFamily.Throughput, MetricFamily.Storage, MetricFamily.Latency, MetricFamily.Memory
            }
        };

        map[B05] = new BenchmarkDefinition
        {
            BenchmarkId = B05,
            Title = "Encoder value against serving cost",
            Question = "Whether the MF-01 encoder earns its cost.",
            Method = "Same snapshot with and without embedding columns; measure model lift, "
                   + "p95 latency delta, artifact size and VRAM.",
            Decides = "Whether MF-01 ships.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-172",
            SubjectKind = "ProcessEncoder",
            ApplicableFamilies = new[]
            {
                MetricFamily.Quality, MetricFamily.Latency, MetricFamily.Storage, MetricFamily.Vram
            }
        };

        map[B06] = new BenchmarkDefinition
        {
            BenchmarkId = B06,
            Title = "ANN family per size class",
            Question = "Flat against HNSW against IVF-PQ policy.",
            Method = "Build candidates on representative populations; measure recall at k "
                   + "against exact Flat, p95 latency, build time and RAM.",
            Decides = "index_policy thresholds.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-173",
            SubjectKind = "VectorSimilarityIndex",
            ApplicableFamilies = new[]
            {
                MetricFamily.Quality, MetricFamily.Latency, MetricFamily.Duration, MetricFamily.Memory
            }
        };

        map[B07] = new BenchmarkDefinition
        {
            BenchmarkId = B07,
            Title = "Evidence and token budget",
            Question = "Packed evidence size and token budget.",
            Method = "Vary evidence size; measure groundedness, citation correctness and answer latency.",
            Decides = "Evidence and token-budget policy.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-180",
            SubjectKind = "EvidencePacker",
            ApplicableFamilies = new[]
            {
                MetricFamily.Quality, MetricFamily.Latency, MetricFamily.Count
            }
        };

        map[B08] = new BenchmarkDefinition
        {
            BenchmarkId = B08,
            Title = "Re-ranking value",
            Question = "Whether re-ranking earns its latency.",
            Method = "Same retrieval request with and without the reranking or cross-encoder seam; "
                   + "measure the citation-correctness delta against added p95 latency.",
            Decides = "Ship or drop reranking.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-180",
            SubjectKind = "RetrievalRerankerSeam",
            ApplicableFamilies = new[]
            {
                MetricFamily.Quality, MetricFamily.Latency
            }
        };

        map[B09] = new BenchmarkDefinition
        {
            BenchmarkId = B09,
            Title = "Model serving runtime and concurrency",
            Question = "Serving runtime and concurrency.",
            Method = "Benchmark candidate runtimes at target concurrency; measure "
                   + "time-to-first-token, throughput and VRAM per session.",
            Decides = "Runtime selection and serving sizing.",
            DecisionOwner = ProductOwnerRuling,
            DefinitionSource = FrozenRegister,
            HookOwner = "T-137",
            SubjectKind = "ModelServingRuntime",
            ApplicableFamilies = new[]
            {
                MetricFamily.Latency, MetricFamily.Throughput, MetricFamily.Vram
            }
        };

        return map;
    }
}
