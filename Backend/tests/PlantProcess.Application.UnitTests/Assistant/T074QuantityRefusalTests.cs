using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// T-074 question-level quantity authority.
///
/// A quantity the registry knows but cannot vouch for stops the turn. Without
/// this the turn continued to generic retrieval and an unrelated document
/// answered a quantity question - safe from fabrication, but not a value, not a
/// band and not a refusal, so none of the three permitted outcomes.
/// </summary>
public class T074QuantityRefusalTests
{
    private sealed class CountingIndex : IRetrievalIndex
    {
        public int Searches;

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken ct)
        {
            Searches = Searches + 1;

            var chunk = new RetrievedChunk(
                Guid.NewGuid(), "DOC", "doc-1",
                "An unrelated policy document with no numbers in it.",
                ProvenanceHandle.DocumentSection("doc-1"), 1.0);

            return Task.FromResult<IReadOnlyList<RetrievedChunk>>(new List<RetrievedChunk> { chunk });
        }

        public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct)
            => Task.FromResult(new ReindexResult(0, 0, request.CorrelationId));
    }

    private sealed class PassThroughModel : IAssistantModel
    {
        public AssistantDraft Draft(AssistantRequest request, IReadOnlyList<RetrievedChunk> chunks, IReadOnlyList<ToolResult> tools)
        {
            var claims = chunks.Select(c => new AssistantClaim(c.Content, c.Handle, Array.Empty<string>())).ToList();
            return new AssistantDraft(string.Join(" ", chunks.Select(c => c.Content)), claims);
        }
    }

    private sealed class FixedRegistry : IParameterQuantityRegistry
    {
        private readonly IReadOnlyList<RegistryQuantity> _rows;
        public FixedRegistry(params RegistryQuantity[] rows) => _rows = rows;

        public Task<IReadOnlyList<RegistryQuantity>> GetActiveAsync(CancellationToken ct)
            => Task.FromResult(_rows);
    }

    private sealed class NoEvidence : IWidgetResultEvidenceReader
    {
        public Task<WidgetResultEvidenceSnapshot?> ReadAsync(Guid tenantId, Guid evidenceId, CancellationToken ct)
            => Task.FromResult<WidgetResultEvidenceSnapshot?>(null);

        public Task<Guid?> FindActiveAnchorAsync(Guid tenantId, string widgetCode, string? pageCode, CancellationToken ct)
            => Task.FromResult<Guid?>(null);
    }

    private static (AssistantService Service, CountingIndex Index) Build(params RegistryQuantity[] registry)
    {
        var index = new CountingIndex();
        var service = new AssistantService(
            index, new ToolRegistry(Array.Empty<ITool>()), new PassThroughModel(),
            new NoEvidence(), new FixedRegistry(registry));
        return (service, index);
    }

    private static AssistantRequest Ask(string question)
        => new(Guid.NewGuid(), "engineer", "pro-plus", question, Array.Empty<string>(), null);

    [Fact]
    public async Task A_known_but_unvouched_quantity_refuses_before_retrieval_runs()
    {
        /* Two synthetic definitions naming the same quantity with different
           ranges - the measured presentation state, in generic clothing. */
        var (service, index) = Build(
            new RegistryQuantity("ALPHA_RATE", "Alpha Rate", "Numeric", "u/min", 0.5m, 2.5m, true),
            new RegistryQuantity("ALPHA_RATE_UPM", "Alpha rate", "Numeric", "u/min", 0m, 3.0m, true));

        var answer = await service.AskAsync(Ask("what is the alpha rate"), null, CancellationToken.None);

        Assert.True(answer.IsRefusal);
        /* Stopped at the question, not filtered after composition. */
        Assert.Equal(0, index.Searches);
        Assert.Empty(answer.Citations);
    }

    [Fact]
    public async Task The_refusal_names_no_parameter_and_no_vocabulary()
    {
        var (service, _) = Build(
            new RegistryQuantity("ALPHA_RATE", "Alpha Rate", "Numeric", "u/min", 0.5m, 2.5m, true),
            new RegistryQuantity("ALPHA_RATE_UPM", "Alpha rate", "Numeric", "u/min", 0m, 3.0m, true));

        var answer = await service.AskAsync(Ask("what is the alpha rate"), null, CancellationToken.None);

        Assert.DoesNotContain("ALPHA_RATE", answer.RefusalReason);
        Assert.DoesNotContain("u/min", answer.RefusalReason);
    }

    [Fact]
    public async Task An_approved_definition_still_answers_normally()
    {
        var (service, index) = Build(
            new RegistryQuantity("ALPHA_RATE", "Alpha Rate", "Numeric", "u/min", 0.5m, 2.5m, false));

        var answer = await service.AskAsync(Ask("what is the alpha rate"), null, CancellationToken.None);

        Assert.False(answer.IsRefusal);
        Assert.Equal(1, index.Searches);
    }

    [Fact]
    public async Task A_question_naming_no_registry_quantity_is_untouched()
    {
        var (service, index) = Build(
            new RegistryQuantity("ALPHA_RATE", "Alpha Rate", "Numeric", "u/min", 0.5m, 2.5m, true));

        var answer = await service.AskAsync(Ask("what does this chart show"), null, CancellationToken.None);

        Assert.False(answer.IsRefusal);
        Assert.Equal(1, index.Searches);
    }
}