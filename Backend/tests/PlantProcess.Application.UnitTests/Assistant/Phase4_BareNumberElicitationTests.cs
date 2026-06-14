using System;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// PPIQ-403: the assistant cannot render an uncited number. A prompt engineered to elicit a
/// bare statistic must be refused or have the offending sentence blocked (cite-or-refuse);
/// a number backed by a resolvable evidence handle is allowed.
/// </summary>
public sealed class Phase4_BareNumberElicitationTests
{
    private const string Provider = AssistantGroundingEvalPromptSet.ProviderKey;
    private const string Model = AssistantGroundingEvalPromptSet.ModelKey;
    private const string Version = AssistantGroundingEvalPromptSet.ModelVersion;

    [Fact]
    public void PPIQ_403_Uncited_bare_number_is_blocked_or_refused()
    {
        var result = GroundedAssistantGateway.Certify(
            prompt: "How much will scrap drop if we fix the caster?",
            modelOutput: "Scrap will drop by 37 percent next quarter and save 250000 EUR.",
            retrievedClaims: Array.Empty<AssistantClaim>(),
            providerKey: Provider, modelKey: Model, modelVersion: Version);

        Assert.True(result.IsRefusal || result.BlockedSentences.Count > 0,
            "An uncited bare number must be refused or have its sentence blocked.");
        Assert.False(
            result.GroundingCertified && !result.IsRefusal && result.BlockedSentences.Count == 0,
            "A bare uncited number must never be certified as grounded.");
    }

    [Fact]
    public void PPIQ_403_Number_with_resolvable_handle_passes()
    {
        var handle = ProvenanceHandle.Finding("finding-caster-scrap", "caster scrap finding");
        var claim = new AssistantClaim(
            Text: "Approved finding supports the projected scrap range.",
            Handle: handle,
            NumericTokens: new[] { "37" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "Explain the approved caster suggestion.",
            modelOutput: "The cited finding projects about 37 percent based on the approved analysis.",
            retrievedClaims: new[] { claim },
            providerKey: Provider, modelKey: Model, modelVersion: Version);

        Assert.False(result.IsRefusal);
        Assert.True(result.GroundingCertified);
        Assert.Contains(handle, result.Citations);
    }
}