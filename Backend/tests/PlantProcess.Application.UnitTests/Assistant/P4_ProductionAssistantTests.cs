using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class P4_ProductionAssistantTests
{
    [Fact]
    public void T023_local_semantic_embedder_retrieves_related_corpus_better_than_unrelated()
    {
        var embedder = new LocalSemanticEmbedder();

        var query = embedder.Embed("surface defect quality finding");
        var related = embedder.Embed("quality finding shows repeated surface defect events");
        var unrelated = embedder.Embed("energy market price and contract renewal");

        Assert.Equal(LocalSemanticEmbedder.Dimensions, query.Count);
        Assert.True(LocalSemanticEmbedder.AirGapNoNetwork);
        Assert.True(
            LocalSemanticEmbedder.Cosine(query, related) > LocalSemanticEmbedder.Cosine(query, unrelated),
            "Related manufacturing-quality corpus text should be closer than unrelated commercial text.");
    }

    [Fact]
    public async Task T024_index_build_service_filters_synthetic_and_is_tenant_scoped()
    {
        var tenantId = Guid.NewGuid();
        var handle = ProvenanceHandle.DocumentSection("p4-doc", "quality");
        var index = new FakeRetrievalIndex();
        var service = new AssistantRetrievalIndexBuildService(index);

        var result = await service.RefreshAsync(
            tenantId,
            new[]
            {
                new RetrievedChunk(Guid.NewGuid(), "finding", "F-1", "surface defect rate 12", handle, 0.9),
                new RetrievedChunk(Guid.NewGuid(), "finding", "F-2", "synthetic demo should not index", handle, 0.9, IsSynthetic: true)
            },
            "finding",
            "p4-test-job",
            CancellationToken.None);

        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(1, result.ChunkCount);
        Assert.True(result.ChangedOnly);
        Assert.Equal("completed", result.Status);
        Assert.Single(index.Indexed);
    }

    [Fact]
    public void T025_gateway_blocks_uncited_numbers_and_forbidden_causality()
    {
        var handle = ProvenanceHandle.DocumentSection("p4-evidence", "defect-rate");
        var claims = new[]
        {
            new AssistantClaim(
                "Observed defect rate was 12.5%",
                handle,
                new[] { "12.5" })
        };

        var result = GroundedAssistantGateway.Certify(
            prompt: "Explain defect rate.",
            modelOutput: "Observed defect rate was 12.5%. The root cause will save 999.",
            retrievedClaims: claims,
            providerKey: LocalSemanticEmbedder.ProviderKey,
            modelKey: LocalSemanticEmbedder.ModelKey,
            modelVersion: "2026.06");

        Assert.False(result.IsRefusal);
        Assert.Contains("12.5", result.Text);
        Assert.DoesNotContain("999", result.Text);
        Assert.DoesNotContain("root cause", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Citations);
        Assert.NotEmpty(result.BlockedSentences);
    }

    [Fact]
    public void T026_extractive_model_answers_with_visible_citation()
    {
        var handle = ProvenanceHandle.DocumentSection("p4-evidence", "kpi");
        var chunk = new RetrievedChunk(
            Guid.NewGuid(),
            "finding",
            "F-100",
            "Defect rate is 12.5% based on inspected population.",
            handle,
            0.99);

        var request = new AssistantRequest(Guid.NewGuid(), "engineer", "enterprise", "What is the defect rate?", Array.Empty<string>());
        var draft = new ExtractiveAssistantModel().Draft(request, new[] { chunk }, Array.Empty<ToolResult>());
        var answer = GroundingService.Enforce(draft.Text, draft.Claims);

        Assert.False(answer.IsRefusal);
        Assert.Contains("12.5", answer.Text);
        Assert.Contains(handle, answer.Citations);
    }

    [Fact]
    public void T027_eval_harness_fails_model_version_drift_and_forbidden_number()
    {
        var handle = ProvenanceHandle.DocumentSection("p4-evidence", "eval");
        var result = new GroundedAssistantGatewayResult(
            Prompt: "test",
            Text: "Unsupported 999 value.",
            Citations: new[] { handle },
            IsRefusal: false,
            RefusalReason: null,
            BlockedSentences: Array.Empty<string>(),
            ProviderKey: LocalSemanticEmbedder.ProviderKey,
            ModelKey: LocalSemanticEmbedder.ModelKey,
            ModelVersion: "wrong-version",
            GroundingCertified: true);

        var eval = new AssistantEvalHarness().Evaluate(
            new AssistantEvalCase(
                "p4-version-pin",
                ExpectedAnswerable: true,
                RequiredCitationTokens: new[] { handle.Token },
                ForbiddenNumbers: new[] { "999" },
                PinnedModelVersion: "2026.06"),
            result);

        Assert.False(eval.Passed);
        Assert.Contains(eval.Errors, x => x.Contains("forbidden number", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(eval.Errors, x => x.Contains("Model version drift", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeRetrievalIndex : IRetrievalIndex
    {
        public List<RetrievedChunk> Indexed { get; } = new();

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RetrievedChunk>>(Indexed);

        public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken cancellationToken)
        {
            Indexed.Clear();
            Indexed.AddRange(request.Chunks);
            return Task.FromResult(new ReindexResult(request.Chunks.Count, request.Chunks.Count, request.CorrelationId));
        }
    }
}