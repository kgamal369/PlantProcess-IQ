
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.
/// Certifies that the assistant eval gate fails on uncited numbers,
/// unsupported causal claims, blocked sentences, and provider/model drift.
/// </summary>
public sealed class Phase9_T048AssistantGroundingEvalGateTests
{
    private const string ProviderKey = AssistantGroundingEvalPromptSet.ProviderKey;
    private const string ModelKey = AssistantGroundingEvalPromptSet.ModelKey;
    private const string ModelVersion = AssistantGroundingEvalPromptSet.ModelVersion;

    private static readonly ProvenanceHandle EdgeFinding =
        ProvenanceHandle.Finding("finding-edge-caster-a", "edge-crack suggestion");

    private static AssistantClaim EdgeClaim(params string[] numbers)
        => new(
            Text: "Approved finding supports the edge-crack projected range.",
            Handle: EdgeFinding,
            NumericTokens: numbers);

    private static AssistantGroundingEvalCase Case(
        bool expectedAnswerable = true,
        IReadOnlyList<string>? forbiddenNumbers = null,
        string provider = ProviderKey,
        string model = ModelKey,
        string version = ModelVersion)
        => new(
            CaseKey: "case-under-test",
            Prompt: "Explain the approved suggestion.",
            ExpectedAnswerable: expectedAnswerable,
            RequiredCitationTokens: expectedAnswerable ? new[] { EdgeFinding.Token } : Array.Empty<string>(),
            ForbiddenNumbers: forbiddenNumbers ?? Array.Empty<string>(),
            PinnedProviderKey: provider,
            PinnedModelKey: model,
            PinnedModelVersion: version);

    private static GroundedAssistantGatewayResult Certify(
        string output,
        IReadOnlyList<AssistantClaim> claims,
        string provider = ProviderKey,
        string model = ModelKey,
        string version = ModelVersion)
        => GroundedAssistantGateway.Certify(
            prompt: "Explain the approved suggestion.",
            modelOutput: output,
            retrievedClaims: claims,
            providerKey: provider,
            modelKey: model,
            modelVersion: version);

    [Fact]
    public void T048_Clean_Grounded_Model_Output_Passes_Eval_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR based on the cited finding.",
            new[] { EdgeClaim("42000") });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(result.IsRefusal);
        Assert.True(result.GroundingCertified);
        Assert.Empty(result.BlockedSentences);
        Assert.Contains(EdgeFinding, result.Citations);
        Assert.True(evaluation.Passed, string.Join(" | ", evaluation.Errors));
    }

    [Fact]
    public void T048_Uncited_Number_Is_Blocked_And_Fails_Regression_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR. The assistant also estimates 99,999 EUR.",
            new[] { EdgeClaim("42000") });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(
            Case(forbiddenNumbers: new[] { "99999", "99,999" }),
            result);

        Assert.False(result.IsRefusal);
        Assert.DoesNotContain("99,999", result.Text);
        Assert.Contains(result.BlockedSentences, s => s.Contains("99,999", StringComparison.OrdinalIgnoreCase));
        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("blocked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Unsupported_Causal_Claim_Is_Blocked_And_Fails_Regression_Gate()
    {
        var result = Certify(
            "EDGE_CRACK is caused by caster-a. The approved projected value is 42,000 EUR.",
            new[] { EdgeClaim("42000") });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(result.IsRefusal);
        Assert.DoesNotContain("is caused by", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.BlockedSentences, s => s.Contains("is caused by", StringComparison.OrdinalIgnoreCase));
        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Synthetic_Only_Evidence_Produces_Honest_Refusal_And_Passes_Refusal_Case()
    {
        var synthetic = new AssistantClaim(
            Text: "Seed finding claims value 42,000.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "42000" },
            IsSynthetic: true);

        var result = Certify(
            "The value is 42,000 EUR.",
            new[] { synthetic });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(
            Case(expectedAnswerable: false),
            result);

        Assert.True(result.IsRefusal);
        Assert.False(result.GroundingCertified);
        Assert.True(evaluation.Passed, string.Join(" | ", evaluation.Errors));
    }

    [Fact]
    public void T048_Model_Version_Drift_Fails_Eval_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR based on the cited finding.",
            new[] { EdgeClaim("42000") },
            version: "unexpected-model-version");

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("Model version drift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Provider_And_Model_Key_Drift_Fail_Eval_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR based on the cited finding.",
            new[] { EdgeClaim("42000") },
            provider: "external-provider",
            model: "different-model");

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("Provider drift", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evaluation.Errors, e => e.Contains("Model key drift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Fixed_Prompt_Set_Is_Pinned_And_Contains_Regression_Cases()
    {
        var cases = AssistantGroundingEvalPromptSet.Default;

        Assert.Equal(3, cases.Count);
        Assert.Contains(cases, c => c.CaseKey == "answer_value_range_with_citation");
        Assert.Contains(cases, c => c.CaseKey == "block_uncited_number");
        Assert.Contains(cases, c => c.CaseKey == "refuse_without_live_evidence");

        Assert.All(cases, c =>
        {
            Assert.Equal(ProviderKey, c.PinnedProviderKey);
            Assert.Equal(ModelKey, c.PinnedModelKey);
            Assert.Equal(ModelVersion, c.PinnedModelVersion);
            Assert.False(string.IsNullOrWhiteSpace(c.Prompt));
        });
    }

    [Fact]
    public void T048_EvaluateMany_Fails_Build_Gate_When_Any_Case_Fails()
    {
        var gate = new AssistantGroundingEvalGate();
        var cases = AssistantGroundingEvalPromptSet.Default;

        var results = gate.EvaluateMany(cases, testCase =>
        {
            if (testCase.CaseKey == "block_uncited_number")
            {
                return Certify(
                    "The approved projected value is 42,000 EUR. The assistant also estimates 99,999 EUR.",
                    new[] { EdgeClaim("42000") });
            }

            if (testCase.CaseKey == "refuse_without_live_evidence")
            {
                return GroundedAssistantGateway.RefuseNoEvidence(
                    testCase.Prompt,
                    ProviderKey,
                    ModelKey,
                    ModelVersion);
            }

            return Certify(
                "The approved projected value is 42,000 EUR based on the cited finding.",
                new[] { EdgeClaim("42000") });
        });

        Assert.Contains(results, r => !r.Passed && r.CaseKey == "block_uncited_number");
        Assert.Contains(results, r => r.Passed && r.CaseKey == "answer_value_range_with_citation");
        Assert.Contains(results, r => r.Passed && r.CaseKey == "refuse_without_live_evidence");
    }
}
