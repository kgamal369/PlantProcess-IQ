// Transition-aware pooling guard contract.
//
// Backlog origin: T-236.
//
// Answers one question before any statistic is computed: may these samples be pooled?
//
// The failure this prevents is not an outlier problem. Steady-state and transition
// samples are both correct measurements of what the plant was doing; pooling them
// produces an average of two different processes, and the result looks entirely
// plausible. The committed validation fixture carries the case: four steady samples
// averaging 100, three transition samples, and a pooled mean of 615/7. The pooled
// number is not noisy - it is confidently wrong, and nothing in it announces that.
//
// A sample's regime is only usable if its timestamp uncertainty does not straddle a
// regime boundary. An instant that could fall on either side of a changeover has no
// determinable regime, and admitting it under whichever side the point estimate landed
// on would reintroduce the same defect one sample at a time.
//
// Deliberately out of scope: the statistics themselves, reconciliation and persistence.
// This contract admits or refuses a population; what is computed from an admitted one
// belongs elsewhere.
using System;
using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// One sample offered for pooling, carrying the instant it was taken at with the
/// uncertainty that instant was admitted under.
/// </summary>
public sealed record RegimeScopedSample(string ScopeKey, TemporalInstant Instant, double Value);

/// <summary>
/// Whether a population may be pooled, and under which regime. An admitted population
/// carries exactly one regime; a refused one carries none.
/// </summary>
public sealed record PoolingAdmission(
    bool IsAdmitted,
    OperationalRegime Regime,
    int SampleCount,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// Refusal and admission codes. RG01 is the string the committed validation fixture
/// already names, so the fixture, the regime classifier and this guard all agree on one
/// spelling.
/// </summary>
public static class PoolingGuardCodes
{
    public const string MixedProcessRegime = "RG01 mixed_process_regime";
    public const string SampleRegimeTemporallyUncertain = "RG02 sample_regime_temporally_uncertain";
    public const string EmptyPopulation = "RG03 empty_population";
    public const string HeterogeneousScope = "RG04 heterogeneous_scope";

    public const string PoolingAdmitted = "RG10 pooling_admitted";
}