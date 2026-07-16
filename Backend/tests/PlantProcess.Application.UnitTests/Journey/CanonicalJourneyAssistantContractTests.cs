using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.UnitTests.Journey;

/// <summary>
/// Automated evidence for journey step 15: every surfaced number is grounded,
/// causal overclaiming is blocked, synthetic evidence is excluded and refusal is honest.
/// </summary>
public sealed class CanonicalJourneyAssistantContractTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void J15_Supported_numeric_claim_is_preserved_with_a_resolvable_citation()
    {
        var handle = ProvenanceHandle.Finding("finding-superheat-crack");
        var claims = new[]
        {
            new AssistantClaim(
                "Superheat is associated with a 9.3x odds ratio.",
                handle,
                new[] { "9.3" })
        };

        var answer = GroundingService.Enforce(
            "Superheat is associated with a 9.3x odds ratio.",
            claims);

        Assert.False(answer.IsRefusal);
        Assert.Contains("9.3", answer.Text, StringComparison.Ordinal);
        Assert.Contains(handle, answer.Citations);
        Assert.Empty(answer.BlockedSentences);
    }

    [Fact]
    public void J15_Uncited_number_is_blocked_before_it_reaches_the_client()
    {
        var handle = ProvenanceHandle.Finding("finding-approved");
        var claims = new[]
        {
            new AssistantClaim("Approved effect is 2.0.", handle, new[] { "2.0" })
        };

        var answer = GroundingService.Enforce(
            "The effect is 99.9 and therefore certain.",
            claims);

        Assert.True(answer.IsRefusal);
        Assert.DoesNotContain("99.9", answer.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("This is the root cause.")]
    [InlineData("This defect is caused by superheat.")]
    [InlineData("This change will save 100000.")]
    public void J15_Forbidden_causal_or_guaranteed_language_is_blocked(string modelOutput)
    {
        var handle = ProvenanceHandle.Finding("finding-1");
        var answer = GroundingService.Enforce(
            modelOutput,
            new[] { new AssistantClaim(modelOutput, handle, new[] { "100000" }) });

        Assert.True(answer.IsRefusal);
    }

    [Fact]
    public void J15_Synthetic_chunks_do_not_become_customer_evidence()
    {
        var model = new ExtractiveAssistantModel();
        var request = new AssistantRequest(TenantId, "Engineer", "Enterprise", "What drives defects?", Array.Empty<string>());
        var chunks = new[]
        {
            new RetrievedChunk(
                Guid.NewGuid(),
                "FINDING",
                "synthetic-1",
                "A synthetic pattern reports 12.5.",
                ProvenanceHandle.Finding("synthetic-1"),
                0.99,
                IsSynthetic: true)
        };

        var draft = model.Draft(request, chunks, Array.Empty<ToolResult>());
        var answer = GroundingService.Enforce(draft.Text, draft.Claims);

        Assert.Empty(draft.Claims);
        Assert.True(answer.IsRefusal);
    }

    [Fact]
    public void J15_Extractive_baseline_uses_association_language_and_exact_evidence_numbers()
    {
        var model = new ExtractiveAssistantModel();
        var handle = ProvenanceHandle.Finding("finding-superheat");
        var request = new AssistantRequest(TenantId, "Engineer", "Enterprise", "What is associated with CRACK_LONG?", Array.Empty<string>());
        var chunks = new[]
        {
            new RetrievedChunk(
                Guid.NewGuid(),
                "FINDING",
                "finding-superheat",
                "Superheat has effect 0.62 with q 0.004.",
                handle,
                0.98)
        };

        var draft = model.Draft(request, chunks, Array.Empty<ToolResult>());
        var answer = GroundingService.Enforce(draft.Text, draft.Claims);

        Assert.False(answer.IsRefusal);
        Assert.Contains("0.62", answer.Text, StringComparison.Ordinal);
        Assert.Contains("0.004", answer.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("root cause", answer.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(handle, answer.Citations);
    }

    [Fact]
    public void J15_Gateway_certifies_only_non_refusal_answers_with_citations()
    {
        var handle = ProvenanceHandle.Finding("finding-2");
        var result = GroundedAssistantGateway.Certify(
            "question",
            "Evidence reports 4.2.",
            new[] { new AssistantClaim("Evidence reports 4.2.", handle, new[] { "4.2" }) },
            "extractive",
            "safe-default",
            "1");

        Assert.True(result.GroundingCertified);
        Assert.False(result.IsRefusal);
        Assert.Single(result.Citations);
    }
}
