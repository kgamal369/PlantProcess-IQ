
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Assistant.ModelGateway;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP.
/// Final Phase 09 assistant regression sweep:
/// suggestions, grounding, private model gateway, citations, and refusal behavior.
/// </summary>
public sealed class Phase9_T050AssistantRegressionSweepTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly ProvenanceHandle EdgeFinding =
        ProvenanceHandle.Finding("finding-edge-caster-a", "approved edge-crack value suggestion");

    [Fact]
    public void T050_Demo_Assistant_Answers_Approved_Question_With_Citation()
    {
        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR for the edge-crack suggestion.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "What is the projected value range for the approved edge-crack suggestion?",
            modelOutput: "Approved projected value range is 28,000 to 56,000 EUR.",
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        Assert.False(result.IsRefusal);
        Assert.True(result.GroundingCertified);
        Assert.Contains("28,000", result.Text);
        Assert.Contains("56,000", result.Text);
        Assert.Contains(result.Citations, c => c.Id == EdgeFinding.Id);
        Assert.Empty(result.BlockedSentences);
    }

    [Fact]
    public void T050_Assistant_Blocks_Invented_Number_In_Demo_Response()
    {
        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR for the edge-crack suggestion.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "Give me the approved range and any extra estimate.",
            modelOutput: "Approved projected value range is 28,000 to 56,000 EUR. The assistant also estimates 99,999 EUR.",
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        Assert.False(result.IsRefusal);
        Assert.DoesNotContain("99,999", result.Text);
        Assert.Contains(result.BlockedSentences, s => s.Contains("99,999", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Citations, c => c.Id == EdgeFinding.Id);
    }

    [Fact]
    public async Task T050_SelfHosted_NoEgress_Gateway_Can_Feed_Grounded_Assistant_Demo()
    {
        var transport = new CapturingTransport();
        var gateway = new PrivateModelGatewayService(transport);

        var evidence = new[]
        {
            new ScopedEvidenceChunk(
                Handle: EdgeFinding.Token,
                Text: "Approved projected value range is 28,000 to 56,000 EUR.",
                SourceTable: "raw_hsm_rows",
                RawPlantRowJson: """{"secret":"must-not-egress"}""")
        };

        var gatewayResult = await gateway.AskAsync(
            new PrivateModelGatewayRequest(
                TenantId,
                "What is the approved value range?",
                evidence,
                new PrivateModelGatewayEndpoint(
                    EndpointCode: "self-hosted-no-egress",
                    ServingMode: PrivateModelServingMode.SelfHostedNoEgress,
                    ProviderType: "self-hosted-local",
                    ModelName: "ppiq-local-extractive",
                    ModelVersion: "phase09-t050",
                    EndpointUri: null,
                    ZeroDataRetentionConfirmed: true,
                    CustomerOwnedEndpoint: true,
                    NetworkBoundary: "local-process"),
                new PrivateModelGatewayTenantPolicy(
                    TenantId,
                    NoEgress: true,
                    AllowPrivateEndpoint: false,
                    AllowBringYourOwnModel: false)),
            CancellationToken.None);

        Assert.True(gatewayResult.Allowed);
        Assert.False(gatewayResult.OutboundCallAttempted);
        Assert.Equal(0, transport.CallCount);
        Assert.Contains("No outbound call was made", gatewayResult.Answer);

        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var grounded = GroundedAssistantGateway.Certify(
            prompt: "What is the approved value range?",
            modelOutput: gatewayResult.Answer,
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        Assert.False(grounded.IsRefusal);
        Assert.True(grounded.GroundingCertified);
        Assert.Contains(grounded.Citations, c => c.Id == EdgeFinding.Id);
        Assert.DoesNotContain("must-not-egress", grounded.Text, StringComparison.OrdinalIgnoreCase);
    }

        [Fact]
    public void T050_Eval_Gate_Fails_If_Assistant_Tries_Causal_Or_Value_Overclaim()
    {
        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "Explain why this happened.",
            modelOutput: "The root cause is caster-a and it will save 56,000 EUR.",
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        var evalCase = new AssistantGroundingEvalCase(
            CaseKey: "t050_no_causal_value_overclaim",
            Prompt: "Explain why this happened.",
            ExpectedAnswerable: true,
            RequiredCitationTokens: new[] { EdgeFinding.Token },
            ForbiddenNumbers: Array.Empty<string>(),
            PinnedProviderKey: AssistantGroundingEvalPromptSet.ProviderKey,
            PinnedModelKey: AssistantGroundingEvalPromptSet.ModelKey,
            PinnedModelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        var evaluation = new AssistantGroundingEvalGate().Evaluate(evalCase, result);

        // T-050 accepts either strict refusal or sentence-level blocking.
        // The certified guardrail is that unsafe causal/value overclaim must not pass.
        Assert.False(evaluation.Passed);
        Assert.True(
            result.IsRefusal || !result.GroundingCertified || result.BlockedSentences.Count > 0,
            "Unsafe causal/value overclaim must be refused, uncertified, or sentence-blocked.");

        Assert.DoesNotContain("root cause", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("will save", result.Text, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            evaluation.Errors,
            e => e.Contains("refused", StringComparison.OrdinalIgnoreCase)
              || e.Contains("blocked", StringComparison.OrdinalIgnoreCase)
              || e.Contains("certification", StringComparison.OrdinalIgnoreCase)
              || e.Contains("Unsupported", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingTransport : IPrivateModelGatewayTransport
    {
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(
            Uri endpointUri,
            PrivateModelGatewayPayload payload,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult("This should not be called in no-egress mode.");
        }
    }
}
