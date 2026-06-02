namespace PlantProcess.Application.Assistant;

/// <summary>
/// T-052: the ONLY path by which the assistant reaches backend data. Enforces role, tenant and license
/// before executing, refuses unknown/unpermitted tools with a typed error, and drops invalid results.
/// </summary>
public sealed class ToolRegistry
{
    private static readonly string[] RoleRank = { "viewer", "operator", "engineer", "admin" };
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
        => _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ToolNames => _tools.Keys.ToArray();

    public async Task<ToolResult> ExecuteAsync(string toolName, ToolContext context, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return ToolResult.Refused(toolName, "unknown_tool");

        if (Rank(context.Role) < Rank(tool.RequiredRole))
            return ToolResult.Refused(toolName, "unpermitted_role");

        if (!string.IsNullOrWhiteSpace(tool.RequiredLicense) &&
            !string.Equals(tool.RequiredLicense, context.License, StringComparison.OrdinalIgnoreCase))
            return ToolResult.Refused(toolName, "unpermitted_license");

        ToolResult result;
        try { result = await tool.ExecuteAsync(context, args, ct); }
        catch { return ToolResult.Refused(toolName, "tool_error"); }

        // Validation gate: a result that is not Ok or carries no resolvable handle is NOT returned.
        if (!result.Ok || result.Handles.Count == 0 || result.Handles.Any(h => string.IsNullOrWhiteSpace(h.Id)))
            return ToolResult.Refused(toolName, "invalid_output");

        return result;
    }

    private static int Rank(string role)
    {
        var i = Array.IndexOf(RoleRank, (role ?? "").Trim().ToLowerInvariant());
        return i < 0 ? 0 : i;
    }
}