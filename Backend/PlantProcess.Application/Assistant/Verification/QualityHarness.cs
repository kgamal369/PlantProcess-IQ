using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Planning;
using PlantProcess.Application.Assistant.Retrieval;

namespace PlantProcess.Application.Assistant.Verification;

/// <summary>
/// T-181. THE FIXED PROBE HARNESS.
///
/// It runs a declared probe set through the frozen planner, the frozen evidence packer
/// and this verifier, and reports each gate with the numbers behind it.
///
/// It calls no model. Every probe supplies its answer draft as a typed fixture, which
/// is what makes the measurement reproducible: the same probes on the same build give
/// the same report, and the fingerprint carries no clock reading to break that.
///
/// Q-01 measures the planner and does not modify it. The planner is frozen at its own
/// commit and is consumed here exactly as any caller would consume it.
/// </summary>
public static class QualityHarness
{
    /// <summary>One labelled probe: an input, and what should happen to it.</summary>
    public sealed record VerificationProbe(
        string ProbeId,
        AnswerDraft Draft,
        EvidencePack Pack,
        EvidenceLedger Ledger,
        bool ExpectedDisplayable,
        ImmutableArray<VerificationCode> ExpectedCodes);

    /// <summary>One labelled planning probe for Q-01.</summary>
    public sealed record PlanningProbe(
        string ProbeId,
        PlanningRequest Request,
        PlanningOutcome ExpectedOutcome,
        ImmutableArray<string> ExpectedToolIds);

    /// <summary>Two drafts that must mean the same thing in two languages, for Q-07.</summary>
    public sealed record FidelityProbe(
        string ProbeId,
        AnswerDraft First,
        AnswerDraft Second);

    /// <summary>Run every gate and produce one machine-readable report.</summary>
    public static QualityReport Run(
        IEnumerable<PlanningProbe> planningProbes,
        IEnumerable<VerificationProbe> verificationProbes,
        IEnumerable<FidelityProbe> fidelityProbes)
    {
        ArgumentNullException.ThrowIfNull(planningProbes);
        ArgumentNullException.ThrowIfNull(verificationProbes);
        ArgumentNullException.ThrowIfNull(fidelityProbes);

        var planning = planningProbes.OrderBy(p => p.ProbeId, StringComparer.Ordinal).ToArray();
        var verification = verificationProbes.OrderBy(p => p.ProbeId, StringComparer.Ordinal).ToArray();
        var fidelity = fidelityProbes.OrderBy(p => p.ProbeId, StringComparer.Ordinal).ToArray();

        var reports = verification.ToDictionary(
            probe => probe.ProbeId,
            probe => AnswerVerifier.Verify(probe.Draft, probe.Pack, probe.Ledger),
            StringComparer.Ordinal);

        var gates = new List<QualityGateResult>
        {
            MeasurePlanning(planning),
            MeasureByCode(
                QualityGateId.Q02_Groundedness, "Groundedness", verification, reports,
                new[]
                {
                    VerificationCode.UncitedNumericClaim,
                    VerificationCode.CitedValueDoesNotMatch,
                    VerificationCode.UnitMismatch,
                    VerificationCode.QuantityKindMismatch,
                    VerificationCode.UndeclaredNumberInText
                },
                "Every numeric claim resolves to evidence that supports its value, unit "
                    + "and quantity, and the prose declares no number the ledger does not."),
            MeasureByCode(
                QualityGateId.Q03_CitationCorrectness, "Citation correctness", verification, reports,
                new[]
                {
                    VerificationCode.FabricatedEvidenceHandle,
                    VerificationCode.CitationDoesNotSupportClaim,
                    VerificationCode.UndeclaredHandleInText,
                    VerificationCode.LedgerHandleNotInEvidencePack,
                    VerificationCode.SubjectMismatch
                },
                "Every cited handle exists in the evidence pack and supports the claim "
                    + "attached to it. A citation that exists is not a citation that supports."),
            MeasureByCode(
                QualityGateId.Q04_UnsupportedClaimRate, "Unsupported-claim rate", verification, reports,
                new[] { VerificationCode.UnsupportedMaterialClaim },
                "No material claim is asserted without resolving evidence."),
            MeasureByCode(
                QualityGateId.Q05_RefusalCorrectness, "Refusal correctness", verification, reports,
                new[]
                {
                    VerificationCode.GovernedRefusalReplacedByAnswer,
                    VerificationCode.TransportFailurePresentedAsConclusion
                },
                "A governed refusal survives phrasing, and a transport failure is never "
                    + "rendered as an absence of evidence, relationship or risk."),
            MeasureByCode(
                QualityGateId.Q06_CausalOverreachRate, "Causal-overreach rate", verification, reports,
                new[] { VerificationCode.ClaimClassUpgradedByPhrasing },
                "No phrasing claims more authority than its evidence class permits."),
            MeasureFidelity(fidelity)
        };

        gates.AddRange(RuntimeGates());

        return new QualityReport(
            QualityReport.Version,
            gates.OrderBy(g => (int)g.GateId).ToImmutableArray());
    }

    /// <summary>
    /// Q-01. The planner is run, never modified.
    ///
    /// A probe passes when the outcome and the ordered tool set are exactly what the
    /// fixture labelled.
    /// </summary>
    private static QualityGateResult MeasurePlanning(IReadOnlyList<PlanningProbe> probes)
    {
        var correct = 0L;

        foreach (var probe in probes)
        {
            var plan = DeterministicToolPlanner.Plan(probe.Request);
            var outcomeMatches = plan.Outcome == probe.ExpectedOutcome;
            var toolsMatch = plan.SelectedToolIds.SequenceEqual(probe.ExpectedToolIds, StringComparer.Ordinal);

            if (outcomeMatches && toolsMatch)
            {
                correct++;
            }
        }

        return QualityGateResult.Measured(
            QualityGateId.Q01_ToolSelectionAccuracy,
            "Tool-selection accuracy",
            correct,
            probes.Count,
            "fraction",
            probes.Select(p => p.ProbeId).ToImmutableArray(),
            "The frozen planner selects the labelled tool set for each probe. Measured "
                + "here; the planner implementation is not touched.");
    }

    /// <summary>A probe passes a gate when the verifier raised none of that gate's codes.</summary>
    private static QualityGateResult MeasureByCode(
        QualityGateId gateId,
        string gateName,
        IReadOnlyList<VerificationProbe> probes,
        IReadOnlyDictionary<string, VerificationReport> reports,
        IReadOnlyList<VerificationCode> codes,
        string reason)
    {
        var clean = 0L;

        foreach (var probe in probes)
        {
            var report = reports[probe.ProbeId];
            var raised = codes.Where(report.HasCode).ToArray();
            var expected = probe.ExpectedCodes.Where(codes.Contains).ToArray();

            // A probe designed to trip a gate passes the gate when it trips exactly
            // what it was labelled to trip. A gate that counted deliberate failures as
            // failures would report the harness rather than the product.
            if (raised.OrderBy(c => (int)c).SequenceEqual(expected.OrderBy(c => (int)c)))
            {
                clean++;
            }
        }

        return QualityGateResult.Measured(
            gateId,
            gateName,
            clean,
            probes.Count,
            "fraction",
            probes.Select(p => p.ProbeId).ToImmutableArray(),
            reason);
    }

    /// <summary>
    /// Q-07. Fidelity, not translation.
    ///
    /// The same answer in two languages must carry the same number, the same unit, the
    /// same handles, the same claim class and the same refusal meaning. Nothing here
    /// judges how well it reads.
    /// </summary>
    private static QualityGateResult MeasureFidelity(IReadOnlyList<FidelityProbe> probes)
    {
        var faithful = 0L;

        foreach (var probe in probes)
        {
            if (IsFaithful(probe.First, probe.Second))
            {
                faithful++;
            }
        }

        return QualityGateResult.Measured(
            QualityGateId.Q07_MultilingualFidelity,
            "Multilingual fidelity",
            faithful,
            probes.Count,
            "fraction",
            probes.Select(p => p.ProbeId).ToImmutableArray(),
            "Number, unit, evidence handle, claim class and refusal meaning are "
                + "identical across the declared languages.");
    }

    /// <summary>Whether two drafts assert the same thing in different words.</summary>
    public static bool IsFaithful(AnswerDraft first, AnswerDraft second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.EngineOutcome != second.EngineOutcome || first.Transport != second.Transport)
        {
            return false;
        }

        if (first.Claims.Length != second.Claims.Length)
        {
            return false;
        }

        var left = first.Claims.OrderBy(c => c.ClaimId, StringComparer.Ordinal).ToArray();
        var right = second.Claims.OrderBy(c => c.ClaimId, StringComparer.Ordinal).ToArray();

        for (var index = 0; index < left.Length; index++)
        {
            var a = left[index];
            var b = right[index];

            if (!string.Equals(a.ClaimId, b.ClaimId, StringComparison.Ordinal)
                || a.Class != b.Class
                || a.Kind != b.Kind
                || !Nullable.Equals(a.NumericValue, b.NumericValue)
                || !string.Equals(a.Unit, b.Unit, StringComparison.Ordinal)
                || !string.Equals(a.QuantityKind, b.QuantityKind, StringComparison.Ordinal)
                || !a.EvidenceHandles.OrderBy(h => h, StringComparer.Ordinal)
                    .SequenceEqual(b.EvidenceHandles.OrderBy(h => h, StringComparer.Ordinal), StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Q-08 to Q-11. Declared, contracted, and honestly unmeasured.
    ///
    /// The unit and the aggregation are defined so that the contract is complete and a
    /// later run can fill it. The value is absent because there is no serving runtime
    /// in this build to measure, and inventing one would be the single most damaging
    /// thing this harness could do.
    /// </summary>
    public static ImmutableArray<QualityGateResult> RuntimeGates()
    {
        const string owner = "T-137 supplies the serving runtime; T-138 reruns these gates at cutover.";
        const string reason =
            "No serving runtime exists in this isolated build, so there is nothing to "
                + "measure. No value is reported, because a plausible number here would be "
                + "indistinguishable from a real one.";

        return ImmutableArray.Create(
            QualityGateResult.Unavailable(
                QualityGateId.Q08_TimeToFirstToken, "Time to first token",
                "milliseconds, p95 over the probe set", reason, owner),
            QualityGateResult.Unavailable(
                QualityGateId.Q09_TotalAnswerLatency, "Total answer latency",
                "milliseconds, p95 over the probe set", reason, owner),
            QualityGateResult.Unavailable(
                QualityGateId.Q10_ServingThroughput, "Serving throughput",
                "answers per second, sustained mean", reason, owner),
            QualityGateResult.Unavailable(
                QualityGateId.Q11_MemoryPerConcurrentSession, "Memory per concurrent session",
                "megabytes, peak per session", reason, owner));
    }
}
