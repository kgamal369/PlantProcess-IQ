using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Assistant;

namespace PlantProcess.Api.Endpoints.Assistant;

/// <summary>T-053/T-054 backend: grounded ask + admin reindex. Tenant/role/license come from the caller's claims.</summary>
public static class AssistantEndpoints
{
    public sealed record AskRequest(string Question, IReadOnlyList<string>? ContextChips, IReadOnlyList<ToolCallDto>? Tools);
    public sealed record ToolCallDto(string Tool, Dictionary<string, string>? Args);

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assistant").WithTags("Assistant").RequireAuthorization();

        group.MapPost("/ask", async (AskRequest req, ClaimsPrincipal user, AssistantService assistant, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });

            var request = new AssistantRequest(
                tenantId, Role(user), License(user), req.Question ?? string.Empty,
                req.ContextChips ?? Array.Empty<string>());

            var toolCalls = req.Tools?
                .Select(t => (t.Tool, (IReadOnlyDictionary<string, string>)(t.Args ?? new Dictionary<string, string>())))
                .ToList();

            var answer = await assistant.AskAsync(request, toolCalls, ct);

            return Results.Ok(new
            {
                isRefusal = answer.IsRefusal,
                refusalReason = answer.RefusalReason,
                text = answer.Text,
                citations = answer.Citations.Select(h => new { kind = h.Kind.ToString(), id = h.Id, detail = h.Detail }).ToArray(),
                blocked = answer.BlockedSentences
            });
        });

        return app;
    }

    private static bool TryTenant(ClaimsPrincipal u, out Guid t)
    {
        t = Guid.Empty;
        var raw = u.FindFirst("tenant_id")?.Value;
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out t);
    }
    private static string Role(ClaimsPrincipal u) => u.FindFirst(ClaimTypes.Role)?.Value ?? u.FindFirst("role")?.Value ?? "viewer";
    private static string License(ClaimsPrincipal u) => u.FindFirst("license_tier")?.Value ?? u.FindFirst("license")?.Value ?? "";
}