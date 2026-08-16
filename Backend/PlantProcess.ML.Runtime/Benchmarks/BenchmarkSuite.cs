// T-182 - The suite. Runs every registered benchmark and reports readiness per
// B-ID. Readiness is computed from what the run produced, never asserted.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.ML.Runtime.Benchmarks;

public sealed record BenchmarkSuiteResult
{
    public required IReadOnlyList<BenchmarkEnvelope> SyntheticResults { get; init; }
    public required IReadOnlyList<BenchmarkEnvelope> ProductionResults { get; init; }
    public required IReadOnlyList<BenchmarkReadiness> Readiness { get; init; }

    public IReadOnlyList<string> NotMeasuredBenchmarkIds()
    {
        return Readiness.Where(r => !r.ProductionMeasured)
                        .Select(r => r.BenchmarkId)
                        .ToList();
    }
}

public static class BenchmarkSuite
{
    public const string DefaultMachineClass = "developer-workstation";

    /// <summary>
    /// Every B-01..B-09 has a registered fixture and a registered production
    /// subject. A missing registration is a failure of this method, not a gap
    /// discovered later at run time.
    /// </summary>
    public static IReadOnlyList<string> RegisteredBenchmarkIds()
    {
        List<string> registered = new();
        foreach (string benchmarkId in BenchmarkCatalogue.AllBenchmarkIds)
        {
            IBenchmarkSubject synthetic = BenchmarkFixtures.Synthetic(benchmarkId);
            IBenchmarkSubject production = BenchmarkFixtures.Production(benchmarkId);

            if (!string.Equals(synthetic.BenchmarkId, benchmarkId, StringComparison.Ordinal)
                || !string.Equals(production.BenchmarkId, benchmarkId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "registration mismatch for " + benchmarkId);
            }

            registered.Add(benchmarkId);
        }

        return registered;
    }

    public static BenchmarkRequest RequestFor(
        string benchmarkId,
        string gitCommit,
        string capturedAtUtc,
        int warmup = 1,
        int repetitions = 20)
    {
        return new BenchmarkRequest
        {
            BenchmarkId = benchmarkId,
            Workload = BenchmarkFixtures.StandardWorkload(benchmarkId),
            Execution = ExecutionPolicy.Of(warmup, repetitions),
            GitCommit = gitCommit,
            MachineClass = DefaultMachineClass,
            CapturedAtUtc = capturedAtUtc
        };
    }

    /// <summary>
    /// Runs the fixture for every B-ID and the production placeholder for every
    /// B-ID, then reports readiness. The production pass is not ceremony: it is
    /// how the manifest states which real values remain unmeasured and who owns
    /// each of them.
    /// </summary>
    public static BenchmarkSuiteResult RunAll(
        IEnvironmentProbe environmentProbe,
        string gitCommit,
        string capturedAtUtc)
    {
        BenchmarkRunner runner = new(environmentProbe);

        List<BenchmarkEnvelope> synthetic = new();
        List<BenchmarkEnvelope> production = new();
        List<BenchmarkReadiness> readiness = new();

        foreach (string benchmarkId in RegisteredBenchmarkIds())
        {
            BenchmarkRequest request = RequestFor(benchmarkId, gitCommit, capturedAtUtc);

            BenchmarkEnvelope syntheticEnvelope =
                runner.Run(BenchmarkFixtures.Synthetic(benchmarkId), request);
            BenchmarkEnvelope productionEnvelope =
                runner.Run(BenchmarkFixtures.Production(benchmarkId), request);

            synthetic.Add(syntheticEnvelope);
            production.Add(productionEnvelope);

            // The two records are kept apart deliberately. The synthetic record can
            // reach Measured while the production record truthfully stays
            // CapabilityUnavailable, and readiness reads one fact from each.
            readiness.Add(new BenchmarkReadiness
            {
                BenchmarkId = benchmarkId,
                HarnessImplemented = syntheticEnvelope.HarnessImplemented,
                SyntheticSmokeMeasured = syntheticEnvelope.SyntheticSmokeMeasured,
                ProductionMeasured = productionEnvelope.ProductionMeasured,
                SelectedValue = false,
                OwnerTrigger = productionEnvelope.OwnerTrigger ?? "unassigned"
            });
        }

        return new BenchmarkSuiteResult
        {
            SyntheticResults = synthetic,
            ProductionResults = production,
            Readiness = readiness
        };
    }
}
