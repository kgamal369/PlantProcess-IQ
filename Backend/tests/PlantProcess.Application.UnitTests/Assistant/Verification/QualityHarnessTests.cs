using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Planning;
using PlantProcess.Application.Assistant.Retrieval;
using PlantProcess.Application.Assistant.Verification;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant.Verification;

/// <summary>
/// T-181. THE Q-01..Q-11 HARNESS.
///
/// The gates that can be measured here are measured on a fixed probe set. The four
/// that need a serving runtime are reported as unavailable, and a large part of this
/// file exists to prove they cannot quietly become numbers.
/// </summary>
public sealed class QualityHarnessTests
{
    private const string Tenant = "tenant_fixture";
    private const string ToolId = "layer_a.exact_count";
    private const string Handle = "evidence_0001";

    private static void AssertOrdered(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());

    // ------------------------------------------------------------- fixtures

    private static PlanningRequest Request(bool ambiguous = false) =>
        new(
            PermissionContext.Of(Tenant, "process_engineer", ToolId),
            ResolvedIntent.Create("harness_probe", ClaimClass.ObservedFact, true, "unit_scope"),
            ambiguous
                ? ImmutableArray.Create(ResolvedEntity.Ambiguous("unit_scope", "a", "b"))
                : ImmutableArray.Create(ResolvedEntity.Bound("unit_scope", "unit_scope_0001")),
            ToolRegistry.Of(DeclaredTool.Create(
                ToolId, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope")));

    private static EvidencePack Pack() =>
        EvidencePacker.Pack(
            DeterministicToolPlanner.Plan(Request()),
            new[]
            {
                EvidenceCandidate.Create(
                    Handle, Tenant, ToolId, EvidenceClass.StructuredToolResult, Handle,
                    "payload", 10, exactScore: 0.9)
            },
            TokenBudget.Of(1000, 200));

    private static EvidenceLedger Ledger() => EvidenceLedger.Of(
        new EvidenceFact(Handle, "rolling_speed", "m/s", 12.5, "unit_scope_0001"));

    private static AnswerDraft GoodDraft() => AnswerDraft.Create(
        "The recorded speed was 12.5 m/s.",
        "en",
        EngineOutcome.Answered,
        TransportState.Completed,
        claims: new[]
        {
            AnswerClaim.Numeric(
                "claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "rolling_speed",
                "unit_scope_0001", "The recorded speed was 12.5 m/s.", Handle)
        },
        citations: new[] { new AnswerCitation("cite_1", Handle, "claim_1") });

    private static AnswerDraft CausalUpgradeDraft() => AnswerDraft.Create(
        "The speed causes the outcome. 12.5",
        "en",
        EngineOutcome.Answered,
        TransportState.Completed,
        claims: new[]
        {
            AnswerClaim.Numeric(
                "claim_1", ClaimClass.Association, 12.5, "m/s", "rolling_speed",
                "unit_scope_0001", "The speed causes the outcome.", Handle)
        });

    private static QualityHarness.VerificationProbe Probe(
        string id, AnswerDraft draft, params VerificationCode[] expected) =>
        new(id, draft, Pack(), Ledger(), expected.Length == 0, expected.ToImmutableArray());

    private static QualityReport Run() => QualityHarness.Run(
        new[]
        {
            new QualityHarness.PlanningProbe(
                "plan_01", Request(), PlanningOutcome.Planned, ImmutableArray.Create(ToolId)),
            new QualityHarness.PlanningProbe(
                "plan_02", Request(ambiguous: true), PlanningOutcome.ClarificationRequired,
                ImmutableArray<string>.Empty)
        },
        new[]
        {
            Probe("verify_01", GoodDraft()),
            Probe("verify_02", CausalUpgradeDraft(), VerificationCode.ClaimClassUpgradedByPhrasing)
        },
        new[]
        {
            new QualityHarness.FidelityProbe("fidelity_01", GoodDraft(), GermanTwin())
        });

    private static AnswerDraft GermanTwin() => AnswerDraft.Create(
        "Die gemessene Geschwindigkeit betrug 12.5 m/s.",
        "de",
        EngineOutcome.Answered,
        TransportState.Completed,
        claims: new[]
        {
            AnswerClaim.Numeric(
                "claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "rolling_speed",
                "unit_scope_0001", "Die gemessene Geschwindigkeit betrug 12.5 m/s.", Handle)
        });

    // -------------------------------------------------------- the eleven gates

    [Fact]
    public void AllElevenGatesArePresentExactlyOnce()
    {
        var report = Run();

        Assert.Equal(11, report.Gates.Length);
        Assert.Equal(11, report.Gates.Select(g => g.GateId).Distinct().Count());
        AssertOrdered(
            Enum.GetValues<QualityGateId>().Select(g => g.ToString()),
            report.Gates.Select(g => g.GateId.ToString()));
    }

    [Fact]
    public void TheSevenMeasurableGatesAreMeasuredAndPass()
    {
        var report = Run();
        var measured = report.MeasuredGates;

        Assert.Equal(7, measured.Length);
        Assert.All(measured, gate => Assert.Equal(GateVerdict.Pass, gate.Verdict));
        Assert.True(report.AllMeasuredGatesPass);
    }

    [Fact]
    public void EveryMeasuredGateCarriesItsNumeratorDenominatorAndProbes()
    {
        foreach (var gate in Run().MeasuredGates)
        {
            Assert.NotNull(gate.Numerator);
            Assert.NotNull(gate.Denominator);
            Assert.NotNull(gate.Value);
            Assert.NotEmpty(gate.ProbeIds);
            Assert.False(string.IsNullOrWhiteSpace(gate.Reason));
        }
    }

    // ----------------------------------------- the four that cannot run here

    [Fact]
    public void TheFourRuntimeGatesAreUnavailableAndCarryNoValue()
    {
        var report = Run();
        var unavailable = report.UnavailableGates;

        AssertOrdered(
            new[]
            {
                "Q08_TimeToFirstToken", "Q09_TotalAnswerLatency",
                "Q10_ServingThroughput", "Q11_MemoryPerConcurrentSession"
            },
            unavailable.Select(g => g.GateId.ToString()));

        Assert.All(unavailable, gate =>
        {
            Assert.Equal(MeasurementState.CapabilityUnavailable, gate.State);
            Assert.Equal(GateVerdict.NotMeasured, gate.Verdict);
            Assert.Null(gate.Value);
            Assert.Null(gate.Numerator);
            Assert.Null(gate.Denominator);
        });
    }

    [Fact]
    public void AnUnavailableGateStillDeclaresItsUnitAndAggregation()
    {
        // The contract is complete even though the value is absent, so a later run
        // fills a defined shape rather than inventing one.
        foreach (var gate in Run().UnavailableGates)
        {
            Assert.False(string.IsNullOrWhiteSpace(gate.Unit));
            Assert.Contains("no serving runtime", gate.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("T-137", gate.OwnerTaskWhenUnavailable);
            Assert.Contains("T-138", gate.OwnerTaskWhenUnavailable);
        }
    }

    [Fact]
    public void TheReportCannotClaimAllElevenGatesArePassing()
    {
        // The property exists so that nobody has to infer it, and it is false here
        // because four gates never ran.
        var report = Run();

        Assert.True(report.AllMeasuredGatesPass);
        Assert.False(report.AllElevenGatesMeasuredAndPassing);
    }

    [Fact]
    public void NotMeasuredIsNeverQuietlyAPass()
    {
        Assert.All(
            Run().Gates.Where(g => g.State != MeasurementState.Measured),
            gate => Assert.NotEqual(GateVerdict.Pass, gate.Verdict));
    }

    [Fact]
    public void AnUnavailableGateIsDistinctFromAGateNobodyRan()
    {
        // CapabilityUnavailable says the thing to measure does not exist. NotMeasured
        // would say somebody skipped it. Collapsing them would hide which is true.
        var states = Enum.GetValues<MeasurementState>().Select(s => s.ToString()).ToArray();

        Assert.Contains("NotMeasured", states);
        Assert.Contains("CapabilityUnavailable", states);
        Assert.All(
            Run().UnavailableGates,
            gate => Assert.Equal(MeasurementState.CapabilityUnavailable, gate.State));
    }

    // ------------------------------------------------------------ reproducible

    [Fact]
    public void TheSameProbeSetProducesTheIdenticalFingerprint()
    {
        var expected = Run().ReportFingerprint();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(expected, Run().ReportFingerprint());
        }
    }

    [Fact]
    public void TheFingerprintCarriesNoTimestamp()
    {
        var fingerprint = Run().ReportFingerprint();
        foreach (var marker in new[] { "2026", "2025", ":00:", "T0" })
        {
            Assert.DoesNotContain(marker, fingerprint, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProbeDeclarationOrderDoesNotMoveTheReport()
    {
        var forward = QualityHarness.Run(
            new[]
            {
                new QualityHarness.PlanningProbe("plan_01", Request(), PlanningOutcome.Planned, ImmutableArray.Create(ToolId)),
                new QualityHarness.PlanningProbe("plan_02", Request(true), PlanningOutcome.ClarificationRequired, ImmutableArray<string>.Empty)
            },
            new[] { Probe("verify_01", GoodDraft()), Probe("verify_02", CausalUpgradeDraft(), VerificationCode.ClaimClassUpgradedByPhrasing) },
            Array.Empty<QualityHarness.FidelityProbe>());

        var reversed = QualityHarness.Run(
            new[]
            {
                new QualityHarness.PlanningProbe("plan_02", Request(true), PlanningOutcome.ClarificationRequired, ImmutableArray<string>.Empty),
                new QualityHarness.PlanningProbe("plan_01", Request(), PlanningOutcome.Planned, ImmutableArray.Create(ToolId))
            },
            new[] { Probe("verify_02", CausalUpgradeDraft(), VerificationCode.ClaimClassUpgradedByPhrasing), Probe("verify_01", GoodDraft()) },
            Array.Empty<QualityHarness.FidelityProbe>());

        Assert.Equal(forward.ReportFingerprint(), reversed.ReportFingerprint());
    }

    // ------------------------------------------------------------------- Q-01

    [Fact]
    public void Q01_MeasuresThePlannerWithoutModifyingIt()
    {
        var gate = Run().Gates.Single(g => g.GateId == QualityGateId.Q01_ToolSelectionAccuracy);

        Assert.Equal(MeasurementState.Measured, gate.State);
        Assert.Equal(2, gate.Denominator);
        Assert.Equal(2, gate.Numerator);
        Assert.Contains("not touched", gate.Reason);
    }

    [Fact]
    public void Q01_FailsWhenThePlannerDoesNotProduceTheLabelledPlan()
    {
        var report = QualityHarness.Run(
            new[]
            {
                new QualityHarness.PlanningProbe(
                    "plan_wrong", Request(), PlanningOutcome.Planned,
                    ImmutableArray.Create("a_tool_that_was_never_planned"))
            },
            Array.Empty<QualityHarness.VerificationProbe>(),
            Array.Empty<QualityHarness.FidelityProbe>());

        var gate = report.Gates.Single(g => g.GateId == QualityGateId.Q01_ToolSelectionAccuracy);
        Assert.Equal(0, gate.Numerator);
        Assert.Equal(GateVerdict.Fail, gate.Verdict);
    }

    // ------------------------------------------------------------------- Q-07

    [Fact]
    public void Q07_TwoLanguagesCarryingTheSameFactsAreFaithful()
    {
        Assert.True(QualityHarness.IsFaithful(GoodDraft(), GermanTwin()));

        var gate = Run().Gates.Single(g => g.GateId == QualityGateId.Q07_MultilingualFidelity);
        Assert.Equal(1, gate.Numerator);
        Assert.Equal(1, gate.Denominator);
    }

    [Fact]
    public void Q07_AChangedNumberUnitHandleOrClassBreaksFidelity()
    {
        var baseline = GoodDraft();

        var differentNumber = AnswerDraft.Create(
            "x", "de", claims: new[]
            {
                AnswerClaim.Numeric("claim_1", ClaimClass.ObservedFact, 99.0, "m/s", "rolling_speed", "unit_scope_0001", "x", Handle)
            });

        var differentUnit = AnswerDraft.Create(
            "x", "de", claims: new[]
            {
                AnswerClaim.Numeric("claim_1", ClaimClass.ObservedFact, 12.5, "kg", "rolling_speed", "unit_scope_0001", "x", Handle)
            });

        var differentHandle = AnswerDraft.Create(
            "x", "de", claims: new[]
            {
                AnswerClaim.Numeric("claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "rolling_speed", "unit_scope_0001", "x", "other_handle")
            });

        var differentClass = AnswerDraft.Create(
            "x", "de", claims: new[]
            {
                AnswerClaim.Numeric("claim_1", ClaimClass.Association, 12.5, "m/s", "rolling_speed", "unit_scope_0001", "x", Handle)
            });

        Assert.False(QualityHarness.IsFaithful(baseline, differentNumber));
        Assert.False(QualityHarness.IsFaithful(baseline, differentUnit));
        Assert.False(QualityHarness.IsFaithful(baseline, differentHandle));
        Assert.False(QualityHarness.IsFaithful(baseline, differentClass));
    }

    [Fact]
    public void Q07_AChangedRefusalMeaningBreaksFidelity()
    {
        var refused = AnswerDraft.Create("cannot answer", "en", EngineOutcome.Refused);
        var answered = AnswerDraft.Create("keine Antwort", "de", EngineOutcome.Answered);

        Assert.False(QualityHarness.IsFaithful(refused, answered));
    }

    // ------------------------------------------------------- credibility gates

    [Fact]
    public void Q05_FailsWhenARefusalIsReplacedByAnAnswer()
    {
        var erased = AnswerDraft.Create(
            "The recorded speed was 12.5 m/s.",
            "en",
            EngineOutcome.InsufficientEvidence,
            TransportState.Completed,
            claims: GoodDraft().Claims);

        var report = QualityHarness.Run(
            Array.Empty<QualityHarness.PlanningProbe>(),
            new[] { Probe("verify_erased", erased) },
            Array.Empty<QualityHarness.FidelityProbe>());

        var gate = report.Gates.Single(g => g.GateId == QualityGateId.Q05_RefusalCorrectness);
        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(0, gate.Numerator);
    }

    [Fact]
    public void Q06_FailsWhenCausalOverreachIsNotExpected()
    {
        var report = QualityHarness.Run(
            Array.Empty<QualityHarness.PlanningProbe>(),
            new[] { Probe("verify_upgrade", CausalUpgradeDraft()) },
            Array.Empty<QualityHarness.FidelityProbe>());

        var gate = report.Gates.Single(g => g.GateId == QualityGateId.Q06_CausalOverreachRate);
        Assert.Equal(GateVerdict.Fail, gate.Verdict);
    }

    [Fact]
    public void Q02_FailsWhenAnUncitedNumberSlipsThrough()
    {
        var uncited = AnswerDraft.Create(
            "The value was 7.",
            "en",
            claims: new[]
            {
                AnswerClaim.Numeric("claim_1", ClaimClass.ObservedFact, 7.0, "m/s", "rolling_speed", "unit_scope_0001", "The value was 7.")
            });

        var report = QualityHarness.Run(
            Array.Empty<QualityHarness.PlanningProbe>(),
            new[] { Probe("verify_uncited", uncited) },
            Array.Empty<QualityHarness.FidelityProbe>());

        var gate = report.Gates.Single(g => g.GateId == QualityGateId.Q02_Groundedness);
        Assert.Equal(GateVerdict.Fail, gate.Verdict);
    }

    [Fact]
    public void TheRuntimeGateContractCanBeInspectedWithoutRunningTheHarness()
    {
        var gates = QualityHarness.RuntimeGates();

        Assert.Equal(4, gates.Length);
        Assert.All(gates, gate => Assert.Equal(MeasurementState.CapabilityUnavailable, gate.State));
        Assert.All(gates, gate => Assert.Null(gate.Value));
    }
}
