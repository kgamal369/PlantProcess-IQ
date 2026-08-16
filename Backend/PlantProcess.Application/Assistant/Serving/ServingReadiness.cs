using System;
using System.Collections.Immutable;
using System.Linq;

namespace PlantProcess.Application.Assistant.Serving;

/// <summary>
/// T-137. FOUR STATES THAT MUST NEVER BE READ AS ONE.
///
///     ImplementationGreen   the contract exists and its tests pass
///     RuntimeStarted        a real runtime answered a real request
///     BenchmarkMeasured     Q-08..Q-11 were measured against that runtime
///     ProductionCertified   it is wired, approved and serving customers
///
/// Each implies nothing about the next. This project has spent a long time inside the
/// first, and the danger of staying there is not that the work is wrong: it is that a
/// green suite starts to feel like a running system. Every report from this lane has
/// had to say "the engine is real and is connected to nothing" in prose, which works
/// until somebody skims.
///
/// So the distinction is a value rather than a sentence. A caller asks the report which
/// states are attained; it answers with the reason each unattained one is not, and the
/// task that owns it. There is no property that returns true for a state nothing
/// established.
/// </summary>
public enum ServingReadinessState
{
    ImplementationGreen = 0,
    RuntimeStarted = 1,
    BenchmarkMeasured = 2,
    ProductionCertified = 3
}

/// <summary>One state, whether it is attained, and what established or blocks it.</summary>
public sealed record ReadinessEntry(
    ServingReadinessState State,
    bool Attained,
    string Evidence,
    string OwnerTaskWhenNotAttained);

/// <summary>
/// The readiness of a serving configuration.
///
/// Built only from things actually observed. The factory below cannot be talked into
/// marking a state attained without the observation that establishes it.
/// </summary>
public sealed record ServingReadinessReport(ImmutableArray<ReadinessEntry> Entries)
{
    public bool IsAttained(ServingReadinessState state) =>
        Entries.Any(e => e.State == state && e.Attained);

    public ImmutableArray<ServingReadinessState> AttainedStates =>
        Entries.Where(e => e.Attained).Select(e => e.State).OrderBy(s => (int)s).ToImmutableArray();

    /// <summary>
    /// True only when all four are attained.
    ///
    /// Exists so that nobody has to infer it from four separate fields, and it is
    /// false here for the honest reason.
    /// </summary>
    public bool IsProductionCertified =>
        Entries.Length == 4 && Entries.All(e => e.Attained);

    public string Render() =>
        string.Join(
            "\n",
            Entries
                .OrderBy(e => (int)e.State)
                .Select(e => $"{e.State,-22} {(e.Attained ? "attained" : "not attained")}  {e.Evidence}"));
}

/// <summary>Builds a readiness report from observations, never from intentions.</summary>
public static class ServingReadiness
{
    /// <summary>
    /// The state of this task at the moment it closes.
    ///
    /// runtimeAnsweredRealRequest is supplied by whoever observed it. In this build it
    /// is a deterministic fake and an HTTP-shaped stub, which establishes the contract
    /// and establishes nothing about a real provider.
    /// </summary>
    public static ServingReadinessReport Describe(
        bool contractTestsPass,
        bool runtimeAnsweredRealRequest,
        bool benchmarkMeasured,
        bool productionCertified) =>
        new(ImmutableArray.Create(
            new ReadinessEntry(
                ServingReadinessState.ImplementationGreen,
                contractTestsPass,
                contractTestsPass
                    ? "The serving contract compiles and its probes pass against a "
                        + "deterministic fake and a transport stub."
                    : "The contract probes do not pass.",
                contractTestsPass ? string.Empty : "T-137"),
            new ReadinessEntry(
                ServingReadinessState.RuntimeStarted,
                runtimeAnsweredRealRequest,
                runtimeAnsweredRealRequest
                    ? "A real runtime answered a real request."
                    : "No real provider or self-hosted model has been contacted. A fake "
                        + "and a stub prove the contract and prove nothing about a runtime.",
                runtimeAnsweredRealRequest ? string.Empty : "T-138 wires a real endpoint."),
            new ReadinessEntry(
                ServingReadinessState.BenchmarkMeasured,
                benchmarkMeasured,
                benchmarkMeasured
                    ? "Q-08 to Q-11 were measured against a running runtime."
                    : "Q-08 to Q-11 remain CapabilityUnavailable. There is nothing running "
                        + "to time, and a plausible latency would be indistinguishable from "
                        + "a real one.",
                benchmarkMeasured ? string.Empty : "T-182 measures; T-181 owns the gates."),
            new ReadinessEntry(
                ServingReadinessState.ProductionCertified,
                productionCertified,
                productionCertified
                    ? "Wired, approved and serving."
                    : "Not registered in production, not routed and not serving any "
                        + "customer. This task is forbidden from changing that.",
                productionCertified ? string.Empty : "T-138 owns the cutover.")));

    /// <summary>
    /// What T-137 alone can honestly claim.
    ///
    /// One state attained out of four, and the report says which three are not and why.
    /// </summary>
    public static ServingReadinessReport ForIsolatedImplementation(bool contractTestsPass) =>
        Describe(contractTestsPass, false, false, false);
}
