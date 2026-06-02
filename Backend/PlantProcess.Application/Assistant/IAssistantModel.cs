namespace PlantProcess.Application.Assistant;

/// <summary>
/// Pluggable answer composer. The default ExtractiveAssistantModel composes ONLY from grounded chunks/tools
/// (no fabrication). Swap in your LLM here; the grounding guard still enforces the contract on its output.
/// </summary>
public interface IAssistantModel
{
    AssistantDraft Draft(AssistantRequest request, IReadOnlyList<RetrievedChunk> chunks, IReadOnlyList<ToolResult> tools);
}