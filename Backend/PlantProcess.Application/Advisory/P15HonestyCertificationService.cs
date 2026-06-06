namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION
/// Adversarial certification service for Phase 15 recommendation honesty and approval governance.
///
/// Certification rules:
/// - no causal language
/// - no guaranteed saving claim
/// - weak evidence blocks recommendation
/// - out-of-envelope scenario abstains
/// - approval command must be explicit
/// - no automatic write-back path
/// </summary>
public sealed class P15HonestyCertificationService
{
    public P15HonestyCertificationReport RunCertification()
    {
        var cases = new List<P15HonestyCertificationCase>();

        cases.Add(CertifyCleanRecommendation());
        cases.Add(CertifyCausalLanguageBlocked());
        cases.Add(CertifyWeakEvidenceBlocked());
        cases.Add(CertifyOutOfEnvelopeAbstains());
        cases.Add(CertifyApprovalCommandRequired());
        cases.Add(CertifyWriteBackBlocked());

        var passed = cases.Count(item => item.Passed);
        var failed = cases.Count - passed;

        return new P15HonestyCertificationReport
        {
            Marker = "PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION",
            Status = failed == 0 ? "Certified" : "Failed",
            Message = failed == 0
                ? "Recommendation honesty and approval certification passed."
                : "Recommendation honesty and approval certification failed.",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            PassedCases = passed,
            FailedCases = failed,
            Cases = cases.ToArray(),
            RequiredGuardrails = RequiredGuardrails()
        };
    }

    private static P15HonestyCertificationCase CertifyCleanRecommendation()
    {
        var service = new P15RecommendationService();
        var response = service.Generate(service.BuildDemoRequest());
        var recommendation = response.Recommendations.FirstOrDefault();

        var passed = recommendation is not null
            && recommendation.RequiresHumanApproval
            && !recommendation.HasWriteBackPath
            && recommendation.ExpectedImpact is not null
            && recommendation.Evidence.Length > 0
            && recommendation.Provenance.Length > 0
            && recommendation.Status == P15RecommendationStatus.ApprovalRequired;

        return new P15HonestyCertificationCase
        {
            CaseCode = "T101-CLEAN-RECOMMENDATION",
            Title = "Supported recommendation carries expected impact, evidence, provenance and approval requirement",
            ExpectedBehavior = "Recommendation is generated but remains approval-required and has no automatic write-back path.",
            ActualBehavior = recommendation is null ? "No recommendation generated." : $"Status={recommendation.Status}; Approval={recommendation.RequiresHumanApproval}; WriteBack={recommendation.HasWriteBackPath}; Evidence={recommendation.Evidence.Length}; Provenance={recommendation.Provenance.Length}.",
            Passed = passed,
            Violations = passed ? Array.Empty<string>() : new[] { "Supported recommendation did not satisfy honesty contract." }
        };
    }

    private static P15HonestyCertificationCase CertifyCausalLanguageBlocked()
    {
        var candidate = BuildSafeCandidate() with
        {
            Title = "This change guarantees savings",
            AdvisoryText = "This parameter change will cause lower defects and definitely saves money."
        };

        var decision = P15AdvisoryHonestyPolicy.ValidateRecommendation(candidate);
        var passed = !decision.IsAllowed && decision.Violations.Any(item => item.Contains("causal", StringComparison.OrdinalIgnoreCase) || item.Contains("guarantee", StringComparison.OrdinalIgnoreCase) || item.Contains("definitely", StringComparison.OrdinalIgnoreCase));

        return new P15HonestyCertificationCase
        {
            CaseCode = "T101-CAUSAL-LANGUAGE-BLOCKED",
            Title = "Causal or guaranteed-saving language is blocked",
            ExpectedBehavior = "Recommendation with causal/guaranteed wording is rejected.",
            ActualBehavior = decision.Message + " " + string.Join(" ", decision.Violations),
            Passed = passed,
            Violations = decision.Violations
        };
    }

    private static P15HonestyCertificationCase CertifyWeakEvidenceBlocked()
    {
        var candidate = BuildSafeCandidate() with
        {
            EvidenceStrength = P15EvidenceStrength.Weak,
            Evidence = new[]
            {
                new P15EvidenceReference
                {
                    EvidenceId = "weak-evidence-test",
                    EvidenceType = "association-finding",
                    SourceSystem = "certification",
                    Description = "Weak evidence certification case.",
                    Confidence = 0.25m,
                    Strength = P15EvidenceStrength.Weak,
                    Provenance = new[] { "certification" }
                }
            }
        };

        var decision = P15AdvisoryHonestyPolicy.ValidateRecommendation(candidate);
        var passed = !decision.IsAllowed && decision.Violations.Any(item => item.Contains("Weak", StringComparison.OrdinalIgnoreCase));

        return new P15HonestyCertificationCase
        {
            CaseCode = "T101-WEAK-EVIDENCE-BLOCKED",
            Title = "Weak evidence blocks recommendation",
            ExpectedBehavior = "Recommendation with weak evidence is rejected.",
            ActualBehavior = decision.Message + " " + string.Join(" ", decision.Violations),
            Passed = passed,
            Violations = decision.Violations
        };
    }

    private static P15HonestyCertificationCase CertifyOutOfEnvelopeAbstains()
    {
        var scenarioService = new P15ScenarioSimulationService();
        var request = scenarioService.BuildDemoRequest();
        var outOfEnvelope = request with
        {
            Adjustments = request.Adjustments.Select((item, index) => index == 0
                ? item with { ProposedValue = item.MaximumObservedValue + 100m }
                : item).ToArray()
        };

        var response = scenarioService.Simulate(outOfEnvelope);
        var passed = response.SupportStatus == P15SupportStatus.OutOfEnvelope && response.ProjectedValueImpact is null;

        return new P15HonestyCertificationCase
        {
            CaseCode = "T101-OUT-OF-ENVELOPE-ABSTAINS",
            Title = "Out-of-envelope scenario abstains",
            ExpectedBehavior = "Scenario outside observed envelope returns OutOfEnvelope and no projected value impact.",
            ActualBehavior = $"SupportStatus={response.SupportStatus}; ImpactNull={response.ProjectedValueImpact is null}.",
            Passed = passed,
            Violations = passed ? Array.Empty<string>() : new[] { "Out-of-envelope scenario did not abstain." }
        };
    }

    private static P15HonestyCertificationCase CertifyApprovalCommandRequired()
    {
        var service = new P15RecommendationService();
        var invalid = new P15ApprovalCommand
        {
            RecommendationId = "p15-rec-certification",
            ApproverUserId = string.Empty,
            Decision = P15ApprovalDecision.None,
            Comment = string.Empty,
            DecidedAtUtc = DateTimeOffset.UtcNow
        };

        var result = service.Decide(invalid);
        var passed = result.Status == P15RecommendationStatus.Blocked
            && result.Message.Contains("Approval command rejected", StringComparison.OrdinalIgnoreCase);

        return new P15HonestyCertificationCase
        {
            CaseCode = "T101-APPROVAL-COMMAND-REQUIRED",
            Title = "Approval command must be explicit",
            ExpectedBehavior = "Missing approver, decision and comment blocks approval command.",
            ActualBehavior = result.Message,
            Passed = passed,
            Violations = passed ? Array.Empty<string>() : new[] { "Incomplete approval command was not blocked." }
        };
    }

    private static P15HonestyCertificationCase CertifyWriteBackBlocked()
    {
        var candidate = BuildSafeCandidate() with
        {
            HasWriteBackPath = true
        };

        var decision = P15AdvisoryHonestyPolicy.ValidateRecommendation(candidate);
        var passed = !decision.IsAllowed && decision.Violations.Any(item => item.Contains("write-back", StringComparison.OrdinalIgnoreCase));

        return new P15HonestyCertificationCase
        {
            CaseCode = "T101-WRITEBACK-BLOCKED",
            Title = "Automatic write-back path is blocked",
            ExpectedBehavior = "Recommendation exposing automatic write-back path is rejected.",
            ActualBehavior = decision.Message + " " + string.Join(" ", decision.Violations),
            Passed = passed,
            Violations = decision.Violations
        };
    }

    private static P15RecommendationCandidate BuildSafeCandidate() =>
        new()
        {
            RecommendationId = "p15-rec-certification-safe",
            FindingId = "finding-certification",
            Title = "Review guarded parameter window",
            AdvisoryText = "Consider reviewing the suggested range with process engineering. This is projection-only and requires approval.",
            Status = P15RecommendationStatus.ApprovalRequired,
            EvidenceStrength = P15EvidenceStrength.Moderate,
            Confidence = 0.82m,
            ExpectedImpact = new P15MoneyRange
            {
                CurrencyCode = "EUR",
                MinValue = 1000m,
                ExpectedValue = 2500m,
                MaxValue = 4000m
            },
            ParameterWindows = new[]
            {
                new P15RecommendationParameterWindow
                {
                    ParameterCode = "certification_parameter",
                    DisplayName = "Certification parameter",
                    RecommendedMinimum = 10m,
                    RecommendedMaximum = 12m,
                    Unit = "index",
                    Basis = "Certification safe parameter window."
                }
            },
            Evidence = new[]
            {
                new P15EvidenceReference
                {
                    EvidenceId = "cert-evidence-001",
                    EvidenceType = "certification",
                    SourceSystem = "Phase15Certification",
                    Description = "Moderate evidence for certification safe case.",
                    Confidence = 0.82m,
                    Strength = P15EvidenceStrength.Moderate,
                    Provenance = new[] { "phase15-certification" }
                }
            },
            Provenance = new[] { "phase15-certification" },
            HonestyCaveat = P15AdvisoryValueContract.ProjectionOnlyStatement + " " + P15AdvisoryValueContract.AttributionCaveat,
            RequiresHumanApproval = true,
            HasWriteBackPath = false
        };

    private static string[] RequiredGuardrails() =>
        new[]
        {
            "No causal language.",
            "No guaranteed saving claim.",
            "Weak evidence blocks recommendation.",
            "Out-of-envelope scenario abstains.",
            "Approval command must be explicit.",
            "No automatic write-back path."
        };
}

public sealed record P15HonestyCertificationReport
{
    public required string Marker { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public int PassedCases { get; init; }
    public int FailedCases { get; init; }
    public P15HonestyCertificationCase[] Cases { get; init; } = Array.Empty<P15HonestyCertificationCase>();
    public string[] RequiredGuardrails { get; init; } = Array.Empty<string>();
}

public sealed record P15HonestyCertificationCase
{
    public required string CaseCode { get; init; }
    public required string Title { get; init; }
    public required string ExpectedBehavior { get; init; }
    public required string ActualBehavior { get; init; }
    public bool Passed { get; init; }
    public string[] Violations { get; init; } = Array.Empty<string>();
}
