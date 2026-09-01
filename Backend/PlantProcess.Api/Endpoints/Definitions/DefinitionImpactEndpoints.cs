using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;

namespace PlantProcess.Api.Endpoints.Definitions;

/// <summary>
/// PPIQ T-091. The canonical definition group: impact preview and portability.
///
/// ONE GROUP, NOT A SECOND NAMESPACE. /api/definitions is the canonical
/// definition route and everything T-091 exposes hangs from it. A separate
/// /api/portability would make the same definitions addressable two ways.
///
/// TENANT COMES FROM THE AUTHENTICATED CONTEXT, NEVER THE CALLER'S BODY. An
/// import artifact carries the source tenant as provenance; if that value could
/// select the target, a package would be able to write into a tenant the caller
/// cannot see.
/// </summary>
public static class DefinitionImpactEndpoints
{
    public static IEndpointRouteBuilder MapDefinitionImpactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/definitions")
            .WithTags("Definitions")
            .RequireAuthorization();

        group.MapGet("/{definitionId:guid}/impact", GetImpactAsync)
            .WithSummary("Preview what depends on a definition")
            .WithDescription(
                "Reverse dependency walk over the canonical graph: everything that consumes this definition, " +
                "directly or transitively, before a change is published. Read-only.");

        group.MapGet("/{definitionId:guid}/export", ExportAsync)
            .WithSummary("Export a portable definition artifact")
            .WithDescription(
                "The definition plus the dependency closure required to reproduce its semantics, as a " +
                "deterministic environment-independent package. Omitting version exports the published version.");

        group.MapPost("/import", ImportAsync)
            .WithSummary("Import a portable definition artifact")
            .WithDescription(
                "Installs one artifact as a single unit of work through the canonical definition authority. " +
                "Refuses conflicts rather than overwriting established definitions.");

        return app;
    }

    private static async Task<Results<Ok<DefinitionImpact>, NotFound<string>, BadRequest<string>>> GetImpactAsync(
        Guid definitionId,
        ClaimsPrincipal user,
        ICanonicalDefinitionGraph graph,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantAsync(user, identity, cancellationToken);
        if (tenantId is null)
        {
            return TypedResults.BadRequest("No tenant identity could be resolved for this caller.");
        }

        var impact = await graph.PreviewImpactAsync(tenantId.Value, definitionId, cancellationToken);
        if (impact.IsFailure)
        {
            return TypedResults.NotFound(impact.Error!.Message);
        }

        return TypedResults.Ok(impact.Value!);
    }

    private static async Task<Results<Ok<string>, NotFound<string>, BadRequest<string>>> ExportAsync(
        Guid definitionId,
        int? version,
        ClaimsPrincipal user,
        IDefinitionPortability portability,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantAsync(user, identity, cancellationToken);
        if (tenantId is null)
        {
            return TypedResults.BadRequest("No tenant identity could be resolved for this caller.");
        }

        var exported = await portability.ExportAsync(tenantId.Value, definitionId, version, cancellationToken);
        if (exported.IsFailure)
        {
            return TypedResults.NotFound(exported.Error!.Message);
        }

        return TypedResults.Ok(DefinitionArtifactCanonicalizer.ToTransportJson(exported.Value!));
    }

    private static async Task<Results<Ok<DefinitionImportResult>, BadRequest<string>, Conflict<string>>> ImportAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        IDefinitionPortability portability,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantAsync(user, identity, cancellationToken);
        var ownerId = await identity.ResolveOwnerAsync(user.Identity?.Name, cancellationToken);

        if (tenantId is null || ownerId is null)
        {
            return TypedResults.BadRequest("No tenant or owner identity could be resolved for this caller.");
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var artifact = DefinitionArtifactCanonicalizer.FromTransportJson(body);
        if (artifact is null)
        {
            return TypedResults.BadRequest("The request body is not a readable definition artifact.");
        }

        var imported = await portability.ImportAsync(tenantId.Value, ownerId.Value, artifact, cancellationToken);
        if (imported.IsFailure)
        {
            var message = imported.Error!.Message;
            return message.Contains("IMPORT_CONFLICT", StringComparison.Ordinal)
                ? TypedResults.Conflict(message)
                : TypedResults.BadRequest(message);
        }

        return TypedResults.Ok(imported.Value!);
    }

    private static Task<Guid?> ResolveTenantAsync(
        ClaimsPrincipal user,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        var claim = user.FindFirst("tenant_code")?.Value;
        return identity.ResolveTenantAsync(string.IsNullOrWhiteSpace(claim) ? null : claim, cancellationToken);
    }
}
