using System.Text.Json;
using System.Text.RegularExpressions;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// Safe default model: composes an answer purely from retrieved chunks and tool results, framing
/// relationships as suspected associations (never 'root cause'). Every number it states comes from a
/// source, so it is authorized by a claim. Deterministic.
/// </summary>
public sealed class ExtractiveAssistantModel : IAssistantModel
{
    private static readonly Regex NumberRx = new("\\d[\\d.,]*", RegexOptions.Compiled);

    public AssistantDraft Draft(AssistantRequest request, IReadOnlyList<RetrievedChunk> chunks, IReadOnlyList<ToolResult> tools)
    {
        var claims = new List<AssistantClaim>();
        var sentences = new List<string>();

        foreach (var chunk in chunks.Where(c => !c.IsSynthetic))
        {
            var text = $"Based on {chunk.SourceKind} evidence, {chunk.Content.Trim()}";
            sentences.Add(EnsureStop(text));
            claims.Add(new AssistantClaim(text, chunk.Handle, Numbers(chunk.Content), chunk.IsSynthetic));
        }

        foreach (var tool in tools.Where(t => t.Ok))
        {
            var payload = tool.PayloadJson ?? "";
            var text = $"A {tool.ToolName} result indicates {Summarise(payload)}";
            sentences.Add(EnsureStop(text));
            foreach (var handle in tool.Handles)
                claims.Add(new AssistantClaim(text, handle, Numbers(payload)));
        }

        return new AssistantDraft(string.Join(" ", sentences), claims);
    }

    private static IReadOnlyList<string> Numbers(string s)
        => NumberRx.Matches(s).Select(m => m.Value.Replace(",", "").TrimEnd('.')).ToList();

    private static string EnsureStop(string s) => s.EndsWith(".") || s.EndsWith("!") || s.EndsWith("?") ? s : s + ".";

    private static string Summarise(string json)
    {
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.ToString(); }
        catch { return json; }
    }
}