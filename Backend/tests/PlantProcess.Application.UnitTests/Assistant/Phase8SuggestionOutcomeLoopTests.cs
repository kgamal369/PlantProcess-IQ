using System;
using PlantProcess.Application.AssistantRuntime;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class Phase8SuggestionOutcomeLoopTests
{
    [Fact]
    public void T048_Record_Action_Outcome_Without_Causal_Claim()
    {
        var record = Phase8SuggestionOutcomeLoop.RecordOutcome(new Phase8SuggestionOutcomeInput(
            RecommendationId: "p08-rec-edge-crack-review",
            ActionTaken: "engineering-review-started",
            OutcomeKpi: "edge_crack_rate",
            BeforeValue: 0.034m,
            AfterValue: 0.021m,
            LowerIsBetter: true,
            EvidenceRefs: new[] { "suggestion:p08-rec-edge-crack-review", "value-scenario:edge-crack" },
            Actor: "quality-engineer",
            Comment: "Actioned for review only."));

        Assert.Equal("Improved", record.OutcomeDirection);
        Assert.False(record.CausalClaimMade);
        Assert.Contains("does not prove causation", record.OutcomeCaveat, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not causal attribution", record.ValueLoopCaveat, StringComparison.OrdinalIgnoreCase);
        Assert.True(record.Confidence > 0.7m);
    }

    [Fact]
    public void T048_Value_Loop_Contains_Outcome_With_Caveat()
    {
        var record = Phase8SuggestionOutcomeLoop.RecordOutcome(new Phase8SuggestionOutcomeInput(
            RecommendationId: "rec-1",
            ActionTaken: "actioned",
            OutcomeKpi: "quality_risk",
            BeforeValue: 10,
            AfterValue: 8,
            LowerIsBetter: true,
            EvidenceRefs: new[] { "suggestion-outcome", "value-loop" },
            Actor: "hmi",
            Comment: null));

        var loop = Phase8SuggestionOutcomeLoop.BuildValueLoop(new[] { record });
        var entry = Assert.Single(loop.Entries);

        Assert.Equal(1, loop.OutcomeCount);
        Assert.Equal(1, loop.ImprovedCount);
        Assert.Equal("Improved", entry.OutcomeDirection);
        Assert.Contains("not causal attribution", entry.AttributionCaveat, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not causal attribution", loop.Caveat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T048_Unmeasured_Outcome_Is_Allowed_But_Honest()
    {
        var record = Phase8SuggestionOutcomeLoop.RecordOutcome(new Phase8SuggestionOutcomeInput(
            RecommendationId: "rec-2",
            ActionTaken: "review-created",
            OutcomeKpi: "manual_review",
            BeforeValue: null,
            AfterValue: null,
            LowerIsBetter: true,
            EvidenceRefs: Array.Empty<string>(),
            Actor: "hmi",
            Comment: null));

        Assert.Equal("Unmeasured", record.OutcomeDirection);
        Assert.True(record.Confidence < 0.5m);
        Assert.False(record.CausalClaimMade);
    }
}