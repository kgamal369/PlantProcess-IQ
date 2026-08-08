using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// T-072. The context envelope must reach the retrieval call, must change what is
/// retrieved between two pages, and must never reach the answer composer.
/// </summary>
public class T072ContextEnvelopeTests
{
    private sealed class CapturingIndex : IRetrievalIndex
    {
        public RetrievalQuery? LastQuery;
        public readonly List<RetrievedChunk> Chunks = new();

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken ct)
        {
            LastQuery = query;
            return Task.FromResult((IReadOnlyList<RetrievedChunk>)Chunks);
        }

        public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct)
            => Task.FromResult(new ReindexResult(0, 0, request.CorrelationId));
    }

    private sealed class CapturingModel : IAssistantModel
    {
        public AssistantRequest? LastRequest;

        public AssistantDraft Draft(AssistantRequest request, IReadOnlyList<RetrievedChunk> chunks, IReadOnlyList<ToolResult> tools)
        {
            LastRequest = request;
            var text = string.Join(" ", chunks.Select(c => c.Content));
            var claims = chunks
                .Select(c => new AssistantClaim(c.Content, c.Handle, Array.Empty<string>()))
                .ToList();
            return new AssistantDraft(text, claims);
        }
    }

    private static (AssistantService Service, CapturingIndex Index, CapturingModel Model) Build()
    {
        var index = new CapturingIndex();
        index.Chunks.Add(new RetrievedChunk(
            Guid.NewGuid(), "finding", "f-1",
            "Defect rate is elevated on the finishing line.",
            ProvenanceHandle.Finding("f-1"), 1.0));

        var model = new CapturingModel();
        var service = new AssistantService(index, new ToolRegistry(Array.Empty<ITool>()), model);
        return (service, index, model);
    }

    private static AssistantRequest Ask(string question, AssistantContextEnvelope? context)
        => new(Guid.NewGuid(), "engineer", "pro-plus", question, Array.Empty<string>(), context);

    [Fact]
    public async Task The_envelope_identifiers_reach_the_retrieval_call()
    {
        var (service, index, _) = Build();

        await service.AskAsync(
            Ask("what does this chart show", new AssistantContextEnvelope(
                Route: "/dashboard/quality",
                PageCode: "PAGE_ALPHA",
                WidgetCode: "WIDGET_ALPHA",
                Selections: new[] { "SELECTION_ALPHA" },
                Filters: new[] { "FILTER_ALPHA" })),
            null, CancellationToken.None);

        var terms = index.LastQuery!.ContextTerms!;
        Assert.Contains("/dashboard/quality", terms);
        Assert.Contains("PAGE_ALPHA", terms);
        Assert.Contains("WIDGET_ALPHA", terms);
        Assert.Contains("SELECTION_ALPHA", terms);
        Assert.Contains("FILTER_ALPHA", terms);
        Assert.Equal("what does this chart show", index.LastQuery.Text);
    }

    [Fact]
    public async Task The_same_question_on_two_pages_retrieves_under_different_context()
    {
        var (service, index, _) = Build();

        await service.AskAsync(
            Ask("what does this chart show", new AssistantContextEnvelope(PageCode: "PAGE_ALPHA", WidgetCode: "WIDGET_ALPHA")),
            null, CancellationToken.None);
        var first = index.LastQuery!.ContextTerms!.ToArray();

        await service.AskAsync(
            Ask("what does this chart show", new AssistantContextEnvelope(PageCode: "PAGE_BETA", WidgetCode: "WIDGET_BETA")),
            null, CancellationToken.None);
        var second = index.LastQuery!.ContextTerms!.ToArray();

        Assert.NotEqual(first, second);
        Assert.Contains("PAGE_ALPHA", first);
        Assert.DoesNotContain("PAGE_ALPHA", second);
    }

    [Fact]
    public async Task The_answer_composer_never_receives_the_envelope()
    {
        var (service, index, model) = Build();

        await service.AskAsync(
            Ask("what does this chart show", new AssistantContextEnvelope(PageCode: "PAGE_ALPHA")),
            null, CancellationToken.None);

        Assert.NotNull(index.LastQuery!.ContextTerms);
        Assert.NotEmpty(index.LastQuery.ContextTerms!);
        Assert.Null(model.LastRequest!.Context);
    }

    [Fact]
    public async Task Client_supplied_numbers_and_handles_are_carried_but_never_embedded()
    {
        var (service, index, _) = Build();

        await service.AskAsync(
            Ask("what does this chart show", new AssistantContextEnvelope(
                PageCode: "PAGE_ALPHA",
                LastResultSummary: "the widget last returned 3.4 and 1.9",
                EvidenceHandles: new[] { "finding:should-not-be-embedded" })),
            null, CancellationToken.None);

        var terms = index.LastQuery!.ContextTerms!;
        Assert.DoesNotContain(terms, t => t.Contains("3.4"));
        Assert.DoesNotContain(terms, t => t.Contains("should-not-be-embedded"));
    }

    [Fact]
    public async Task A_request_without_an_envelope_still_answers()
    {
        var (service, index, model) = Build();

        var answer = await service.AskAsync(Ask("what does this chart show", null), null, CancellationToken.None);

        Assert.Empty(index.LastQuery!.ContextTerms!);
        Assert.Null(model.LastRequest!.Context);
        Assert.False(answer.IsRefusal);
    }

    [Fact]
    public void A_client_cannot_stuff_the_embedded_text()
    {
        var selections = Enumerable.Range(0, 200).Select(i => new string('x', 400) + i).ToArray();
        var terms = new AssistantContextEnvelope(PageCode: "PAGE_ALPHA", Selections: selections).RetrievalTerms();

        Assert.True(terms.Count <= 24);
        Assert.All(terms, t => Assert.True(t.Length <= 120));
    }
}