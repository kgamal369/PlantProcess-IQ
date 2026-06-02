using System.Collections.Generic;
using System.Linq;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

public class GroundingContractTests
{
    private static AssistantClaim Claim(string text, params string[] numbers)
        => new(text, ProvenanceHandle.Finding("f1"), numbers, false);

    [Fact]
    public void An_uncited_number_is_blocked_and_never_reaches_the_client()
    {
        var draft = "The defect rate is 0.137. The secret figure is 999.42.";
        var claims = new List<AssistantClaim> { Claim("defect rate 0.137", "0.137") };

        var answer = GroundingService.Enforce(draft, claims);

        Assert.False(answer.IsRefusal);
        Assert.Contains("0.137", answer.Text);
        Assert.DoesNotContain("999.42", answer.Text);          // number never surfaced
        Assert.Contains(answer.BlockedSentences, s => s.Contains("999.42"));
    }

    [Fact]
    public void A_root_cause_assertion_is_blocked()
    {
        var draft = "The root cause is the caster nozzle. The defect is associated with high speed.";
        var claims = new List<AssistantClaim> { Claim("associated with high speed") };

        var answer = GroundingService.Enforce(draft, claims);

        Assert.DoesNotContain("root cause", answer.Text.ToLowerInvariant());
        Assert.Contains("associated", answer.Text.ToLowerInvariant());
    }

    [Fact]
    public void Insufficient_evidence_yields_an_honest_refusal_not_a_guess()
    {
        var answer = GroundingService.Enforce("Anything at all with a 5.", new List<AssistantClaim>());
        Assert.True(answer.IsRefusal);
        Assert.Contains("evidence", answer.RefusalReason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_seed_backed_claim_is_never_surfaced()
    {
        var seed = new AssistantClaim("synthetic 12.5", ProvenanceHandle.SourceTable("public.demo_seed"), new[] { "12.5" }, IsSynthetic: true);
        var answer = GroundingService.Enforce("The synthetic figure is 12.5.", new List<AssistantClaim> { seed });
        Assert.True(answer.IsRefusal);          // only claim was synthetic -> no grounding
        Assert.DoesNotContain("12.5", answer.Text);
    }

    [Fact]
    public void Every_surfaced_number_traces_to_a_claim_handle()
    {
        var draft = "The score is 73. The trend is 12 over the window.";
        var claims = new List<AssistantClaim>
        {
            new("score 73", ProvenanceHandle.Finding("risk-1"), new[] { "73" }),
            new("trend 12", ProvenanceHandle.Finding("trend-1"), new[] { "12" }),
        };
        var answer = GroundingService.Enforce(draft, claims);
        Assert.False(answer.IsRefusal);
        Assert.NotEmpty(answer.Citations);                     // numbers came from cited claims, not the model
        Assert.All(answer.Citations, h => Assert.False(string.IsNullOrWhiteSpace(h.Id)));
    }

    [Fact]
    public void Extractive_model_frames_associations_and_authorises_its_numbers()
    {
        var model = new ExtractiveAssistantModel();
        var chunks = new List<RetrievedChunk>
        {
            new(System.Guid.NewGuid(), "finding", "f1", "edge-crack rate was 0.137 on line A", ProvenanceHandle.Finding("f1"), 0.9)
        };
        var draft = model.Draft(new AssistantRequest(System.Guid.NewGuid(), "engineer", "", "why edge cracks?", new string[0]), chunks, new List<ToolResult>());
        var answer = GroundingService.Enforce(draft.Text, draft.Claims);

        Assert.False(answer.IsRefusal);
        Assert.Contains("0.137", answer.Text);                 // sourced number survives
        Assert.DoesNotContain("root cause", answer.Text.ToLowerInvariant());
    }
}