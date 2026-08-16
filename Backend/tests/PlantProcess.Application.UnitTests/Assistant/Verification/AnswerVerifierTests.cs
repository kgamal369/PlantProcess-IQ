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
/// T-181. THE VERIFIER FALSIFICATION SET.
///
/// Every probe starts from one valid grounded answer and bends exactly one thing, so
/// a rejection is attributable to the rule it aims at.
/// </summary>
public sealed class AnswerVerifierTests
{
    private const string Tenant = "tenant_fixture";
    private const string ToolId = "layer_a.exact_count";
    private const string Handle = "evidence_0001";
    private const string OtherHandle = "evidence_0002";

    private static void AssertOrdered(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());

    // ------------------------------------------------------------- fixture

    private static ToolPlan Plan()
    {
        var registry = ToolRegistry.Of(
            DeclaredTool.Create(ToolId, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope"));

        return DeterministicToolPlanner.Plan(new PlanningRequest(
            PermissionContext.Of(Tenant, "process_engineer", ToolId),
            ResolvedIntent.Create("verification_probe", ClaimClass.ObservedFact, true, "unit_scope"),
            ImmutableArray.Create(ResolvedEntity.Bound("unit_scope", "unit_scope_0001")),
            registry));
    }

    private static EvidencePack Pack(params string[] handles)
    {
        var candidates = (handles.Length > 0 ? handles : new[] { Handle, OtherHandle })
            .Select(h => EvidenceCandidate.Create(
                h, Tenant, ToolId, EvidenceClass.StructuredToolResult, h,
                "payload of " + h, 10, exactScore: 0.9))
            .ToArray();

        return EvidencePacker.Pack(Plan(), candidates, TokenBudget.Of(1000, 200));
    }

    private static EvidenceLedger Ledger() => EvidenceLedger.Of(
        new EvidenceFact(Handle, "rolling_speed", "m/s", 12.5, "unit_scope_0001"),
        new EvidenceFact(OtherHandle, "unit_count", "units", 41.0, "unit_scope_0001"));

    private static AnswerClaim ValidClaim() => AnswerClaim.Numeric(
        "claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "rolling_speed", "unit_scope_0001",
        "The recorded speed was 12.5 m/s.", Handle);

    private static AnswerDraft ValidDraft(
        IEnumerable<AnswerClaim>? claims = null,
        string? text = null,
        EngineOutcome outcome = EngineOutcome.Answered,
        TransportState transport = TransportState.Completed,
        string language = "en",
        IEnumerable<AnswerCitation>? citations = null) =>
        AnswerDraft.Create(
            text ?? "The recorded speed was 12.5 m/s.",
            language,
            outcome,
            transport,
            governedReason: "",
            claims ?? new[] { ValidClaim() },
            citations ?? new[] { new AnswerCitation("cite_1", Handle, "claim_1") });

    private static VerificationReport Verify(
        AnswerDraft? draft = null, EvidencePack? pack = null, EvidenceLedger? ledger = null) =>
        AnswerVerifier.Verify(draft ?? ValidDraft(), pack ?? Pack(), ledger ?? Ledger());

    // =================================================================== V11

    [Fact]
    public void V11_AFullyGroundedCitedAnswerPasses()
    {
        var report = Verify();

        Assert.Equal(VerificationVerdict.Displayable, report.Verdict);
        Assert.Empty(report.Findings);
        Assert.Equal(1, report.CheckedNumericClaimCount);
    }

    // ==================================================================== V1

    [Fact]
    public void V1_AnUncitedNumericClaimIsRejected()
    {
        var uncited = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "rolling_speed", "unit_scope_0001",
            "The recorded speed was 12.5 m/s.");

        var report = Verify(ValidDraft(new[] { uncited }, citations: Array.Empty<AnswerCitation>()));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.UncitedNumericClaim));
    }

    // ==================================================================== V2

    [Fact]
    public void V2_AFabricatedEvidenceHandleIsRejected()
    {
        var fabricated = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "rolling_speed", "unit_scope_0001",
            "The recorded speed was 12.5 m/s.", "evidence_does_not_exist");

        var report = Verify(ValidDraft(
            new[] { fabricated },
            citations: new[] { new AnswerCitation("cite_1", "evidence_does_not_exist", "claim_1") }));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.FabricatedEvidenceHandle));
        Assert.True(report.HasCode(VerificationCode.UndeclaredHandleInText));
    }

    // ==================================================================== V3

    [Fact]
    public void V3_AValidHandleSupportingADifferentValueIsRejected()
    {
        var wrongValue = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 99.9, "m/s", "rolling_speed", "unit_scope_0001",
            "The recorded speed was 99.9 m/s.", Handle);

        var report = Verify(ValidDraft(
            new[] { wrongValue }, text: "The recorded speed was 99.9 m/s."));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.CitedValueDoesNotMatch));
    }

    [Fact]
    public void V3_ACitationThatExistsIsNotACitationThatSupports()
    {
        // The handle is real, permitted and packed. It simply says nothing about
        // this quantity. That is a rejection, not a pass.
        var borrowed = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 41.0, "units", "unit_count", "unit_scope_0001",
            "There were 41 units.", Handle);

        var report = Verify(ValidDraft(new[] { borrowed }, text: "There were 41 units."));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.QuantityKindMismatch));
    }

    // ==================================================================== V4

    [Fact]
    public void V4_TheWrongPhysicalUnitIsRejected()
    {
        // The failure named at the top of the 27 July review: a speed answered in a
        // unit of mass. Not an inaccuracy; evidence the answer was never grounded.
        var wrongUnit = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 12.5, "kg", "rolling_speed", "unit_scope_0001",
            "The recorded speed was 12.5 kg.", Handle);

        var report = Verify(ValidDraft(new[] { wrongUnit }, text: "The recorded speed was 12.5 kg."));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.UnitMismatch));
    }

    [Fact]
    public void V4_AQuantityAnsweredInTheWrongKindIsRejected()
    {
        var wrongQuantity = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 12.5, "m/s", "unit_count", "unit_scope_0001",
            "The count was 12.5 m/s.", Handle);

        var report = Verify(ValidDraft(new[] { wrongQuantity }, text: "The count was 12.5 m/s."));

        Assert.True(report.HasCode(VerificationCode.QuantityKindMismatch));
        Assert.Contains("never grounded", report.Findings.First(f =>
            f.Code == VerificationCode.QuantityKindMismatch).Detail);
    }

    // ==================================================================== V5

    [Fact]
    public void V5_AnAssociationPhrasedAsACauseIsRejected()
    {
        var upgraded = AnswerClaim.Numeric(
            "claim_1", ClaimClass.Association, 12.5, "m/s", "rolling_speed", "unit_scope_0001",
            "The higher speed causes the outcome.", Handle);

        var report = Verify(ValidDraft(new[] { upgraded }, text: "The higher speed causes the outcome. 12.5"));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.ClaimClassUpgradedByPhrasing));
    }

    [Fact]
    public void V5_AMeasuredCausalEffectMaySpeakOfCausation()
    {
        // The rule is about upgrading, not about forbidding the word. Evidence that
        // measured a causal effect may say so.
        var permitted = AnswerClaim.Numeric(
            "claim_1", ClaimClass.CausalEffect, 12.5, "m/s", "rolling_speed", "unit_scope_0001",
            "The change causes the measured effect.", Handle);

        var report = Verify(ValidDraft(new[] { permitted }, text: "The change causes the measured effect. 12.5"));

        Assert.False(report.HasCode(VerificationCode.ClaimClassUpgradedByPhrasing));
    }

    // ==================================================================== V6

    [Fact]
    public void V6_ALearnedContributionPhrasedAsAProvenRootCauseIsRejected()
    {
        var upgraded = AnswerClaim.Material(
            "claim_1", ClaimClass.Prediction, "unit_scope_0001",
            "This is the root cause and it proves that the setting was wrong.", Handle);

        var report = Verify(ValidDraft(
            new[] { upgraded },
            text: "This is the root cause and it proves that the setting was wrong."));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.ClaimClassUpgradedByPhrasing));

        var codes = report.Findings
            .Where(f => f.Code == VerificationCode.ClaimClassUpgradedByPhrasing)
            .ToArray();
        Assert.NotEmpty(codes);
    }

    [Fact]
    public void V6_APredictionPhrasedAsCertaintyIsRejected()
    {
        var upgraded = AnswerClaim.Material(
            "claim_1", ClaimClass.Prediction, "unit_scope_0001",
            "This will certainly happen on the next unit.", Handle);

        var report = Verify(ValidDraft(
            new[] { upgraded }, text: "This will certainly happen on the next unit."));

        Assert.True(report.HasCode(VerificationCode.ClaimClassUpgradedByPhrasing));
    }

    [Fact]
    public void V6_ARemediationCandidatePhrasedAsAnInstructionIsRejected()
    {
        var upgraded = AnswerClaim.Material(
            "claim_1", ClaimClass.RemediationCandidate, "unit_scope_0001",
            "You must set the value before the next stage.", Handle);

        var report = Verify(ValidDraft(
            new[] { upgraded }, text: "You must set the value before the next stage."));

        Assert.True(report.HasCode(VerificationCode.ClaimClassUpgradedByPhrasing));
    }

    // ==================================================================== V7

    [Fact]
    public void V7_AnEngineRefusalReplacedByAnAnswerIsRejected()
    {
        var report = Verify(ValidDraft(
            outcome: EngineOutcome.InsufficientEvidence,
            text: "The recorded speed was 12.5 m/s."));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.GovernedRefusalReplacedByAnswer));
    }

    [Fact]
    public void V7_ARefusalMayBePhrasedAndNotReplaced()
    {
        var report = Verify(AnswerDraft.Create(
            "There is not enough evidence to answer that.",
            "en",
            EngineOutcome.InsufficientEvidence,
            TransportState.Completed,
            "The readiness gate reported 46.5 percent completeness against an 85 percent bar."));

        Assert.Equal(VerificationVerdict.Displayable, report.Verdict);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void V7_ARefusalWithNoRefusalMarkerLeftInTheTextIsRejected()
    {
        var report = Verify(AnswerDraft.Create(
            "Everything looks fine.", "en", EngineOutcome.Refused, TransportState.Completed));

        Assert.True(report.HasCode(VerificationCode.GovernedRefusalReplacedByAnswer));
    }

    // ==================================================================== V8

    [Fact]
    public void V8_ATransportFailurePresentedAsNoEvidenceIsRejected()
    {
        var report = Verify(AnswerDraft.Create(
            "No relationship was found between the two parameters.",
            "en",
            EngineOutcome.Answered,
            TransportState.Failed));

        Assert.Equal(VerificationVerdict.SystemFailure, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.TransportFailurePresentedAsConclusion));
    }

    [Fact]
    public void V8_ATransportFailureIsItsOwnVerdictAndNotARejection()
    {
        // The distinction that matters: a system failure is not a refusal and not a
        // rejected answer. It is its own state.
        var report = Verify(AnswerDraft.Create(
            "The request could not be completed.",
            "en",
            EngineOutcome.Answered,
            TransportState.Failed));

        Assert.Equal(VerificationVerdict.SystemFailure, report.Verdict);
        Assert.Empty(report.Findings);
        Assert.Contains("never reported as an absence of evidence", report.Reason);
    }

    [Fact]
    public void V8_ATransportFailureMayNotCarryClaims()
    {
        var report = Verify(ValidDraft(transport: TransportState.Failed));

        Assert.Equal(VerificationVerdict.SystemFailure, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.TransportFailurePresentedAsConclusion));
    }

    // ==================================================================== V9

    [Fact]
    public void V9_AnUnsupportedMaterialClaimIsRejected()
    {
        var unsupported = AnswerClaim.Material(
            "claim_1", ClaimClass.ObservedFact, "unit_scope_0001",
            "The process was running normally throughout.");

        var report = Verify(ValidDraft(
            new[] { unsupported },
            text: "The process was running normally throughout.",
            citations: Array.Empty<AnswerCitation>()));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.UnsupportedMaterialClaim));
        Assert.Contains("omitting a figure", report.Findings.First().Detail);
    }

    // =================================================================== V10

    [Fact]
    public void V10_ACitedHandleThatDeclaresNoFactIsRejected()
    {
        var report = Verify(
            ValidDraft(),
            Pack(),
            EvidenceLedger.Of(new EvidenceFact(OtherHandle, "unit_count", "units", 41.0, "unit_scope_0001")));

        Assert.True(report.HasCode(VerificationCode.CitationDoesNotSupportClaim));
        Assert.Contains("not a citation that supports", report.Findings.First().Detail);
    }

    [Fact]
    public void V10_ACitedHandleDescribingADifferentSubjectIsRejected()
    {
        var report = Verify(
            ValidDraft(),
            Pack(),
            EvidenceLedger.Of(new EvidenceFact(Handle, "rolling_speed", "m/s", 12.5, "unit_scope_9999")));

        Assert.True(report.HasCode(VerificationCode.SubjectMismatch));
    }

    // ------------------------------------------------ text beyond the ledger

    [Fact]
    public void ANumberInTheProseThatNoClaimDeclaresIsRejected()
    {
        // The ledger is correct and the prose says something extra. Everything
        // structured looks right, which is what makes this shape dangerous.
        var report = Verify(ValidDraft(
            text: "The recorded speed was 12.5 m/s, which is 30 percent above target."));

        Assert.Equal(VerificationVerdict.Rejected, report.Verdict);
        Assert.True(report.HasCode(VerificationCode.UndeclaredNumberInText));
    }

    [Fact]
    public void ALedgerCannotDescribeEvidenceThePackDoesNotContain()
    {
        var report = Verify(
            ValidDraft(),
            Pack(Handle),
            EvidenceLedger.Of(
                new EvidenceFact(Handle, "rolling_speed", "m/s", 12.5, "unit_scope_0001"),
                new EvidenceFact("smuggled_handle", "rolling_speed", "m/s", 99.0, "unit_scope_0001")));

        Assert.True(report.HasCode(VerificationCode.LedgerHandleNotInEvidencePack));
        Assert.Contains("may never extend it", report.Findings
            .First(f => f.Code == VerificationCode.LedgerHandleNotInEvidencePack).Detail);
    }

    [Fact]
    public void AHandleMergedDuringPackingStillResolves()
    {
        // T-180 collapses duplicate content and keeps every handle. A citation of a
        // merged handle must still verify, or deduplication would break grounding.
        var duplicates = new[]
        {
            EvidenceCandidate.Create(Handle, Tenant, ToolId, EvidenceClass.StructuredToolResult,
                "shared_content", "payload", 10, exactScore: 0.9),
            EvidenceCandidate.Create(OtherHandle, Tenant, ToolId, EvidenceClass.StructuredToolResult,
                "shared_content", "payload", 10, exactScore: 0.9)
        };

        var pack = EvidencePacker.Pack(Plan(), duplicates, TokenBudget.Of(1000, 200));
        Assert.Single(pack.Items);

        var claim = AnswerClaim.Numeric(
            "claim_1", ClaimClass.ObservedFact, 41.0, "units", "unit_count", "unit_scope_0001",
            "There were 41 units.", OtherHandle);

        var report = AnswerVerifier.Verify(
            ValidDraft(new[] { claim }, text: "There were 41 units.",
                citations: new[] { new AnswerCitation("cite_1", OtherHandle, "claim_1") }),
            pack,
            Ledger());

        Assert.Equal(VerificationVerdict.Displayable, report.Verdict);
    }

    // ----------------------------------------------------------- determinism

    [Fact]
    public void TheSameDraftVerifiesIdenticallyEveryTime()
    {
        var expected = Verify().ReportFingerprint();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Equal(expected, Verify().ReportFingerprint());
        }
    }

    [Fact]
    public void FindingsAreOrderedDeterministicallyByCode()
    {
        var broken = AnswerClaim.Numeric(
            "claim_1", ClaimClass.Association, 99.9, "kg", "rolling_speed", "unit_scope_0001",
            "The speed causes it.", "fabricated_handle");

        var report = Verify(ValidDraft(new[] { broken }, text: "The speed causes it. 99.9"));

        var codes = report.Findings.Select(f => (int)f.Code).ToArray();
        Assert.Equal(codes.OrderBy(c => c).ToArray(), codes);
    }

    [Fact]
    public void TheFingerprintCarriesNoClockReading()
    {
        var fingerprint = Verify().ReportFingerprint();
        foreach (var year in new[] { "2026", "2025", "20260816" })
        {
            Assert.DoesNotContain(year, fingerprint, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------- languages

    [Fact]
    public void ADeclaredSecondLanguageIsPolicedByItsOwnPhrases()
    {
        var upgraded = AnswerClaim.Material(
            "claim_1", ClaimClass.Association, "unit_scope_0001",
            "Der hoehere Wert verursacht das Ergebnis.", Handle);

        var report = Verify(ValidDraft(
            new[] { upgraded },
            text: "Der hoehere Wert verursacht das Ergebnis.",
            language: "de"));

        Assert.True(report.HasCode(VerificationCode.ClaimClassUpgradedByPhrasing));
    }

    [Fact]
    public void ThePolicyDeclaresWhichLanguagesItCarries()
    {
        AssertOrdered(new[] { "en", "de" }, ClaimPhrasePolicy.DeclaredLanguages);
        Assert.True(ClaimPhrasePolicy.CarriesLanguage("en"));
        Assert.False(ClaimPhrasePolicy.CarriesLanguage("fr"));
    }
}
