namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT
/// Phase 15 recommendation generator with expected e-impact, confidence, evidence, provenance and explicit approval workflow.
///
/// Guardrails:
/// - no causal claim
/// - no automatic write-back
/// - weak evidence blocks recommendation
/// - every recommendation requires human approval
/// - expected e-impact is a range and projection-only
/// </summary>
public sealed class P15RecommendationService
{
    public P15RecommendationGenerationResponse Generate(P15RecommendationGenerationRequest request)
    {
        var scenarioService = new P15ScenarioSimulationService();
        var scenario = scenarioService.Simulate(request.ScenarioRequest);

        if (scenario.SupportStatus != P15SupportStatus.Supported || scenario.ProjectedValueImpact is null)
        {
            return new P15RecommendationGenerationResponse
            {
                RequestId = BuildRequestId(request, scenario.Seed),
                ScenarioId = scenario.ScenarioId,
                ScenarioSupportStatus = scenario.SupportStatus,
                Message = "No recommendation generated because scenario support is insufficient or outside the observed envelope.",
                Recommendations = Array.Empty<P15RecommendationCandidate>(),
                Guardrails = DefaultGuardrails()
            };
        }

        var recommendation = BuildCandidate(request, scenario);
        var policy = P15AdvisoryHonestyPolicy.ValidateRecommendation(recommendation);

        recommendation = recommendation with
        {
            Status = policy.IsAllowed ? P15RecommendationStatus.ApprovalRequired : P15RecommendationStatus.Blocked,
            HonestyCaveat = policy.IsAllowed
                ? recommendation.HonestyCaveat
                : recommendation.HonestyCaveat + " Blocked by honesty policy: " + string.Join(" ", policy.Violations)
        };

        return new P15RecommendationGenerationResponse
        {
            RequestId = BuildRequestId(request, scenario.Seed),
            ScenarioId = scenario.ScenarioId,
            ScenarioSupportStatus = scenario.SupportStatus,
            Message = policy.IsAllowed
                ? "Recommendation generated with expected e-impact range and explicit approval requirement."
                : "Recommendation blocked by honesty policy.",
            Recommendations = policy.IsAllowed ? new[] { recommendation } : Array.Empty<P15RecommendationCandidate>(),
            Guardrails = DefaultGuardrails()
        };
    }

    public P15RecommendationGenerationRequest BuildDemoRequest()
    {
        var scenarioService = new P15ScenarioSimulationService();
        return new P15RecommendationGenerationRequest
        {
            TenantId = "demo-tenant",
            PlantId = "demo-plant-01",
            ScenarioRequest = scenarioService.BuildDemoRequest()
        };
    }

    public P15ApprovalResult Decide(P15ApprovalCommand command)
    {
        var policy = P15AdvisoryHonestyPolicy.ValidateApprovalCommand(command);
        if (!policy.IsAllowed)
        {
            return new P15ApprovalResult
            {
                RecommendationId = command.RecommendationId,
                Status = P15RecommendationStatus.Blocked,
                Message = "Approval command rejected: " + string.Join(" ", policy.Violations),
                ApprovalRecordId = BuildApprovalRecordId(command),
                DecidedAtUtc = command.DecidedAtUtc
            };
        }

        return new P15ApprovalResult
        {
            RecommendationId = command.RecommendationId,
            Status = command.Decision == P15ApprovalDecision.Approve
                ? P15RecommendationStatus.Approved
                : P15RecommendationStatus.Dismissed,
            Message = command.Decision == P15ApprovalDecision.Approve
                ? "Recommendation approved for downstream review. No automatic process write-back was executed."
                : "Recommendation dismissed by user. No automatic process write-back was executed.",
            ApprovalRecordId = BuildApprovalRecordId(command),
            DecidedAtUtc = command.DecidedAtUtc
        };
    }

    private static P15RecommendationCandidate BuildCandidate(P15RecommendationGenerationRequest request, P15ScenarioResponse scenario)
    {
        var strongestEvidence = scenario.Evidence.Length == 0 ? P15EvidenceStrength.None : scenario.Evidence.Max(item => item.Strength);
        var confidence = scenario.Evidence.Length == 0 ? 0m : Math.Round(scenario.Evidence.Average(item => item.Confidence), 3, MidpointRounding.AwayFromZero);

        return new P15RecommendationCandidate
        {
            RecommendationId = $"p15-rec-{Sanitize(scenario.FindingId)}-{scenario.Seed}",
            FindingId = scenario.FindingId,
            Title = "Review guarded parameter window for " + scenario.FindingId,
            AdvisoryText = "Consider reviewing the recommended parameter window with process engineering. The expected e-impact is projection-only and requires explicit human approval before operational use.",
            Status = P15RecommendationStatus.ApprovalRequired,
            EvidenceStrength = strongestEvidence,
            Confidence = confidence,
            ExpectedImpact = scenario.ProjectedValueImpact,
            ParameterWindows = request.ScenarioRequest.Adjustments.Select(BuildWindow).ToArray(),
            Evidence = scenario.Evidence,
            Provenance = scenario.Evidence.SelectMany(item => item.Provenance).Append("phase15-recommendation-generator").Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            HonestyCaveat = P15AdvisoryValueContract.ProjectionOnlyStatement + " " + P15AdvisoryValueContract.AttributionCaveat,
            RequiresHumanApproval = true,
            HasWriteBackPath = false
        };
    }

    private static P15RecommendationParameterWindow BuildWindow(P15ParameterAdjustment adjustment)
    {
        var envelopeRange = Math.Max(1m, adjustment.MaximumObservedValue - adjustment.MinimumObservedValue);
        var margin = Math.Round(envelopeRange * 0.05m, 3, MidpointRounding.AwayFromZero);
        var min = Math.Max(adjustment.MinimumObservedValue, adjustment.ProposedValue - margin);
        var max = Math.Min(adjustment.MaximumObservedValue, adjustment.ProposedValue + margin);

        return new P15RecommendationParameterWindow
        {
            ParameterCode = adjustment.ParameterCode,
            DisplayName = adjustment.DisplayName,
            RecommendedMinimum = min,
            RecommendedMaximum = max,
            Unit = adjustment.Unit,
            Basis = "Window centered around supported what-if proposed value and clipped to observed envelope."
        };
    }

    private static string[] DefaultGuardrails() =>
        new[]
        {
            "No causal language.",
            "Expected e-impact is projection-only.",
            "Confidence, evidence and provenance are required.",
            "Weak evidence blocks recommendation.",
            "Human approval is required.",
            "No automatic process write-back."
        };

    private static string BuildRequestId(P15RecommendationGenerationRequest request, int seed) =>
        $"p15-rec-request-{Sanitize(request.ScenarioRequest.FindingId)}-{seed}";

    private static string BuildApprovalRecordId(P15ApprovalCommand command) =>
        $"p15-approval-{Sanitize(command.RecommendationId)}-{command.DecidedAtUtc:yyyyMMddHHmmss}";

    private static string Sanitize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}

public sealed record P15RecommendationGenerationRequest
{
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public required P15ScenarioRequest ScenarioRequest { get; init; }
}

public sealed record P15RecommendationGenerationResponse
{
    public required string RequestId { get; init; }
    public required string ScenarioId { get; init; }
    public P15SupportStatus ScenarioSupportStatus { get; init; }
    public required string Message { get; init; }
    public P15RecommendationCandidate[] Recommendations { get; init; } = Array.Empty<P15RecommendationCandidate>();
    public string[] Guardrails { get; init; } = Array.Empty<string>();
}
