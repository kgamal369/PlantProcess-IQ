using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Infrastructure.Integration.Acquisition;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Integration;

/// <summary>
/// Industrial Integration dataset authority: explicit governance, stable fields,
/// raw layout revisions and acquisition configuration versions. Tenant identity
/// comes only from TenantClaims; every read and write carries it. The permission
/// matrix row for this family is /api/datasets (source.configure). No route here
/// starts acquisition or reports a running session.
/// </summary>
public static class IndustrialAcquisitionEndpoints
{
    public const string Family = "/api/datasets";

    private const string Tag = "Industrial Integration - datasets";

    public sealed record FieldsRequest(IReadOnlyList<FieldDeclarationRequest>? Fields);

    public sealed record LayoutRequest(JsonElement? Document);

    public sealed record ConfigurationRequest(JsonElement? Content, bool Publish);

    public static IEndpointRouteBuilder MapIndustrialAcquisitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Family + "/{datasetId:guid}")
            .WithTags(Tag)
            .RequireAuthorization();

        group.MapPost("/governance", GovernAsync);
        group.MapGet("/fields", ListFieldsAsync);
        group.MapPut("/fields", DeclareFieldsAsync);
        group.MapGet("/layouts", ListLayoutsAsync);
        group.MapPost("/layouts", DeclareLayoutAsync);
        group.MapGet("/layouts/{revision:int}", GetLayoutAsync);
        group.MapGet("/acquisition-configurations", ListConfigurationsAsync);
        group.MapPost("/acquisition-configurations", CreateConfigurationAsync);
        group.MapGet("/acquisition-configurations/{version:int}", GetConfigurationAsync);
        group.MapPost("/acquisition-configurations/{version:int}/validate", ValidateAsync);
        group.MapPost("/acquisition-configurations/{version:int}/activate", ActivateAsync);

        return app;
    }

    private static async Task<IResult> GovernAsync(
        Guid datasetId, HttpContext httpContext, PlantProcessDbContext db, ICanonicalDefinitionWriter writer,
        ICanonicalIdentityResolver identity, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var actor = await identity.ResolveOwnerAsync(httpContext.User.Identity?.Name, cancellationToken);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.GovernAsync(tenantId, datasetId, actor, cancellationToken));
    }

    private static async Task<IResult> ListFieldsAsync(
        Guid datasetId, bool? includeHistory, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.ListFieldsAsync(tenantId, datasetId, includeHistory == true, cancellationToken));
    }

    private static async Task<IResult> DeclareFieldsAsync(
        Guid datasetId, FieldsRequest request, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, ICanonicalIdentityResolver identity, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var actor = await identity.ResolveOwnerAsync(httpContext.User.Identity?.Name, cancellationToken);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.DeclareFieldsAsync(tenantId, datasetId, request?.Fields, actor, cancellationToken));
    }

    private static async Task<IResult> ListLayoutsAsync(
        Guid datasetId, HttpContext httpContext, PlantProcessDbContext db, ICanonicalDefinitionWriter writer,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.ListLayoutsAsync(tenantId, datasetId, null, cancellationToken));
    }

    private static async Task<IResult> GetLayoutAsync(
        Guid datasetId, int revision, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        var result = await service.ListLayoutsAsync(tenantId, datasetId, revision, cancellationToken);
        return result.IsAccepted ? Results.Ok(result.Value![0]) : Refusal(result.Refusal!);
    }

    private static async Task<IResult> DeclareLayoutAsync(
        Guid datasetId, LayoutRequest request, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, ICanonicalIdentityResolver identity, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var actor = await identity.ResolveOwnerAsync(httpContext.User.Identity?.Name, cancellationToken);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.DeclareLayoutAsync(tenantId, datasetId, request?.Document, actor, cancellationToken));
    }

    private static async Task<IResult> ListConfigurationsAsync(
        Guid datasetId, HttpContext httpContext, PlantProcessDbContext db, ICanonicalDefinitionWriter writer,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.ListVersionsAsync(tenantId, datasetId, cancellationToken));
    }

    private static async Task<IResult> CreateConfigurationAsync(
        Guid datasetId, ConfigurationRequest request, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, ICanonicalIdentityResolver identity, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var owner = await identity.ResolveOwnerAsync(httpContext.User.Identity?.Name, cancellationToken);
        if (owner is null)
        {
            return Results.Problem(
                title: "The caller does not resolve to an owner identity; nothing is written under an invented owner.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.CreateVersionAsync(
            tenantId, owner.Value, datasetId, request?.Content, request?.Publish == true, cancellationToken));
    }

    private static async Task<IResult> GetConfigurationAsync(
        Guid datasetId, int version, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.ResolveAsync(tenantId, datasetId, version, cancellationToken));
    }

    private static async Task<IResult> ValidateAsync(
        Guid datasetId, int version, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        return Answer(await service.ValidateAsync(tenantId, datasetId, version, cancellationToken));
    }

    private static async Task<IResult> ActivateAsync(
        Guid datasetId, int version, HttpContext httpContext, PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var service = new AcquisitionConfigurationService(db, writer);
        var result = await service.ActivateAsync(tenantId, datasetId, version, cancellationToken);
        if (!result.IsAccepted)
        {
            return Refusal(result.Refusal!);
        }

        // Never 200: an unadmitted activation is a typed refusal that still names
        // the exact target it resolved.
        return result.Value!.Admitted
            ? Results.Ok(result.Value)
            : Results.Json(result.Value, statusCode: StatusCodes.Status409Conflict);
    }

    private static IResult Answer<T>(AcquisitionOutcome<T> outcome) =>
        outcome.IsAccepted ? Results.Ok(outcome.Value) : Refusal(outcome.Refusal!);

    private static IResult Refusal(AcquisitionRefusal refusal)
    {
        var code = refusal.Code.Split(' ')[0];
        var body = new { code = refusal.Code, detail = refusal.Detail };
        return code switch
        {
            "IAG01" or "IAG03" or "IAF06" or "IAL02" or "IAC06" => Results.NotFound(body),
            "IAG02" or "IAF02" or "IAF03" or "IAF04" or "IAF05" or "IAC03" or "IAC07" => Results.Conflict(body),
            _ => Results.BadRequest(body)
        };
    }
}
