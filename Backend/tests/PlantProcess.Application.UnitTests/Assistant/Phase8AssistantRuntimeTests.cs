
using PlantProcess.Application.AssistantRuntime;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class Phase8AssistantRuntimeTests
{
    [Fact]
    public void T047_Assistant_Configuration_Normalizes_Unsafe_Values()
    {
        var input = new Phase8AssistantConfiguration(
            Mode: "unsafe-open-ended",
            GroundingPolicy: "anything-goes",
            EvidencePolicy: "",
            NoEgress: false,
            MaxCitations: 99,
            AllowedTools: new[] { "material-investigation", "shell-access" },
            RequireHumanApprovalForRecommendations: true,
            EnableSuggestionWorkflow: true,
            UpdatedBy: "tester",
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        var result = Phase8AssistantConfigurationValidator.ValidateAndNormalize(input, "tester");

        Assert.Equal("grounded-extractive", result.Normalized.Mode);
        Assert.Equal("strict-citations-required", result.Normalized.GroundingPolicy);
        Assert.Equal("citations-and-provenance-required", result.Normalized.EvidencePolicy);
        Assert.Equal(12, result.Normalized.MaxCitations);
        Assert.Contains("material-investigation", result.Normalized.AllowedTools);
        Assert.DoesNotContain("shell-access", result.Normalized.AllowedTools);
        Assert.NotEmpty(result.Findings);
    }

    [Fact]
    public void T045_Suggestion_Engine_Returns_Guarded_Recommendation_With_Value_Range()
    {
        var config = Phase8AssistantConfiguration.Default("test");
        var response = Phase8SuggestionRecommendationEngine.Generate(
            new Phase8SuggestionRequest(
                Scope: "demo",
                OutcomeKey: "defect.edge_crack_rate",
                MaterialScope: "coil",
                MinimumConfidence: 0.74m,
                IncludeValueProjection: true),
            config);

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Equal("Ready", response.Status);
        Assert.True(recommendation.ExpectedValueLow <= recommendation.ExpectedValueExpected);
        Assert.True(recommendation.ExpectedValueExpected <= recommendation.ExpectedValueHigh);
        Assert.True(recommendation.RequiresHumanApproval);
        Assert.Contains(recommendation.Guardrails, x => x.Contains("No causal claim", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("not causal proof", response.HonestyCaveat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T045_Suggestion_Engine_Blocks_When_Hmi_Config_Disables_Workflow()
    {
        var config = Phase8AssistantConfiguration.Default("test") with
        {
            EnableSuggestionWorkflow = false
        };

        var response = Phase8SuggestionRecommendationEngine.Generate(
            new Phase8SuggestionRequest("demo", "defect.edge_crack_rate", "coil", 0.7m, true),
            config);

        Assert.Equal("Blocked", response.Status);
        Assert.Empty(response.Recommendations);
    }
}
