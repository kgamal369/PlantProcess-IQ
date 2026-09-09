using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;

namespace PlantProcess.Api.Endpoints.Prep;

/// <summary>
/// PPIQ T-244. THE TRANSPORT ADAPTER FOR THE CANVAS DEFINITION LIFECYCLE.
///
/// This file owns no lifecycle logic. It resolves identity exactly the way the
/// T-091 definition endpoints do, shapes the request, and delegates to
/// ICanvasDefinitionLifecycle. The two legacy endpoints - graph publish and
/// SQL version save - keep their routes and their response shapes and call
/// the same two helpers below, so a client built before this task is still a
/// valid client and the browser makes ONE governed request per operation.
///
/// The reopen route is the minimum identity-continuity surface: a definition
/// code resolves to its canonical identity and representation without any
/// mutation. It is not a browser and it is not a management UI.
/// </summary>
public static class CanvasDefinitionEndpoints
{
    public static IEndpointRouteBuilder MapCanvasDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prep/definitions")
            .WithTags("Prep - Canvas definitions")
            .RequireAuthorization();

        group.MapGet("/{code}", ReopenAsync);
        group.MapGet("/{code}/versions", ListVersionsAsync);
        group.MapGet("/{code}/versions/{version:int}", ReopenVersionAsync);

        return app;
    }

    /// <summary>
    /// T-253. OutputTarget is nullable because a legacy SQL definition genuinely has
    /// none. The browser must show that absence and ask, never fill it in.
    /// </summary>
    public sealed record CanvasDefinitionResponse(
        Guid DefinitionId,
        Guid VersionId,
        string DefinitionCode,
        int VersionNumber,
        string Status,
        string DefinitionHash,
        string Representation,
        JsonElement? Graph,
        string? Sql,
        JsonElement? ForkedFromGraph,
        string? OutputTarget,
        // T-243. Null for a version saved before boards were persisted. The surface
        // states that rather than fabricating a layout for it.
        JsonElement? Board);

    public static CanvasDefinitionResponse ToResponse(CanvasDefinitionVersion version)
    {
        var representation = version.Representation;
        return new CanvasDefinitionResponse(
            version.DefinitionId,
            version.VersionId,
            version.DefinitionCode,
            version.VersionNumber,
            version.Status,
            version.DefinitionHash,
            representation.Representation,
            ParseOrNull(representation.GraphJson),
            representation.Sql,
            ParseOrNull(representation.ForkedFromGraphJson),
            representation.OutputTarget,
            ParseOrNull(representation.BoardJson));
    }

    /// <summary>
    /// T-243. Read-only history. It answers which versions exist so a person can choose
    /// one; choosing is the whole of rollback under the canonical lifecycle, which
    /// republishes an existing governed version and never rewrites an old one.
    /// </summary>
    private static async Task<IResult> ListVersionsAsync(
        string code,
        ClaimsPrincipal user,
        [FromServices] ICanonicalIdentityResolver identity,
        [FromServices] ICanvasDefinitionLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantAsync(user, identity, cancellationToken);
        if (tenantId is null)
        {
            return Results.Forbid();
        }

        var listed = await lifecycle.ListVersionsAsync(tenantId.Value, code, cancellationToken);
        return listed.IsFailure
            ? Refusal(listed.Error!)
            : Results.Ok(new { definitionCode = code, versions = listed.Value });
    }

    private static Task<IResult> ReopenAsync(
        string code,
        ClaimsPrincipal user,
        [FromServices] ICanonicalIdentityResolver identity,
        [FromServices] ICanvasDefinitionLifecycle lifecycle,
        CancellationToken cancellationToken)
        => ReopenCoreAsync(code, null, user, identity, lifecycle, cancellationToken);

    private static Task<IResult> ReopenVersionAsync(
        string code,
        int version,
        ClaimsPrincipal user,
        [FromServices] ICanonicalIdentityResolver identity,
        [FromServices] ICanvasDefinitionLifecycle lifecycle,
        CancellationToken cancellationToken)
        => ReopenCoreAsync(code, version, user, identity, lifecycle, cancellationToken);

    private static async Task<IResult> ReopenCoreAsync(
        string code,
        int? version,
        ClaimsPrincipal user,
        ICanonicalIdentityResolver identity,
        ICanvasDefinitionLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantAsync(user, identity, cancellationToken);
        if (tenantId is null)
        {
            return Results.Forbid();
        }

        var reopened = await lifecycle.ReopenAsync(tenantId.Value, code, version, cancellationToken);
        return reopened.IsFailure
            ? Refusal(reopened.Error!)
            : Results.Ok(ToResponse(reopened.Value!));
    }

    /// <summary>
    /// Resolves the canonical tenant and owner the writer requires, the same
    /// way the T-091 endpoints do. Null means the caller's identity does not
    /// resolve to real ids; nothing is invented in its place.
    /// </summary>
    public static async Task<(Guid TenantId, Guid OwnerId)?> ResolveIdentityAsync(
        ClaimsPrincipal user,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantAsync(user, identity, cancellationToken);
        var ownerId = await identity.ResolveOwnerAsync(user.Identity?.Name, cancellationToken);

        if (tenantId is null || ownerId is null)
        {
            return null;
        }

        return (tenantId.Value, ownerId.Value);
    }

    private static Task<Guid?> ResolveTenantAsync(
        ClaimsPrincipal user,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        var claim = user.FindFirst("tenant_code")?.Value;
        return identity.ResolveTenantAsync(string.IsNullOrWhiteSpace(claim) ? null : claim, cancellationToken);
    }

    public static IResult Refusal(ApplicationError error)
    {
        var message = error.Message;
        return error.Type switch
        {
            ApplicationErrorType.NotFound => Results.NotFound(new { message }),
            ApplicationErrorType.Forbidden => Results.Forbid(),
            ApplicationErrorType.Conflict => Results.Conflict(new { message }),
            _ => Results.BadRequest(new { message }),
        };
    }

    private static JsonElement? ParseOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
