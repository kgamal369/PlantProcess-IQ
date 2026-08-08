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
    private readonly IWidgetResultEvidenceReader _widgetEvidence;

    public AssistantService(
        IRetrievalIndex retrieval,
        ToolRegistry tools,
        IAssistantModel model,
        IWidgetResultEvidenceReader widgetEvidence)
    {
        _retrieval = retrieval;
        _tools = tools;
        _model = model;
        _widgetEvidence = widgetEvidence;
    }

    public async Task<GroundedAnswer> AskAsync(AssistantRequest request, IReadOnlyList<(string Tool, IReadOnlyDictionary<string, string> Args)>? toolCalls, CancellationToken ct)
    {
        // T-073 CONTEXTUAL EVIDENCE ANCHOR.
        //
        // When the caller says which widget is focused, this turn must be anchored
        // to evidence for THAT widget. Without this rule the turn falls through to
        // whatever chunk happens to rank highest, and a question asked while
        // looking at a chart gets answered from a source-connector description -
        // grounded, and completely wrong for what was asked.
        //
        // The trigger is the typed envelope, not the words of the question. No page
        // code, widget code or question text is named anywhere in this rule, and the
        // match runs against the persisted snapshot identity rather than against
        // chunk prose.
        //
        // A page WITHOUT a focused widget stays soft narrowing, deliberately: a
        // general question asked from a page must not be forced to have widget
        // evidence behind it.
        var focusedWidget = request.Context?.WidgetCode;
        if (!string.IsNullOrWhiteSpace(focusedWidget))
        {
            var anchor = await _widgetEvidence.FindActiveAnchorAsync(
                request.TenantId, focusedWidget!, request.Context?.PageCode, ct);

            if (anchor is null)
            {
                return GroundedAnswer.Refusal(
                    "I don't have approved evidence for this widget to answer that.");
            }
        }

        // T-072: the envelope narrows the search and nothing else. Tenant and role
        // still come from the caller's claims, so context can never widen scope.
        var contextTerms = request.Context?.RetrievalTerms() ?? Array.Empty<string>();

        var chunks = await _retrieval.SearchAsync(
            new RetrievalQuery(request.TenantId, request.Role, request.Question, 6, contextTerms), ct);

        var toolResults = new List<ToolResult>();
        if (toolCalls is not null)
        {
            var ctx = new ToolContext(request.TenantId, request.Role, request.License);
            foreach (var call in toolCalls)
                toolResults.Add(await _tools.ExecuteAsync(call.Tool, ctx, call.Args, ct));
        }

        if (chunks.Count == 0 && toolResults.All(t => !t.Ok))
            return GroundedAnswer.Refusal("I don't have approved evidence in your scope to answer that.");

        // T-072: the model is handed the request WITHOUT the envelope. Echoing a
        // context value into an answer is then structurally impossible rather than
        // forbidden by a string check, for this model and for any model swapped in
        // later. Retrieval has already used it by this point.
        var draft = _model.Draft(request with { Context = null }, chunks, toolResults);
        return GroundingService.Enforce(draft.Text, draft.Claims);
    }
}