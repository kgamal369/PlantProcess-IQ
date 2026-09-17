using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Application.Temporal;
using PlantProcess.Infrastructure.Temporal;

namespace PlantProcess.Api.Endpoints.SourceTime;

/// <summary>
/// Canonical API for the frozen Source Time Authority contract (backlog reference:
/// T-233). The request and response carry exactly the contract fields plus an
/// effective window. Tenant identity comes only from TenantClaims, and every read
/// and write carries it. No settings surface and no alignment algorithm live here.
/// </summary>
public static class SourceTimeAuthorityEndpoints
{
    /// <summary>
    /// The collection route, written out whole. A group prefix plus a "/" child would
    /// register a trailing-slash pattern that a request to the collection never matches.
    /// The permission matrix row for this family is /api/source-time.
    /// </summary>
    public const string Route = "/api/source-time/authorities";

    private const string Tag = "Source Time Authority";

    public static IEndpointRouteBuilder MapSourceTimeAuthorityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Route, DeclareAsync).RequireAuthorization().WithTags(Tag);
        app.MapGet(Route, ListAsync).RequireAuthorization().WithTags(Tag);
        app.MapGet(Route + "/{sourceKey}/{signalKey}", GetAsync).RequireAuthorization().WithTags(Tag);

        return app;
    }

    private static async Task<IResult> DeclareAsync(
        SourceTimeAuthorityDeclarationRequest request,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var store = new SourceTimeAuthorityStore(dataSource);

        var result = await store.DeclareAsync(tenantId, request, cancellationToken);
        if (!result.IsAccepted || result.Id is null)
        {
            return MapRefusal(result);
        }

        var asOf = request.EffectiveFromUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.EffectiveFromUtc, DateTimeKind.Utc)
            : request.EffectiveFromUtc.ToUniversalTime();

        var stored = await store.FindInForceAsync(
            tenantId, request.SourceKey ?? string.Empty, request.SignalKey ?? string.Empty, asOf, cancellationToken);

        return stored is null || stored.Id != result.Id.Value
            ? Results.Problem(
                title: "Source time declaration was accepted but could not be read back",
                statusCode: StatusCodes.Status500InternalServerError)
            : Results.Ok(stored.ToResponse());
    }

    private static async Task<IResult> ListAsync(
        DateTime? asOfUtc,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var store = new SourceTimeAuthorityStore(dataSource);
        var rows = await store.ListInForceAsync(tenantId, AsOf(asOfUtc), cancellationToken);
        return Results.Ok(rows.Select(r => r.ToResponse()).ToArray());
    }

    private static async Task<IResult> GetAsync(
        string sourceKey,
        string signalKey,
        DateTime? asOfUtc,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var store = new SourceTimeAuthorityStore(dataSource);
        var row = await store.FindInForceAsync(tenantId, sourceKey, signalKey, AsOf(asOfUtc), cancellationToken);
        return row is null
            ? Results.NotFound(new { code = SourceTimeCodes.SignalNotDeclared })
            : Results.Ok(row.ToResponse());
    }

    private static DateTime AsOf(DateTime? asOfUtc)
    {
        if (!asOfUtc.HasValue)
        {
            return DateTime.UtcNow;
        }

        return asOfUtc.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(asOfUtc.Value, DateTimeKind.Utc)
            : asOfUtc.Value.ToUniversalTime();
    }

    private static IResult MapRefusal(SourceTimeAuthorityWriteResult result)
    {
        if (result.Code.StartsWith("ST07", StringComparison.Ordinal))
        {
            return Results.Conflict(new { code = result.Code, detail = result.Detail });
        }

        return Results.BadRequest(new { code = result.Code, detail = result.Detail });
    }
}