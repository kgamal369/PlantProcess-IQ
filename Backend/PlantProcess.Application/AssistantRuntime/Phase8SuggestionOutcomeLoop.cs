using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.AssistantRuntime;

/// <summary>
/// PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP.
/// Records suggestion action outcomes with honesty caveats and feeds the value/ROI loop
/// without causal attribution.
/// </summary>
public sealed record Phase8SuggestionOutcomeInput(
    string RecommendationId,
    string ActionTaken,
    string OutcomeKpi,
    decimal? BeforeValue,
    decimal? AfterValue,
    bool LowerIsBetter,
    IReadOnlyList<string>? EvidenceRefs,
    string Actor,
    string? Comment);

public sealed record Phase8SuggestionOutcomeRecord(
    Guid OutcomeId,
    string RecommendationId,
    string ActionTaken,
    string OutcomeKpi,
    decimal? BeforeValue,
    decimal? AfterValue,
    decimal? Delta,
    string OutcomeDirection,
    decimal Confidence,
    string Actor,
    DateTimeOffset RecordedAtUtc,
    IReadOnlyList<string> EvidenceRefs,
    string OutcomeCaveat,
    string ValueLoopCaveat,
    bool CausalClaimMade);

public sealed record Phase8SuggestionValueLoopEntry(
    Guid OutcomeId,
    string RecommendationId,
    string OutcomeKpi,
    string OutcomeDirection,
    decimal? ObservedDelta,
    decimal Confidence,
    string AttributionCaveat,
    IReadOnlyList<string> EvidenceRefs);

public sealed record Phase8SuggestionValueLoopSummary(
    int OutcomeCount,
    int ImprovedCount,
    int RegressedCount,
    int UnmeasuredCount,
    string Caveat,
    IReadOnlyList<Phase8SuggestionValueLoopEntry> Entries);

public static class Phase8SuggestionOutcomeLoop
{
    public const string OutcomeCaveat =
        "Observed outcome after action. This does not prove causation and must be reviewed as an association signal only.";

    public const string ValueLoopCaveat =
        "Value/ROI loop includes this outcome as post-action evidence only; it is not causal attribution.";

    public static Phase8SuggestionOutcomeRecord RecordOutcome(Phase8SuggestionOutcomeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.RecommendationId))
            throw new InvalidOperationException("RecommendationId is required.");

        if (string.IsNullOrWhiteSpace(input.ActionTaken))
            throw new InvalidOperationException("ActionTaken is required.");

        var delta = input.BeforeValue.HasValue && input.AfterValue.HasValue
            ? input.AfterValue.Value - input.BeforeValue.Value
            : (decimal?)null;

        var direction = DetermineDirection(input.BeforeValue, input.AfterValue, input.LowerIsBetter);
        var confidence = DetermineConfidence(input);

        return new Phase8SuggestionOutcomeRecord(
            OutcomeId: Guid.NewGuid(),
            RecommendationId: input.RecommendationId.Trim(),
            ActionTaken: input.ActionTaken.Trim(),
            OutcomeKpi: Normalize(input.OutcomeKpi, "quality_risk"),
            BeforeValue: input.BeforeValue,
            AfterValue: input.AfterValue,
            Delta: delta,
            OutcomeDirection: direction,
            Confidence: confidence,
            Actor: Normalize(input.Actor, "hmi-user"),
            RecordedAtUtc: DateTimeOffset.UtcNow,
            EvidenceRefs: (input.EvidenceRefs ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            OutcomeCaveat: OutcomeCaveat,
            ValueLoopCaveat: ValueLoopCaveat,
            CausalClaimMade: false);
    }

    public static Phase8SuggestionValueLoopSummary BuildValueLoop(IEnumerable<Phase8SuggestionOutcomeRecord> records)
    {
        var rows = records
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new Phase8SuggestionValueLoopEntry(
                x.OutcomeId,
                x.RecommendationId,
                x.OutcomeKpi,
                x.OutcomeDirection,
                x.Delta,
                x.Confidence,
                x.ValueLoopCaveat,
                x.EvidenceRefs))
            .ToArray();

        return new Phase8SuggestionValueLoopSummary(
            OutcomeCount: rows.Length,
            ImprovedCount: rows.Count(x => string.Equals(x.OutcomeDirection, "Improved", StringComparison.OrdinalIgnoreCase)),
            RegressedCount: rows.Count(x => string.Equals(x.OutcomeDirection, "Regressed", StringComparison.OrdinalIgnoreCase)),
            UnmeasuredCount: rows.Count(x => string.Equals(x.OutcomeDirection, "Unmeasured", StringComparison.OrdinalIgnoreCase)),
            Caveat: ValueLoopCaveat,
            Entries: rows);
    }

    private static string DetermineDirection(decimal? before, decimal? after, bool lowerIsBetter)
    {
        if (!before.HasValue || !after.HasValue)
            return "Unmeasured";

        if (before.Value == after.Value)
            return "Unchanged";

        var improved = lowerIsBetter
            ? after.Value < before.Value
            : after.Value > before.Value;

        return improved ? "Improved" : "Regressed";
    }

    private static decimal DetermineConfidence(Phase8SuggestionOutcomeInput input)
    {
        var score = 0.35m;

        if (input.BeforeValue.HasValue && input.AfterValue.HasValue)
            score += 0.30m;

        if ((input.EvidenceRefs ?? Array.Empty<string>()).Any(x => !string.IsNullOrWhiteSpace(x)))
            score += 0.20m;

        if (!string.IsNullOrWhiteSpace(input.Comment))
            score += 0.10m;

        return Math.Clamp(score, 0m, 0.95m);
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}