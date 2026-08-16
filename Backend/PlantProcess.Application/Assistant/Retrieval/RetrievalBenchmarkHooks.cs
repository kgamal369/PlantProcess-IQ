using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace PlantProcess.Application.Assistant.Retrieval;

/// <summary>
/// T-180 owns the B-07 and B-08 HOOKS and their result shape only.
///
/// B-07 measures the retrieval and packing path. B-08 measures the same request with
/// and without the reranking seam, which is why the seam exists as a seam rather than
/// as a branch inside the packer.
///
/// The common benchmark framework is T-182's. No threshold is defined here, no run is
/// compared with another, and there is no field on the record where a verdict could be
/// written.
/// </summary>
public sealed record RetrievalMeasurement(
    string BenchmarkId,
    int ResultSchemaVersion,
    string TenantId,
    string IntentCode,
    string RerankerIdentity,
    string Outcome,
    int PermittedCandidateCount,
    int DistinctPermittedSourceCount,
    int PackedItemCount,
    int OmittedForBudgetCount,
    bool Truncated,
    bool SemanticSignalAvailable,
    int TokensUsed,
    int TokensAvailableForEvidence,
    int ReservedAnswerTokens,
    double ElapsedMilliseconds,
    string PackFingerprint,
    ImmutableArray<string> PackedHandles)
{
    public const int SchemaVersion = 1;
}

/// <summary>Produces a measurement from one packing run. Measures; never judges.</summary>
public static class RetrievalBenchmarkHooks
{
    /// <summary>B-07. One retrieval and packing run, timed.</summary>
    public static (EvidencePack Pack, RetrievalMeasurement Measurement) MeasureRetrieval(
        Func<EvidencePack> run,
        string benchmarkId = "B-07")
    {
        ArgumentNullException.ThrowIfNull(run);

        var stopwatch = Stopwatch.StartNew();
        var pack = run();
        stopwatch.Stop();

        return (pack, Describe(pack, benchmarkId, stopwatch.Elapsed.TotalMilliseconds));
    }

    /// <summary>
    /// B-08. The same request with and without the seam.
    ///
    /// Returned side by side and not compared. Whether a reranker earns its cost is a
    /// promotion decision, and promotion belongs to T-176.
    /// </summary>
    public static ImmutableArray<RetrievalMeasurement> MeasureRerankerSeam(
        Func<IEvidenceReranker?, EvidencePack> run,
        IEvidenceReranker candidateReranker)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(candidateReranker);

        var baseline = MeasureRetrieval(() => run(null), "B-08").Measurement;
        var withSeam = MeasureRetrieval(() => run(candidateReranker), "B-08").Measurement;

        return ImmutableArray.Create(baseline, withSeam);
    }

    private static RetrievalMeasurement Describe(
        EvidencePack pack,
        string benchmarkId,
        double elapsedMilliseconds) =>
        new(
            BenchmarkId: benchmarkId,
            ResultSchemaVersion: RetrievalMeasurement.SchemaVersion,
            TenantId: pack.TenantId,
            IntentCode: pack.IntentCode,
            RerankerIdentity: pack.RerankerIdentity,
            Outcome: pack.Outcome.ToString(),
            PermittedCandidateCount: pack.PermittedCandidateCount,
            DistinctPermittedSourceCount: pack.DistinctPermittedSourceCount,
            PackedItemCount: pack.Items.Length,
            OmittedForBudgetCount: pack.OmittedForBudgetCount,
            Truncated: pack.Truncated,
            SemanticSignalAvailable: pack.SemanticSignalAvailable,
            TokensUsed: pack.TokensUsed,
            TokensAvailableForEvidence: pack.Budget.AvailableForEvidence,
            ReservedAnswerTokens: pack.Budget.ReservedAnswerTokens,
            ElapsedMilliseconds: elapsedMilliseconds,
            PackFingerprint: pack.PackFingerprint(),
            PackedHandles: pack.Items.Select(i => i.EvidenceHandle).ToImmutableArray());
}
