namespace PlantProcess.Application.Assistant;

/// <summary>An approved backend capability the assistant may call — always within RBAC + tenant + license.</summary>
public interface ITool
{
    string Name { get; }
    string RequiredRole { get; }      // minimum role
    string? RequiredLicense { get; }  // null = any license

    Task<ToolResult> ExecuteAsync(ToolContext context, IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken);
}