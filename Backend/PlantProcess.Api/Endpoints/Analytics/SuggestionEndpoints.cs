using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Analytics.Suggestions;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>T-048/T-049: real suggestion cards (ranked) + assign/accept/reject/close/comment workflow.</summary>
public static class SuggestionEndpoints
{
    public sealed record TransitionRequest(string? Note);

    public static IEndpointRouteBuilder MapSuggestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suggestions").WithTags("Suggestions").RequireAuthorization();

        group.MapGet("/cards", async (ClaimsPrincipal user, ISuggestionStore store, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var t)) return Results.BadRequest(new { error = "no_tenant" });
            var cards = await store.ListActiveAsync(t, ct);
            return Results.Ok(cards.Select(Project).ToArray());
        });

        foreach (var (verb, status) in new[]
        {
            ("assign", SuggestionStatus.Assigned), ("accept", SuggestionStatus.Accepted),
            ("reject", SuggestionStatus.Rejected), ("close", SuggestionStatus.Closed),
        })
        {
            group.MapPost($"/{{id:guid}}/{verb}", async (Guid id, TransitionRequest? body, ClaimsPrincipal user, ISuggestionStore store, CancellationToken ct) =>
            {
                if (!TryTenant(user, out var t)) return Results.BadRequest(new { error = "no_tenant" });
                var decision = await store.TransitionAsync(t, id, status, Role(user), user.Identity?.Name ?? "unknown", body?.Note, ct);
                return decision.Allowed ? Results.Ok(new { id, status = status.ToString() }) : Results.Json(new { error = "transition_denied", reason = decision.Reason }, statusCode: StatusCodes.Status403Forbidden);
            });
        }

        return app;
    }

    private static object Project(SuggestionCard c) => new
    {
        id = c.Id,
        title = c.Title,
        actionType = c.ActionType,
        status = c.Status.ToString(),
        confidence = c.Confidence,
        impactLow = c.ImpactLow,
        impactHigh = c.ImpactHigh,
        honestyText = c.HonestyText,
        unsupported = c.EvidenceHandles.Count == 0, // a card whose evidence does not resolve is shown, not dropped
        evidence = c.EvidenceHandles.Select(h => new { kind = h.Kind.ToString(), id = h.Id, detail = h.Detail }).ToArray(),
        sourceFindings = c.SourceFindingRefs
    };

    private static bool TryTenant(ClaimsPrincipal u, out Guid t)
    {
        t = Guid.Empty;
        var raw = u.FindFirst("tenant_id")?.Value;
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out t);
    }

    private static string Role(ClaimsPrincipal u) =>
        u.FindFirst(ClaimTypes.Role)?.Value ?? u.FindFirst("role")?.Value ?? "viewer";
}