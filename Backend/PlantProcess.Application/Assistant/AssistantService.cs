namespace PlantProcess.Application.Assistant;

/// <summary>
/// Orchestrates a grounded turn: retrieve (tenant + permission scoped) -> run any requested approved tools
/// -> model drafts -> grounding guard enforces the contract -> grounded answer or honest refusal.
/// </summary>
public sealed class AssistantService
{
    private readonly IRetrievalIndex _retrieval;
    private readonly ToolRegistry _tools;
    private readonly IAssistantModel _model;

    public AssistantService(IRetrievalIndex retrieval, ToolRegistry tools, IAssistantModel model)
    {
        _retrieval = retrieval;
        _tools = tools;
        _model = model;
    }

    public async Task<GroundedAnswer> AskAsync(AssistantRequest request, IReadOnlyList<(string Tool, IReadOnlyDictionary<string, string> Args)>? toolCalls, CancellationToken ct)
    {
        var chunks = await _retrieval.SearchAsync(new RetrievalQuery(request.TenantId, request.Role, request.Question), ct);

        var toolResults = new List<ToolResult>();
        if (toolCalls is not null)
        {
            var ctx = new ToolContext(request.TenantId, request.Role, request.License);
            foreach (var call in toolCalls)
                toolResults.Add(await _tools.ExecuteAsync(call.Tool, ctx, call.Args, ct));
        }

        if (chunks.Count == 0 && toolResults.All(t => !t.Ok))
            return GroundedAnswer.Refusal("I don't have approved evidence in your scope to answer that.");

        var draft = _model.Draft(request, chunks, toolResults);
        return GroundingService.Enforce(draft.Text, draft.Claims);
    }
}