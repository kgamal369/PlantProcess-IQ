using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// T-073 focused-widget composition.
///
/// The anchor rule alone produced a false green: a turn focused on one widget
/// was answered describing a neighbouring widget on the same page that ranked
/// higher, while a citation for the focused widget also happened to be present.
/// A user looking at one chart was told about another.
///
/// The invariant these tests hold: for a focused turn, EVERY widget-result claim
/// in the composed answer belongs to the focused widget. Other evidence families
/// may still speak; another widget may not.
/// </summary>
public class T073FocusedCompositionTests
{
    private static readonly Guid FocusedEvidenceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RivalEvidenceId   = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class StubIndex : IRetrievalIndex
    {
        public readonly List<RetrievedChunk> Chunks = new();

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<RetrievedChunk>)Chunks);

        public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct)
            => Task.FromResult(new ReindexResult(0, 0, request.CorrelationId));
    }

    /// <summary>
    /// Mirrors ExtractiveAssistantModel closely enough to be honest: it authorises
    /// the numeric tokens of each chunk it uses.
    ///
    /// An earlier version of this stub returned EMPTY numeric tokens, and
    /// GroundingService then correctly BLOCKED the focused sentence because its
    /// numbers were unauthorised - a true guard failing an untrue test. A stub
    /// that cannot be grounded proves nothing about composition.
    /// </summary>
    private sealed class CapturingModel : IAssistantModel
    {
        private static readonly Regex NumberRx = new("\\d[\\d.,]*", RegexOptions.Compiled);

        public IReadOnlyList<RetrievedChunk> LastChunks = Array.Empty<RetrievedChunk>();

        public AssistantDraft Draft(AssistantRequest request, IReadOnlyList<RetrievedChunk> chunks, IReadOnlyList<ToolResult> tools)
        {
            LastChunks = chunks;

            var claims = chunks
                .Select(c => new AssistantClaim(c.Content, c.Handle, Numbers(c.Content)))
                .ToList();

            return new AssistantDraft(string.Join(" ", chunks.Select(c => c.Content)), claims);
        }

        private static IReadOnlyList<string> Numbers(string s)
            => NumberRx.Matches(s).Select(m => m.Value.Replace(",", string.Empty).TrimEnd('.')).ToList();
    }

    private sealed class StubEvidence : IWidgetResultEvidenceReader
    {
        public Guid? Anchor = FocusedEvidenceId;
        public WidgetResultEvidenceSnapshot? Snapshot;

        public Task<WidgetResultEvidenceSnapshot?> ReadAsync(Guid tenantId, Guid evidenceId, CancellationToken ct)
            => Task.FromResult(evidenceId == FocusedEvidenceId ? Snapshot : null);

        public Task<Guid?> FindActiveAnchorAsync(Guid tenantId, string widgetCode, string? pageCode, CancellationToken ct)
            => Task.FromResult(Anchor);
    }

    private static WidgetResultEvidenceSnapshot FocusedSnapshot()
    {
        var identity = new WidgetEvidenceIdentity(
            "PAGE_ALPHA", "WIDGET_FOCUSED", Guid.Empty, "chart", "line", "DIM_ALPHA", "MEASURE_ALPHA", null);

        var result = WidgetResultEvidence.Normalise(
            new List<string> { "DIM_ALPHA", "dimensionLabel", "value", "observationCount" },
            new List<IDictionary<string, object?>>
            {
                new Dictionary<string, object?>
                {
                    ["DIM_ALPHA"] = "KEY_ONE",
                    ["dimensionLabel"] = "LABEL_ONE",
                    ["value"] = 12.5d,
                    ["observationCount"] = 900
                }
            });

        return new WidgetResultEvidenceSnapshot(
            FocusedEvidenceId, identity, result, "queryfp", "resultfp", "{}", DateTime.UtcNow);
    }

    private static (AssistantService Service, StubIndex Index, CapturingModel Model, StubEvidence Evidence) Build()
    {
        var index = new StubIndex();

        // A rival widget on the same page, ranked first, exactly as happened live.
        index.Chunks.Add(new RetrievedChunk(
            Guid.NewGuid(), WidgetResultEvidence.ChunkSourceKind, RivalEvidenceId.ToString(),
            "On page PAGE_ALPHA, widget WIDGET_RIVAL shows something else entirely: RIVAL_LABEL 7844.",
            ProvenanceHandle.WidgetResult(RivalEvidenceId.ToString()), 0.9));

        index.Chunks.Add(new RetrievedChunk(
            Guid.NewGuid(), "DATASET", "connection:CP-05",
            "A source connection description.", ProvenanceHandle.Dataset("connection:CP-05"), 0.8));

        index.Chunks.Add(new RetrievedChunk(
            Guid.NewGuid(), "finding", "f-1",
            "An approved finding.", ProvenanceHandle.Finding("f-1"), 0.7));

        var model = new CapturingModel();
        var evidence = new StubEvidence { Snapshot = FocusedSnapshot() };
        var service = new AssistantService(
            index, new ToolRegistry(Array.Empty<ITool>()), model,
            evidence, new EmptyParameterQuantityRegistry());
        return (service, index, model, evidence);
    }

    private static AssistantRequest Ask(AssistantContextEnvelope? context)
        => new(Guid.NewGuid(), "engineer", "pro-plus", "what does this chart show", Array.Empty<string>(), context);

    private static AssistantContextEnvelope Focused()
        => new(PageCode: "PAGE_ALPHA", WidgetCode: "WIDGET_FOCUSED");

    [Fact]
    public async Task No_other_widget_speaks_in_place_of_the_focused_one()
    {
        var (service, _, model, _) = Build();

        await service.AskAsync(Ask(Focused()), null, CancellationToken.None);

        var widgetChunks = model.LastChunks
            .Where(c => string.Equals(c.SourceKind, WidgetResultEvidence.ChunkSourceKind, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(widgetChunks);
        Assert.Equal(FocusedEvidenceId.ToString(), widgetChunks[0].SourceRef);
        Assert.DoesNotContain(model.LastChunks, c => c.SourceRef == RivalEvidenceId.ToString());
    }

    [Fact]
    public async Task The_focused_evidence_is_first_and_carries_its_own_handle()
    {
        var (service, _, model, _) = Build();

        await service.AskAsync(Ask(Focused()), null, CancellationToken.None);

        var first = model.LastChunks[0];
        Assert.Equal(WidgetResultEvidence.ChunkSourceKind, first.SourceKind);
        Assert.Equal(ProvenanceKind.WidgetResult, first.Handle.Kind);
        Assert.Equal(FocusedEvidenceId.ToString(), first.Handle.Id);
    }

    [Fact]
    public async Task The_focused_statement_is_the_persisted_sentence_with_its_real_numbers()
    {
        var (service, _, model, evidence) = Build();

        var answer = await service.AskAsync(Ask(Focused()), null, CancellationToken.None);

        var expected = WidgetResultEvidence.Sentence(evidence.Snapshot!.Identity, evidence.Snapshot!.Result);
        Assert.Equal(expected, model.LastChunks[0].Content);

        /* Not merely present - SURVIVING GroundingService. The focused sentence
           carries numbers, so it only reaches the answer if its claim authorises
           them, which is the whole reason the anchor enters as evidence rather
           than as text glued on afterwards. */
        Assert.Empty(answer.BlockedSentences);
        Assert.Contains("12.5", answer.Text);
        Assert.Contains("WIDGET_FOCUSED", answer.Text);
    }

    [Fact]
    public async Task Other_evidence_families_are_not_suppressed()
    {
        var (service, _, model, _) = Build();

        await service.AskAsync(Ask(Focused()), null, CancellationToken.None);

        Assert.Contains(model.LastChunks, c => c.SourceKind == "DATASET");
        Assert.Contains(model.LastChunks, c => c.SourceKind == "finding");
    }

    [Fact]
    public async Task Without_a_focused_widget_composition_is_untouched()
    {
        var (service, index, model, _) = Build();

        await service.AskAsync(Ask(new AssistantContextEnvelope(PageCode: "PAGE_ALPHA")), null, CancellationToken.None);

        Assert.Equal(index.Chunks.Count, model.LastChunks.Count);
        Assert.Contains(model.LastChunks, c => c.SourceRef == RivalEvidenceId.ToString());
    }

    [Fact]
    public async Task A_focused_widget_whose_snapshot_cannot_be_read_refuses()
    {
        var (service, _, _, evidence) = Build();
        evidence.Snapshot = null;

        var answer = await service.AskAsync(Ask(Focused()), null, CancellationToken.None);

        /* The neighbours were available and would have answered. Refusing is the
           only honest option once the focused widget's own evidence is gone. */
        Assert.True(answer.IsRefusal);
    }

    [Fact]
    public async Task The_focused_evidence_is_never_duplicated_when_retrieval_already_found_it()
    {
        var (service, index, model, _) = Build();

        index.Chunks.Add(new RetrievedChunk(
            Guid.NewGuid(), WidgetResultEvidence.ChunkSourceKind, FocusedEvidenceId.ToString(),
            "A retrieved copy of the focused widget evidence.",
            ProvenanceHandle.WidgetResult(FocusedEvidenceId.ToString()), 0.95));

        await service.AskAsync(Ask(Focused()), null, CancellationToken.None);

        Assert.Single(model.LastChunks, c => c.SourceRef == FocusedEvidenceId.ToString());
    }
}