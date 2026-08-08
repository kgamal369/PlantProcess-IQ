using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Assistant;

using PlantProcess.Api.ErrorHandling;
namespace PlantProcess.Api.Endpoints.Assistant;

/// <summary>T-053/T-054 backend: grounded ask + admin reindex. Tenant/role/license come from the caller's claims.</summary>
public static class AssistantEndpoints
{
    public sealed record AskRequest(
        string Question,
        IReadOnlyList<string>? ContextChips,
        IReadOnlyList<ToolCallDto>? Tools,
        ContextEnvelopeDto? Context = null);

    /// <summary>T-072 wire shape of the page and widget context envelope.</summary>
    public sealed record ContextEnvelopeDto(
        string? Route = null,
        string? PageCode = null,
        string? WidgetCode = null,
        IReadOnlyList<string>? Selections = null,
        IReadOnlyList<string>? Filters = null,
        string? LastResultSummary = null,
        IReadOnlyList<string>? EvidenceHandles = null);
    public sealed record ToolCallDto(string Tool, Dictionary<string, string>? Args);

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assistant").WithTags("Assistant").RequireAuthorization();

        group.MapPost("/ask", async ([FromBody] AskRequest req, ClaimsPrincipal user, [FromServices] AssistantService assistant, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return ApplicationProblems.Validation("no_tenant");

            var envelope = req.Context is null ? null : new AssistantContextEnvelope(
                req.Context.Route,
                req.Context.PageCode,
                req.Context.WidgetCode,
                req.Context.Selections,
                req.Context.Filters,
                req.Context.LastResultSummary,
                req.Context.EvidenceHandles);

            var request = new AssistantRequest(
                tenantId, Role(user), License(user), req.Question ?? string.Empty,
                req.ContextChips ?? Array.Empty<string>(), envelope);

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

        group.MapPost("/reindex", async (ClaimsPrincipal user, [FromServices] IAssistantChunkProducer producer, [FromServices] IRetrievalIndex index, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return ApplicationProblems.Validation("no_tenant");

            var correlationId = Guid.NewGuid().ToString("N");
            var chunks = await producer.BuildAsync(tenantId, ct);
            var result = await index.ReindexAsync(new ReindexRequest(tenantId, chunks, correlationId), ct);

            var scoped = chunks.Count(ch => !string.IsNullOrEmpty(ch.ScopeRole));
            return Results.Ok(new
            {
                chunkCount = result.ChunkCount,
                replaced = result.ReplacedCount,
                engineerScopedChunks = scoped,
                correlationId = result.CorrelationId,
                bySource = chunks.GroupBy(ch => ch.SourceKind)
                                 .ToDictionary(g => g.Key, g => g.Count())
            });
        });

        group.MapGet("/evidence/widget-result/{evidenceId:guid}", async (
            Guid evidenceId,
            ClaimsPrincipal user,
            [FromServices] IWidgetResultEvidenceReader reader,
            CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return ApplicationProblems.Validation("no_tenant");

            var snapshot = await reader.ReadAsync(tenantId, evidenceId, ct);

            // T-073 validation point 4, and the tenant boundary in one place. The
            // predicate carries tenant identity AND evidence identity, so a handle
            // belonging to another tenant is UNAVAILABLE here - never content, and
            // never a different tenant's numbers.
            if (snapshot is null)
            {
                return Results.NotFound(new
                {
                    evidenceId,
                    available = false,
                    reason = "No widget result evidence with that identity is available to this tenant."
                });
            }

            return Results.Ok(new
            {
                evidenceId = snapshot.EvidenceId,
                available = true,
                pageCode = snapshot.Identity.PageCode,
                widgetCode = snapshot.Identity.WidgetCode,
                widgetDefinitionId = snapshot.Identity.WidgetDefinitionId,
                widgetType = snapshot.Identity.WidgetType,
                chartType = snapshot.Identity.ChartType,
                dimensionCode = snapshot.Identity.DimensionCode,
                measureCode = snapshot.Identity.MeasureCode,
                parameterCode = snapshot.Identity.ParameterCode,
                queryFingerprint = snapshot.QueryFingerprint,
                resultFingerprint = snapshot.ResultFingerprint,
                filterContext = snapshot.FilterContextJson,
                generatedAtUtc = snapshot.GeneratedAtUtc,
                columns = snapshot.Result.Columns,
                rows = snapshot.Result.Rows,
                hasObservationCount = snapshot.Result.HasObservationCount,
                observationCountTotal = snapshot.Result.ObservationCountTotal,
                sentence = WidgetResultEvidence.Sentence(snapshot.Identity, snapshot.Result)
            });
        });

        return app;
    }

    private static bool TryTenant(ClaimsPrincipal u, out Guid t)
    {
        return PlantProcess.Application.Security.Tenancy.TenantClaims.TryResolve(u, out t);
    }
    private static string Role(ClaimsPrincipal u) => u.FindFirst(ClaimTypes.Role)?.Value ?? u.FindFirst("role")?.Value ?? "viewer";
    private static string License(ClaimsPrincipal u) => u.FindFirst("license_tier")?.Value ?? u.FindFirst("license")?.Value ?? "";
}

