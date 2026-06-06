namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT
/// Central honesty policy for Phase 15 advisory/value features.
///
/// This is deliberately conservative. It blocks causal wording, write-back bypasses,
/// weak-evidence recommendations, and out-of-envelope scenario requests.
/// </summary>
public static class P15AdvisoryHonestyPolicy
{
    private static readonly string[] CausalClaimPhrases =
    {
        "will cause",
        "causes",
        "guarantees",
        "guaranteed",
        "proves causation",
        "root cause is",
        "definitely saves",
        "automatic saving",
        "must change the process"
    };

    public static P15PolicyDecision ValidateScenarioRequest(P15ScenarioRequest request)
    {
        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TenantId))
            violations.Add("TenantId is required.");

        if (string.IsNullOrWhiteSpace(request.PlantId))
            violations.Add("PlantId is required.");

        if (string.IsNullOrWhiteSpace(request.FindingId))
            violations.Add("FindingId is required.");

        if (request.Adjustments.Length == 0)
            violations.Add("At least one parameter adjustment is required.");

        var outOfEnvelope = request.Adjustments
            .Where(adjustment => !adjustment.IsInsideObservedEnvelope)
            .Select(adjustment => adjustment.ParameterCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (outOfEnvelope.Length > 0)
            violations.Add("Out-of-envelope projection must abstain for parameters: " + string.Join(", ", outOfEnvelope));

        return violations.Count == 0
            ? P15PolicyDecision.Allow("Scenario request is inside the observed data envelope.")
            : P15PolicyDecision.Block("Scenario request is not safe for supported projection.", violations);
    }

    public static P15PolicyDecision ValidateRecommendation(P15RecommendationCandidate recommendation)
    {
        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(recommendation.RecommendationId))
            violations.Add("RecommendationId is required.");

        if (recommendation.Confidence < 0m || recommendation.Confidence > 1m)
            violations.Add("Confidence must be between 0 and 1.");

        if (recommendation.EvidenceStrength is P15EvidenceStrength.None or P15EvidenceStrength.Weak)
            violations.Add("Weak or missing evidence must block recommendation.");

        if (recommendation.Evidence.Length == 0)
            violations.Add("Recommendation must include evidence references.");

        if (recommendation.ExpectedImpact is null || !recommendation.ExpectedImpact.IsValid)
            violations.Add("Recommendation must include a valid expected impact range.");

        if (!recommendation.RequiresHumanApproval)
            violations.Add("Recommendation must require explicit human approval.");

        if (recommendation.HasWriteBackPath)
            violations.Add("Recommendation contract must not expose an automatic write-back path.");

        violations.AddRange(FindCausalLanguageViolations(recommendation.Title));
        violations.AddRange(FindCausalLanguageViolations(recommendation.AdvisoryText));
        violations.AddRange(FindCausalLanguageViolations(recommendation.HonestyCaveat));

        return violations.Count == 0
            ? P15PolicyDecision.Allow("Recommendation satisfies honesty and approval guardrails.")
            : P15PolicyDecision.Block("Recommendation failed honesty guardrails.", violations);
    }

    public static P15PolicyDecision ValidateApprovalCommand(P15ApprovalCommand command)
    {
        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(command.RecommendationId))
            violations.Add("RecommendationId is required.");

        if (string.IsNullOrWhiteSpace(command.ApproverUserId))
            violations.Add("ApproverUserId is required.");

        if (command.Decision == P15ApprovalDecision.None)
            violations.Add("Decision must be approve or dismiss.");

        if (string.IsNullOrWhiteSpace(command.Comment))
            violations.Add("Approval/dismissal comment is required.");

        return violations.Count == 0
            ? P15PolicyDecision.Allow("Approval command is valid.")
            : P15PolicyDecision.Block("Approval command is incomplete.", violations);
    }

    public static P15PolicyDecision ValidateBenchmarkVisibility(P15BenchmarkRequest request, int cohortSize)
    {
        if (cohortSize < request.MinimumCohortSize)
        {
            return P15PolicyDecision.Block(
                "Benchmark suppressed because cohort is below minimum privacy threshold.",
                new[] { $"CohortSize={cohortSize}; MinimumCohortSize={request.MinimumCohortSize}" });
        }

        return P15PolicyDecision.Allow("Benchmark can be shown as anonymized aggregate.");
    }

    public static int BuildStableScenarioSeed(P15ScenarioRequest request)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.TenantId ?? string.Empty);
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.PlantId ?? string.Empty);
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.FindingId ?? string.Empty);
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.ScenarioName ?? string.Empty);
            hash = (hash * 31) + request.Seed;

            foreach (var adjustment in request.Adjustments.OrderBy(item => item.ParameterCode, StringComparer.OrdinalIgnoreCase))
            {
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(adjustment.ParameterCode ?? string.Empty);
                hash = (hash * 31) + adjustment.ProposedValue.GetHashCode();
            }

            return hash == int.MinValue ? P15AdvisoryValueContract.DefaultScenarioSeed : Math.Abs(hash);
        }
    }

    private static IEnumerable<string> FindCausalLanguageViolations(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var phrase in CausalClaimPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                yield return $"Causal/overclaim phrase is not allowed: '{phrase}'.";
        }
    }
}

public sealed record P15PolicyDecision
{
    public bool IsAllowed { get; init; }
    public required string Message { get; init; }
    public string[] Violations { get; init; } = Array.Empty<string>();

    public static P15PolicyDecision Allow(string message) =>
        new()
        {
            IsAllowed = true,
            Message = message,
            Violations = Array.Empty<string>()
        };

    public static P15PolicyDecision Block(string message, IEnumerable<string> violations) =>
        new()
        {
            IsAllowed = false,
            Message = message,
            Violations = violations.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
}
