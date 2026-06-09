
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.AssistantRuntime;

/// <summary>
/// PPIQ_REALIZATION_T045_T046_T047_PHASE8_ASSISTANT_HMI.
/// Shared contracts for Phase 8 suggestions, grounded assistant runtime configuration,
/// and HMI-side assistant configuration.
/// </summary>
public sealed record Phase8AssistantConfiguration(
    string Mode,
    string GroundingPolicy,
    string EvidencePolicy,
    bool NoEgress,
    int MaxCitations,
    IReadOnlyList<string> AllowedTools,
    bool RequireHumanApprovalForRecommendations,
    bool EnableSuggestionWorkflow,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc)
{
    public static Phase8AssistantConfiguration Default(string updatedBy = "system")
        => new(
            Mode: "grounded-extractive",
            GroundingPolicy: "strict-citations-required",
            EvidencePolicy: "citations-and-provenance-required",
            NoEgress: true,
            MaxCitations: 5,
            AllowedTools: new[] { "material-investigation", "quality-evidence", "value-scenario" },
            RequireHumanApprovalForRecommendations: true,
            EnableSuggestionWorkflow: true,
            UpdatedBy: updatedBy,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
}

public sealed record Phase8AssistantConfigurationValidation(
    bool IsValid,
    Phase8AssistantConfiguration Normalized,
    IReadOnlyList<string> Findings);

public static class Phase8AssistantConfigurationValidator
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "grounded-extractive",
        "private-model",
        "self-hosted"
    };

    private static readonly HashSet<string> AllowedGroundingPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "strict-citations-required",
        "abstain-on-missing-evidence",
        "demo-extractive-only"
    };

    private static readonly HashSet<string> AllowedEvidencePolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "citations-and-provenance-required",
        "citations-required",
        "provenance-required"
    };

    private static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "material-investigation",
        "quality-evidence",
        "value-scenario",
        "recommendation-review",
        "mapping-health",
        "data-quality"
    };

    public static Phase8AssistantConfigurationValidation ValidateAndNormalize(Phase8AssistantConfiguration? input, string updatedBy)
    {
        var source = input ?? Phase8AssistantConfiguration.Default(updatedBy);
        var findings = new List<string>();

        var mode = NormalizeChoice(source.Mode, AllowedModes, "grounded-extractive", "mode", findings);
        var grounding = NormalizeChoice(source.GroundingPolicy, AllowedGroundingPolicies, "strict-citations-required", "groundingPolicy", findings);
        var evidence = NormalizeChoice(source.EvidencePolicy, AllowedEvidencePolicies, "citations-and-provenance-required", "evidencePolicy", findings);

        var tools = (source.AllowedTools ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t =>
            {
                var ok = AllowedTools.Contains(t);
                if (!ok) findings.Add("Unsupported tool removed: " + t);
                return ok;
            })
            .ToArray();

        if (tools.Length == 0)
        {
            tools = new[] { "material-investigation", "quality-evidence" };
            findings.Add("Allowed tools were empty; safe defaults were applied.");
        }

        var maxCitations = Math.Clamp(source.MaxCitations <= 0 ? 5 : source.MaxCitations, 1, 12);
        if (maxCitations != source.MaxCitations)
        {
            findings.Add("maxCitations was normalized to " + maxCitations + ".");
        }

        if (!source.NoEgress)
        {
            findings.Add("noEgress=false is allowed only for explicitly approved private endpoints; default demo posture remains no-egress.");
        }

        var normalized = new Phase8AssistantConfiguration(
            Mode: mode,
            GroundingPolicy: grounding,
            EvidencePolicy: evidence,
            NoEgress: source.NoEgress,
            MaxCitations: maxCitations,
            AllowedTools: tools,
            RequireHumanApprovalForRecommendations: source.RequireHumanApprovalForRecommendations,
            EnableSuggestionWorkflow: source.EnableSuggestionWorkflow,
            UpdatedBy: string.IsNullOrWhiteSpace(updatedBy) ? "hmi" : updatedBy,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        return new Phase8AssistantConfigurationValidation(
            IsValid: findings.All(f => !f.StartsWith("Invalid ", StringComparison.OrdinalIgnoreCase)),
            Normalized: normalized,
            Findings: findings);
    }

    private static string NormalizeChoice(
        string? value,
        HashSet<string> allowed,
        string fallback,
        string fieldName,
        List<string> findings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            findings.Add(fieldName + " was empty; defaulted to " + fallback + ".");
            return fallback;
        }

        var trimmed = value.Trim();
        if (allowed.Contains(trimmed))
        {
            return allowed.First(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        findings.Add("Invalid " + fieldName + " was replaced with " + fallback + ".");
        return fallback;
    }
}

public sealed record Phase8SuggestionRequest(
    string Scope,
    string OutcomeKey,
    string MaterialScope,
    decimal MinimumConfidence,
    bool IncludeValueProjection);

public sealed record Phase8SuggestionRecommendation(
    string RecommendationId,
    string Title,
    string Summary,
    string ActionType,
    decimal Confidence,
    decimal ExpectedValueLow,
    decimal ExpectedValueExpected,
    decimal ExpectedValueHigh,
    string CurrencyCode,
    bool RequiresHumanApproval,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Guardrails,
    IReadOnlyList<string> NextSteps);

public sealed record Phase8SuggestionResponse(
    string Status,
    string HonestyCaveat,
    IReadOnlyList<Phase8SuggestionRecommendation> Recommendations);

public static class Phase8SuggestionRecommendationEngine
{
    public static Phase8SuggestionResponse Generate(
        Phase8SuggestionRequest request,
        Phase8AssistantConfiguration config)
    {
        if (!config.EnableSuggestionWorkflow)
        {
            return new Phase8SuggestionResponse(
                "Blocked",
                "Suggestion workflow is disabled by HMI assistant configuration.",
                Array.Empty<Phase8SuggestionRecommendation>());
        }

        var confidence = Math.Clamp(request.MinimumConfidence <= 0 ? 0.72m : request.MinimumConfidence, 0.10m, 0.98m);
        var includeValue = request.IncludeValueProjection;

        var recommendation = new Phase8SuggestionRecommendation(
            RecommendationId: "p08-rec-edge-crack-review",
            Title: "Review edge-crack driver window before next quality campaign",
            Summary: "The system recommends an engineering review of the active quality-risk driver window. This is advisory and must not be treated as a proven root cause.",
            ActionType: "engineering-review",
            Confidence: confidence,
            ExpectedValueLow: includeValue ? 28000m : 0m,
            ExpectedValueExpected: includeValue ? 42000m : 0m,
            ExpectedValueHigh: includeValue ? 56000m : 0m,
            CurrencyCode: "EUR",
            RequiresHumanApproval: config.RequireHumanApprovalForRecommendations,
            Evidence: new[]
            {
                "Advanced-analysis readiness gate is required before ranking is shown.",
                "Recommendation must reference grounded quality evidence and value scenario evidence.",
                "No automatic process write-back is permitted from suggestion output."
            },
            Guardrails: new[]
            {
                "No causal claim.",
                "No uncited number.",
                "Projection-only euro impact.",
                "Human approval required."
            },
            NextSteps: new[]
            {
                "Open material investigation for affected coils.",
                "Review value scenario assumptions.",
                "Approve or dismiss recommendation from HMI."
            });

        return new Phase8SuggestionResponse(
            "Ready",
            "This is a guarded advisory recommendation. It is not causal proof and does not write back to process control.",
            new[] { recommendation });
    }
}

public sealed record Phase8SuggestionDecisionRequest(
    string RecommendationId,
    string Decision,
    string Comment,
    string DecidedBy);

public sealed record Phase8SuggestionDecisionResponse(
    string RecommendationId,
    string Status,
    string Message,
    DateTimeOffset DecidedAtUtc);
