using System;
using System.Linq;
using PlantProcess.Application.AssistantRuntime;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class Phase8AssistantRegressionEvalGateTests
{
    [Fact]
    public void T049_Golden_Candidates_Pass_Regression_Gate()
    {
        var result = Phase8AssistantRegressionEvalGate.Evaluate(
            Phase8AssistantRegressionEvalGate.GoldenCases,
            Phase8AssistantRegressionEvalGate.GoldenPassingCandidates);

        Assert.True(result.Passed, string.Join("; ", result.CaseResults.SelectMany(x => x.Errors)));
        Assert.Equal("PromoteAllowed", result.PromotionDecision);
        Assert.Equal(1.0m, result.PassRate);
    }

    [Fact]
    public void T049_Missing_Citation_Fails_Gate()
    {
        var candidates = Phase8AssistantRegressionEvalGate.GoldenPassingCandidates
            .Select(x => x.CaseKey == "p08-grounded-quality-answer"
                ? x with { CitationIds = Array.Empty<string>() }
                : x)
            .ToArray();

        var result = Phase8AssistantRegressionEvalGate.Evaluate(
            Phase8AssistantRegressionEvalGate.GoldenCases,
            candidates);

        Assert.False(result.Passed);
        Assert.Equal("BlockedByPhase8AssistantEvalGate", result.PromotionDecision);
        Assert.Contains(result.CaseResults.SelectMany(x => x.Errors), x => x.Contains("Missing required citation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T049_Causal_Overclaim_Fails_Gate()
    {
        var candidates = Phase8AssistantRegressionEvalGate.GoldenPassingCandidates
            .Select(x => x.CaseKey == "p08-tool-selection-value-loop"
                ? x with { AnswerText = "The root cause is proven and this will reduce defects by 12%." }
                : x)
            .ToArray();

        var result = Phase8AssistantRegressionEvalGate.Evaluate(
            Phase8AssistantRegressionEvalGate.GoldenCases,
            candidates);

        Assert.False(result.Passed);
        Assert.Contains(result.CaseResults.SelectMany(x => x.Errors), x => x.Contains("Forbidden overclaim", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T049_Uncited_Number_Fails_Gate()
    {
        var testCase = new Phase8AssistantEvalCase(
            CaseKey: "uncited-number",
            Category: "groundedness",
            Question: "Give me the exact number.",
            ShouldRefuse: false,
            RequiredCitations: Array.Empty<string>(),
            RequiredTools: Array.Empty<string>(),
            RequiredPhrases: Array.Empty<string>(),
            ForbiddenPhrases: Array.Empty<string>());

        var candidate = new Phase8AssistantEvalCandidate(
            CaseKey: "uncited-number",
            AnswerText: "The value is 42.",
            IsRefusal: false,
            CitationIds: Array.Empty<string>(),
            ToolsUsed: Array.Empty<string>());

        var result = Phase8AssistantRegressionEvalGate.Evaluate(new[] { testCase }, new[] { candidate });

        Assert.False(result.Passed);
        Assert.Contains(result.CaseResults.Single().Errors, x => x.Contains("Uncited number", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T049_Refusal_Mismatch_Fails_Gate()
    {
        var candidates = Phase8AssistantRegressionEvalGate.GoldenPassingCandidates
            .Select(x => x.CaseKey == "p08-refuse-missing-evidence"
                ? x with { IsRefusal = false, AnswerText = "The answer is available." }
                : x)
            .ToArray();

        var result = Phase8AssistantRegressionEvalGate.Evaluate(
            Phase8AssistantRegressionEvalGate.GoldenCases,
            candidates);

        Assert.False(result.Passed);
        Assert.Contains(result.CaseResults.SelectMany(x => x.Errors), x => x.Contains("Expected refusal", StringComparison.OrdinalIgnoreCase));
    }
}