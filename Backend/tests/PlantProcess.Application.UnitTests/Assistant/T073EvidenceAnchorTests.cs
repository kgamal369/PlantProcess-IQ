using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// T-073 contextual evidence anchor.
///
/// A focused widget makes evidence for THAT widget mandatory. Without it the
/// turn falls through to whatever ranks highest, which is how a chart question
/// came back describing a source connector: grounded, and not an answer to what
/// was asked.
/// </summary>
public class T073EvidenceAnchorTests
{
    private sealed class StubIndex : IRetrievalIndex
    {
        public readonly List<RetrievedChunk> Chunks = new();
        public int Searches;

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken ct)
        {
            Searches = Searches + 1;
            return Task.FromResult((IReadOnlyList<RetrievedChunk>)Chunks);
        }

        public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct)
            => Task.FromResult(new ReindexResult(0, 0, request.CorrelationId));
    }

    private sealed class StubModel : IAssistantModel
    {
        public AssistantDraft Draft(AssistantRequest request, IReadOnlyList<RetrievedChunk> chunks, IReadOnlyList<ToolResult> tools)
        {
            var text = string.Join(" ", chunks.Select(c => c.Content));
            var claims = chunks.Select(c => new AssistantClaim(c.Content, c.Handle, Array.Empty<string>())).ToList();
            return new AssistantDraft(text, claims);
        }
    }

    private sealed class StubEvidence : IWidgetResultEvidenceReader
    {
        public Guid? Anchor;
        public string? LastWidgetCode;
        public string? LastPageCode;
        public int Lookups;

        /// <summary>
        /// T-073-05: composition now READS the snapshot behind the anchor and
        /// refuses when it cannot. A stub that always returned null therefore made
        /// every anchored turn refuse - the rule was right and this stub was stale.
        /// It returns the snapshot for the anchor it just handed out, and null for
        /// anything else, which is what a tenant-scoped reader does.
        /// </summary>
        public Task<WidgetResultEvidenceSnapshot?> ReadAsync(Guid tenantId, Guid evidenceId, CancellationToken ct)
        {
            if (Anchor is null || evidenceId != Anchor.Value)
            {
                return Task.FromResult<WidgetResultEvidenceSnapshot?>(null);
            }

            var identity = new WidgetEvidenceIdentity(
                "PAGE_ALPHA", "WIDGET_ALPHA", Guid.Empty, "chart", "bar", "DIM_ALPHA", "MEASURE_ALPHA", null);

            var result = new NormalisedWidgetResult(
                Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>(), false, 0);

            return Task.FromResult<WidgetResultEvidenceSnapshot?>(new WidgetResultEvidenceSnapshot(
                evidenceId, identity, result, "queryfp", "resultfp", "{}", DateTime.UtcNow));
        }

        public Task<Guid?> FindActiveAnchorAsync(Guid tenantId, string widgetCode, string? pageCode, CancellationToken ct)
        {
            Lookups = Lookups + 1;
            LastWidgetCode = widgetCode;
            LastPageCode = pageCode;
            return Task.FromResult(Anchor);
        }
    }

    private static (AssistantService Service, StubIndex Index, StubEvidence Evidence) Build()
    {
        var index = new StubIndex();
        index.Chunks.Add(new RetrievedChunk(
            Guid.NewGuid(), "DATASET", "connection:CP-05",
            "A source connection description that has nothing to do with a chart.",
            ProvenanceHandle.Dataset("connection:CP-05"), 1.0));

        var evidence = new StubEvidence();
        var service = new AssistantService(index, new ToolRegistry(Array.Empty<ITool>()), new StubModel(), evidence);
        return (service, index, evidence);
    }

    private static AssistantRequest Ask(AssistantContextEnvelope? context)
        => new(Guid.NewGuid(), "engineer", "pro-plus", "what does this chart show", Array.Empty<string>(), context);

    [Fact]
    public async Task A_focused_widget_with_no_anchor_refuses_instead_of_answering_from_anything_else()
    {
        var (service, _, evidence) = Build();
        evidence.Anchor = null;

        var answer = await service.AskAsync(
            Ask(new AssistantContextEnvelope(PageCode: "PAGE_ALPHA", WidgetCode: "WIDGET_ALPHA")), null, CancellationToken.None);

        Assert.True(answer.IsRefusal);
        /* The connector chunk was available and outranked nothing else. Before this
           rule it would have been the answer. */
        Assert.DoesNotContain("source connection", answer.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_focused_widget_with_an_anchor_answers_normally()
    {
        var (service, index, evidence) = Build();
        evidence.Anchor = Guid.NewGuid();

        var answer = await service.AskAsync(
            Ask(new AssistantContextEnvelope(PageCode: "PAGE_ALPHA", WidgetCode: "WIDGET_ALPHA")), null, CancellationToken.None);

        Assert.False(answer.IsRefusal);
        Assert.Equal(1, index.Searches);
    }

    [Fact]
    public async Task A_page_without_a_focused_widget_is_not_forced_to_have_widget_evidence()
    {
        var (service, _, evidence) = Build();
        evidence.Anchor = null;

        var answer = await service.AskAsync(
            Ask(new AssistantContextEnvelope(PageCode: "PAGE_ALPHA")), null, CancellationToken.None);

        /* Soft narrowing, deliberately: a general question asked from a page must
           not require a widget behind it. */
        Assert.False(answer.IsRefusal);
        Assert.Equal(0, evidence.Lookups);
    }

    [Fact]
    public async Task A_request_with_no_context_at_all_is_unaffected()
    {
        var (service, _, evidence) = Build();
        evidence.Anchor = null;

        var answer = await service.AskAsync(Ask(null), null, CancellationToken.None);

        Assert.False(answer.IsRefusal);
        Assert.Equal(0, evidence.Lookups);
    }

    [Fact]
    public async Task The_anchor_is_looked_up_by_the_focused_widget_and_page_from_the_envelope()
    {
        var (service, _, evidence) = Build();
        evidence.Anchor = Guid.NewGuid();

        await service.AskAsync(
            Ask(new AssistantContextEnvelope(PageCode: "PAGE_ALPHA", WidgetCode: "WIDGET_ALPHA")), null, CancellationToken.None);

        Assert.Equal(1, evidence.Lookups);
        Assert.Equal("WIDGET_ALPHA", evidence.LastWidgetCode);
        Assert.Equal("PAGE_ALPHA", evidence.LastPageCode);
    }

    [Fact]
    public async Task The_refusal_does_not_name_a_page_a_widget_or_the_question()
    {
        var (service, _, evidence) = Build();
        evidence.Anchor = null;

        var answer = await service.AskAsync(
            Ask(new AssistantContextEnvelope(PageCode: "PAGE_ALPHA", WidgetCode: "WIDGET_ALPHA")), null, CancellationToken.None);

        /* Generic by construction: the sentence a customer sees carries no code
           from their installation and no fragment of their question. */
        Assert.DoesNotContain("PAGE_ALPHA", answer.Text);
        Assert.DoesNotContain("WIDGET_ALPHA", answer.Text);
        Assert.DoesNotContain("chart", answer.Text, StringComparison.OrdinalIgnoreCase);
    }
}