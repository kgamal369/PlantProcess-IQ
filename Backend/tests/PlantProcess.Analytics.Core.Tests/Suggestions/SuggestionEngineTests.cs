using System.Collections.Generic;
using System.Linq;
using PlantProcess.Application.Analytics.Suggestions;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

public class SuggestionEngineTests
{
    private static readonly SuggestionEngine Engine = new();

    private static ApprovedFinding Risk(string subject, decimal low, decimal high, double dq = 0.9, int n = 120, double stab = 0.8, bool synthetic = false)
        => new(FindingKind.Risk, $"finding-{subject}", ProvenanceHandle.Finding($"finding-{subject}"), subject, "EDGE_CRACK", n, stab, dq, low, high, synthetic);

    [Fact]
    public void Identical_findings_produce_identical_cards_stable_ids_and_order()
    {
        var findings = new List<ApprovedFinding> { Risk("C2", 10000, 20000), Risk("C1", 30000, 60000) };
        var a = Engine.Generate(findings);
        var b = Engine.Generate(findings);

        Assert.Equal(a.Select(c => c.Id), b.Select(c => c.Id));
        Assert.Equal(a.Select(c => c.SuggestionKey), b.Select(c => c.SuggestionKey));
        Assert.Equal("finding-C1", a[0].SourceFindingRefs.Single()); // higher impact ranks first
    }

    [Fact]
    public void Every_card_has_a_resolvable_handle_and_a_ranged_impact()
    {
        var cards = Engine.Generate(new List<ApprovedFinding> { Risk("C1", 28000, 56000) });
        Assert.All(cards, c =>
        {
            Assert.NotEmpty(c.EvidenceHandles);
            Assert.False(string.IsNullOrWhiteSpace(c.EvidenceHandles[0].Id));
            Assert.NotNull(c.ImpactLow);
            Assert.NotNull(c.ImpactHigh);
            Assert.True(c.ImpactHigh >= c.ImpactLow);
        });
    }

    [Fact]
    public void Confidence_rises_with_data_quality_sample_and_stability()
    {
        var low = Engine.Generate(new List<ApprovedFinding> { Risk("C1", 1, 2, dq: 0.3, n: 10, stab: 0.3) })[0].Confidence;
        var high = Engine.Generate(new List<ApprovedFinding> { Risk("C1", 1, 2, dq: 0.95, n: 200, stab: 0.95) })[0].Confidence;
        Assert.True(high > low);
    }

    [Fact]
    public void A_finding_with_no_evidence_or_a_seed_finding_is_refused()
    {
        var noHandle = new ApprovedFinding(FindingKind.Risk, "f", new ProvenanceHandle(ProvenanceKind.Finding, ""), "C1", null, 100, 0.8, 0.9, 1, 2);
        var seed = Risk("C9", 1, 2, synthetic: true);
        Assert.Empty(Engine.Generate(new List<ApprovedFinding> { noHandle }));
        Assert.Empty(Engine.Generate(new List<ApprovedFinding> { seed }));
    }

    [Fact]
    public void Card_surfaces_population_and_method_from_the_finding()
    {
        var finding = new ApprovedFinding(
            FindingKind.Correlation, "finding-CM", ProvenanceHandle.Finding("finding-CM"),
            "param_pressure", "EDGE_CRACK", SampleSize: 142, Stability: 0.8, DataQuality: 0.9,
            ImpactLow: 10000, ImpactHigh: 25000, IsSynthetic: false, Method: "Spearman");

        var card = Engine.Generate(new List<ApprovedFinding> { finding }).Single();

        Assert.Equal(142, card.Population);
        Assert.Equal("Spearman", card.Method);
        Assert.NotNull(card.ImpactLow);
        Assert.NotNull(card.ImpactHigh);
    }
}