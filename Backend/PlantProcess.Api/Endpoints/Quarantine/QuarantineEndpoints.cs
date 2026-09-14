using Microsoft.AspNetCore.Mvc;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Integration.Interfaces.Mapping;

namespace PlantProcess.Api.Endpoints.Quarantine;

/// <summary>
/// PPIQ T-099. The bounded reprocess surface, and nothing else.
///
/// T-099 owns POST /api/quarantine/reprocess only. List, detail, dismiss and
/// Mapping Health are T-101's, and adding them here early would put unproven
/// product surface in front of a customer.
/// </summary>
public static class QuarantineEndpoints
{
    public static IEndpointRouteBuilder MapQuarantineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quarantine")
            .WithTags("Quarantine");

        group.MapPost("/reprocess", ReprocessAsync);

        return app;
    }

    private static async Task<IResult> ReprocessAsync(
        ReprocessQuarantineRequest request,
        [FromServices] IProjectionQuarantineReprocessService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ReprocessAsync(request.QuarantineId, request.TenantCode, cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }
}

public sealed record ReprocessQuarantineRequest(Guid QuarantineId, string? TenantCode);
