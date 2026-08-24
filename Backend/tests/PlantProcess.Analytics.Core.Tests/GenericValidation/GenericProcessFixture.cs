// The generic controlled process fixture.
//
// Backlog origin: T-208.
//
// Vocabulary law: every identifier here is opaque. AnalysisSubject, ProcessUnit,
// Parameter, CategoryValue. No industry, no material identity, no plant story. An
// oracle flavoured by one customer would hand that customer's grain back to the
// product through the test suite.
//
// This file is the DATA SURFACE ONLY. Expected answers live in
// ContinuousProcessKnownAnswers and are not reachable from here: a kernel under test
// consumes this and cannot read the answer it is supposed to compute.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

public enum ObservationSourceKind { Machine, Manual }

public enum ProcessRegime { Stable, Transition, Stabilising }

public sealed record ProcessObservation(
    string SubjectId,
    string UnitId,
    string ParameterId,
    DateTimeOffset At,
    double? NumericValue,
    string? CategoryValue,
    ObservationSourceKind Source,
    TimeSpan ClockUncertainty,
    ProcessRegime Regime);

public sealed record RegimeInterval(string SubjectId, DateTimeOffset Start, DateTimeOffset End, ProcessRegime Regime);

public sealed record PerformanceReference(
    string ParameterId,
    string Kind,               // EngineeringStandard | ManagementTarget | OperatingEnvelope
    int Precedence,            // lower number wins
    double? Value,
    bool LowerIsBetter,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveTo);

public static class GenericProcessFixture
{
    public const string UnitA = "UNIT-A";
    public const string UnitB = "UNIT-B";

    public const string ContinuousParameter = "PARAM-C1";   // irregular step signal
    public const string RateParameter       = "PARAM-C2";   // rate; must integrate, never sum
    public const string StateParameter      = "PARAM-S1";   // categorical; Average undefined

    // One signal, three defensible means.
    public static IReadOnlyList<ProcessObservation> ContinuousSignal { get; } = new[]
    {
        Continuous(0, 100), Continuous(10, 100), Continuous(15, 130),
        Continuous(45, 130), Continuous(50, 110), Continuous(60, 110)
    };

    // A rate. Naive SUM answers 180; the true integral is 60.
    public static IReadOnlyList<ProcessObservation> RateSignal { get; } = new[]
    {
        Rate(0, 60), Rate(30, 120), Rate(45, 0), Rate(60, 0)
    };

    // Mean-of-ratios is not ratio-of-sums.
    public static IReadOnlyList<(string SubjectId, double Numerator, double Denominator)> RatioSubjects { get; } = new[]
    {
        ("SUBJ-001", 9d, 10d), ("SUBJ-002", 80d, 100d), ("SUBJ-003", 1d, 2d)
    };

    // Grain conversion needs the weight, not the count.
    public static IReadOnlyList<(string SubjectId, double DurationMinutes, double Mean)> WeightedSubjects { get; } = new[]
    {
        ("SUBJ-001", 10d, 100d), ("SUBJ-002", 40d, 130d), ("SUBJ-003", 10d, 110d)
    };

    // Transition confounding.
    public static IReadOnlyList<double> StableRegimeValues { get; } = new[] { 100d, 102d, 98d, 100d };
    public static IReadOnlyList<double> TransitionRegimeValues { get; } = new[] { 60d, 70d, 85d };

    public static IReadOnlyList<RegimeInterval> Regimes { get; } = new[]
    {
        new RegimeInterval("SUBJ-010", FrozenTestEpoch.AtMinute(0),  FrozenTestEpoch.AtMinute(30), ProcessRegime.Stable),
        new RegimeInterval("SUBJ-010", FrozenTestEpoch.AtMinute(30), FrozenTestEpoch.AtMinute(45), ProcessRegime.Transition),
        new RegimeInterval("SUBJ-010", FrozenTestEpoch.AtMinute(45), FrozenTestEpoch.AtMinute(60), ProcessRegime.Stabilising)
    };

    // Overlapping uncertainty is TemporalUncertain; disjoint is ConflictingEvidence.
    // Both live here, so a kernel cannot pass by always returning the cautious verdict.
    public static IReadOnlyList<ProcessObservation> TemporalPairOverlapping { get; } = new[]
    {
        Machine("SUBJ-020", 100, 1.0), Manual("SUBJ-020", 108, 15.0)
    };

    public static IReadOnlyList<ProcessObservation> TemporalPairDisjoint { get; } = new[]
    {
        Machine("SUBJ-021", 100, 1.0), Manual("SUBJ-021", 140, 15.0)
    };

    // Authority is fact-specific. Never "machine wins".
    public static IReadOnlyList<ProcessObservation> EvidencePairs { get; } = new[]
    {
        Machine("SUBJ-030", 200, 1.0), Manual("SUBJ-030", 200, 5.0),   // Aligned
        Machine("SUBJ-031", 200, 1.0), Manual("SUBJ-031", 205, 5.0),   // PartiallyAligned
        Machine("SUBJ-032", 200, 1.0),                                  // MissingEvidence
        Machine("SUBJ-033", 200, 1.0), Manual("SUBJ-033", 900, 5.0)    // ConflictingEvidence
    };

    // References, with one parameter deliberately uncovered.
    public static IReadOnlyList<PerformanceReference> References { get; } = new[]
    {
        new PerformanceReference(ContinuousParameter, "EngineeringStandard", 1, 100d, true,
            FrozenTestEpoch.AtMinute(0), FrozenTestEpoch.AtMinute(10_000)),
        new PerformanceReference(ContinuousParameter, "ManagementTarget", 2, 95d, true,
            FrozenTestEpoch.AtMinute(0), FrozenTestEpoch.AtMinute(10_000)),
        new PerformanceReference(RateParameter, "OperatingEnvelope", 3, null, false,
            FrozenTestEpoch.AtMinute(0), FrozenTestEpoch.AtMinute(0))   // expired: InsufficientReference
    };

    // Categorical. Average is undefined here.
    public static IReadOnlyList<ProcessObservation> StateSignal { get; } = new[]
    {
        Categorical(0, "CAT-1"), Categorical(20, "CAT-2"), Categorical(35, "CAT-1"), Categorical(60, "CAT-1")
    };

    // An empty window, immediately beside a populated one, so "the test passed because
    // zero rows existed" is detectable rather than invisible.
    public static IReadOnlyList<ProcessObservation> EmptyWindow { get; } = Array.Empty<ProcessObservation>();

    public static IReadOnlyList<ProcessObservation> AllObservations { get; } =
        ContinuousSignal
            .Concat(RateSignal)
            .Concat(StateSignal)
            .Concat(TemporalPairOverlapping)
            .Concat(TemporalPairDisjoint)
            .Concat(EvidencePairs)
            .ToArray();

    private static ProcessObservation Continuous(double minute, double value) => new(
        "SUBJ-010", UnitA, ContinuousParameter, FrozenTestEpoch.AtMinute(minute), value, null,
        ObservationSourceKind.Machine, TimeSpan.FromSeconds(1), ProcessRegime.Stable);

    private static ProcessObservation Rate(double minute, double value) => new(
        "SUBJ-011", UnitA, RateParameter, FrozenTestEpoch.AtMinute(minute), value, null,
        ObservationSourceKind.Machine, TimeSpan.FromSeconds(1), ProcessRegime.Stable);

    private static ProcessObservation Categorical(double minute, string category) => new(
        "SUBJ-012", UnitB, StateParameter, FrozenTestEpoch.AtMinute(minute), null, category,
        ObservationSourceKind.Machine, TimeSpan.FromSeconds(1), ProcessRegime.Stable);

    private static ProcessObservation Machine(string subject, double minute, double uncertaintySeconds) => new(
        subject, UnitA, ContinuousParameter, FrozenTestEpoch.AtMinute(minute), 200d, null,
        ObservationSourceKind.Machine, TimeSpan.FromSeconds(uncertaintySeconds), ProcessRegime.Stable);

    private static ProcessObservation Manual(string subject, double minute, double uncertaintyMinutes) => new(
        subject, UnitA, ContinuousParameter, FrozenTestEpoch.AtMinute(minute), 200d, null,
        ObservationSourceKind.Manual, TimeSpan.FromMinutes(uncertaintyMinutes), ProcessRegime.Stable);
}