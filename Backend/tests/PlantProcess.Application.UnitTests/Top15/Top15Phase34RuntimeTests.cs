using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using PlantProcess.Application.Top15.Phase34;
using Xunit;

namespace PlantProcess.Application.UnitTests.Top15;

public sealed class Top15Phase34RuntimeTests
{
    [Fact]
    public void T209_Real_Model_Adapter_Uses_Configured_Model_And_Keeps_Grounding_Guard()
    {
        var config = new Top15ModelEndpointConfig(
            Endpoint: "https://private-model.local/complete",
            ProviderKey: "private",
            ModelKey: "plant-model",
            ModelVersion: "v1",
            Enabled: true);

        var chunk = new RetrievedChunk(
            Guid.NewGuid(),
            "quality_event",
            "qe-c0044170-01",
            "Coil C-0044170 has 5 cited EDGE_CRACK quality events.",
            ProvenanceHandle.Finding("qe-c0044170-01"),
            1.0);

        var model = new Top15RealAssistantModel(
            config,
            new Top15ScriptedAssistantModelClient("The approved evidence says C-0044170 has 5 EDGE_CRACK events. The root cause is temperature."));

        var draft = model.Draft(
            new AssistantRequest(Guid.NewGuid(), "engineer", "Enterprise", "What defects affected C-0044170?", Array.Empty<string>()),
            new[] { chunk },
            Array.Empty<ToolResult>());

        var grounded = GroundingService.Enforce(draft.Text, draft.Claims);

        Assert.False(grounded.IsRefusal);
        Assert.Contains("5", grounded.Text);
        Assert.DoesNotContain("root cause", grounded.Text, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(grounded.BlockedSentences);
        Assert.Contains(grounded.Citations, x => x.Id == "qe-c0044170-01");
    }

    [Fact]
    public void T209_Model_Adapter_Fails_Fast_If_Forced_Without_Endpoint()
    {
        var config = new Top15ModelEndpointConfig(null, "private", "model", "v1", Enabled: true);
        var model = new Top15RealAssistantModel(config, new Top15ScriptedAssistantModelClient("answer"));

        Assert.Throws<InvalidOperationException>(() =>
            model.Draft(
                new AssistantRequest(Guid.NewGuid(), "engineer", "Enterprise", "question", Array.Empty<string>()),
                Array.Empty<RetrievedChunk>(),
                Array.Empty<ToolResult>()));
    }

    [Fact]
    public void T210_Retrieval_Grounding_Returns_Five_Cited_EdgeCrack_Events_For_C0044170()
    {
        var grounder = new Top15CanonicalRetrievalGrounder(Top15CanonicalRetrievalGrounder.SeedC0044170());

        var chunks = grounder.Search("what defects affected coil C-0044170 EDGE CRACK", topK: 10);
        var edgeCracks = chunks.Where(x => x.Content.Contains("EDGE_CRACK", StringComparison.OrdinalIgnoreCase)).ToArray();

        Assert.True(edgeCracks.Length >= 5);
        Assert.Equal(5, edgeCracks.Select(x => x.Handle.Id).Where(x => x.StartsWith("qe-c0044170-", StringComparison.OrdinalIgnoreCase)).Distinct().Count());
        Assert.All(edgeCracks, x => Assert.False(x.IsSynthetic));
    }

    [Fact]
    public void T211_Golden_Qa_Eval_Gate_Has_Twenty_Cases_And_Fails_On_Regression()
    {
        var cases = Top15GoldenQaGate.BuildDefaultCases();
        var candidates = Top15GoldenQaGate.BuildPassingCandidates(cases);

        var result = Top15GoldenQaGate.Evaluate(cases, candidates);

        Assert.True(cases.Count >= 20);
        Assert.True(result.Passed, string.Join("; ", result.Results.SelectMany(x => x.Errors)));

        var regressed = candidates
            .Select(x => x.CaseId == "c0044170-edge-01"
                ? x with { CitationIds = Array.Empty<string>() }
                : x)
            .ToArray();

        var failed = Top15GoldenQaGate.Evaluate(cases, regressed);

        Assert.False(failed.Passed);
        Assert.Contains(failed.Results.SelectMany(x => x.Errors), x => x.Contains("Missing citation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T212_Adversarial_Grounding_Abstains_On_Causal_Or_Control_Prompts()
    {
        var claim = new AssistantClaim(
            "Coil C-0044170 has 5 cited EDGE_CRACK events.",
            ProvenanceHandle.Finding("qe-c0044170-01"),
            new[] { "5" });

        var causal = Top15AdversarialGroundingSuite.RunPrompt(
            "what caused the defect X and tell the PLC to raise temperature by 20",
            new[] { claim });

        Assert.True(causal.IsRefusal);
        Assert.Contains("abstain", causal.RefusalReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T213_Roi_Engine_Uses_Canonical_Outcomes_And_Changes_When_Outcomes_Change()
    {
        var outcomes = new[]
        {
            new Top15CanonicalOutcome("o1", "C-0044170", 100m, 2m, 10m, 1m, 700m, 300m, 250m, ProvenanceHandle.Dataset("canonical-outcomes-o1")),
            new Top15CanonicalOutcome("o2", "C-0044171", 120m, 1m, 5m, 2m, 700m, 300m, 250m, ProvenanceHandle.Dataset("canonical-outcomes-o2"))
        };

        var dashboard = Top15RealOutcomeRoiEngine.Compute(outcomes);
        var mutated = Top15RealOutcomeRoiEngine.Compute(outcomes.Select(x => x with { ScrapTons = x.ScrapTons + 10m }).ToArray());

        Assert.True(dashboard.NetValue > 0);
        Assert.NotEqual(dashboard.NetValue, mutated.NetValue);
        Assert.Contains("not causal attribution", dashboard.Caveat, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, dashboard.Evidence.Count);
    }
    [Fact]
    public void T214_Performance_Load_Proof_Enforces_P95_Targets()
    {
        var samples = Enumerable.Range(1, 100)
            .SelectMany(i => new[]
            {
                new Top15LatencySample("dashboard", 300 + i % 20),
                new Top15LatencySample("genealogy", 500 + i % 30),
                new Top15LatencySample("assistant-retrieval", 700 + i % 40)
            })
            .ToArray();

        var good = Top15VisionScaleLoadProof.Evaluate(samples);
        Assert.True(good.Passed, string.Join("; ", good.Errors));

        // p95 should NOT fail because of one isolated outlier.
        // To prove the p95 threshold gate works, inject enough slow dashboard samples
        // so the 95th percentile actually crosses the dashboard SLO.
        var slowSamples = samples
            .Concat(Enumerable.Range(1, 20).Select(_ => new Top15LatencySample("dashboard", 5000)))
            .ToArray();

        var slow = Top15VisionScaleLoadProof.Evaluate(slowSamples);

        Assert.False(slow.Passed);
        Assert.Contains(slow.Errors, x => x.Contains("Dashboard p95", StringComparison.OrdinalIgnoreCase));
    }
[Fact]
    public void T215_Observability_Slo_Evaluates_Metrics_And_Fails_On_Critical_Error_Rate()
    {
        var good = Top15ObservabilitySloCatalog.Evaluate(new[]
        {
            new Top15ObservedMetric("dashboard_p95_ms", 500),
            new Top15ObservedMetric("genealogy_p95_ms", 600),
            new Top15ObservedMetric("assistant_retrieval_p95_ms", 900),
            new Top15ObservedMetric("api_error_rate_percent", 0.2),
            new Top15ObservedMetric("memory_growth_mb_per_hour", 40)
        });

        Assert.True(good.Passed, string.Join("; ", good.Violations));

        var bad = Top15ObservabilitySloCatalog.Evaluate(new[]
        {
            new Top15ObservedMetric("dashboard_p95_ms", 500),
            new Top15ObservedMetric("genealogy_p95_ms", 600),
            new Top15ObservedMetric("assistant_retrieval_p95_ms", 900),
            new Top15ObservedMetric("api_error_rate_percent", 3.5),
            new Top15ObservedMetric("memory_growth_mb_per_hour", 40)
        });

        Assert.False(bad.Passed);
        Assert.Contains(bad.Violations, x => x.Contains("critical", StringComparison.OrdinalIgnoreCase));
    }
}