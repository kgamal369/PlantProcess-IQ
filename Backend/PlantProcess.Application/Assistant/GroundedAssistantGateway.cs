using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// PPIQ-T025: gateway contract for model output certification.
/// Any model output, local or external, must pass this gateway before leaving the backend.
/// </summary>
public static class GroundedAssistantGateway
{
    public static GroundedAssistantGatewayResult Certify(
        string prompt,
        string modelOutput,
        IReadOnlyList<AssistantClaim> retrievedClaims,
        string providerKey,
        string modelKey,
        string modelVersion)
    {
        var grounded = GroundingService.Enforce(modelOutput, retrievedClaims);

        return new GroundedAssistantGatewayResult(
            Prompt: prompt,
            Text: grounded.Text,
            Citations: grounded.Citations,
            IsRefusal: grounded.IsRefusal,
            RefusalReason: grounded.RefusalReason,
            BlockedSentences: grounded.BlockedSentences,
            ProviderKey: providerKey,
            ModelKey: modelKey,
            ModelVersion: modelVersion,
            GroundingCertified: !grounded.IsRefusal && grounded.Citations.Count > 0);
    }

    public static GroundedAssistantGatewayResult RefuseNoEvidence(
        string prompt,
        string providerKey,
        string modelKey,
        string modelVersion)
        => new(
            Prompt: prompt,
            Text: string.Empty,
            Citations: Array.Empty<ProvenanceHandle>(),
            IsRefusal: true,
            RefusalReason: "The available evidence does not support a grounded answer.",
            BlockedSentences: Array.Empty<string>(),
            ProviderKey: providerKey,
            ModelKey: modelKey,
            ModelVersion: modelVersion,
            GroundingCertified: false);
}

public sealed record GroundedAssistantGatewayResult(
    string Prompt,
    string Text,
    IReadOnlyList<ProvenanceHandle> Citations,
    bool IsRefusal,
    string? RefusalReason,
    IReadOnlyList<string> BlockedSentences,
    string ProviderKey,
    string ModelKey,
    string ModelVersion,
    bool GroundingCertified);