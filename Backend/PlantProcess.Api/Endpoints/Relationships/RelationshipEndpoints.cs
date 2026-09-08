using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Api.Endpoints.Relationships;

/// <summary>
/// T-057. C6's read surface.
///
/// There is deliberately NO create endpoint. A relationship is emitted by
/// publishing a transformation definition; an authoring endpoint here would be a
/// public M1 contract that M2 has to delete, which the product model forbids.
///
/// Of the write controls Chapter 3 describes for C6, only VALIDATE is exposed here.
/// It exists because the first automated consumers of the relationship model refuse
/// an unproven relationship by contract, and without a way to prove one the model
/// would be usable only by hand. Preferred-path and retire controls still belong
/// with the tasks that own the behaviour behind them.
/// </summary>
public static class RelationshipEndpoints
{
    public static IEndpointRouteBuilder MapRelationshipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/relationships")
            .RequireAuthorization()
            .WithTags("Relationships");

        group.MapGet("/", async (
            string? entity,
            IRelationshipService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetPublishedAsync(entity, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(title: result.Error!.Code, detail: result.Error!.Message, statusCode: StatusCodes.Status400BadRequest);
        });

        group.MapGet("/entities", async (
            IRelationshipService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetEntitiesAsync(cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(title: result.Error!.Code, detail: result.Error!.Message, statusCode: StatusCodes.Status400BadRequest);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IRelationshipService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken);
            if (result.IsSuccess) return Results.Ok(result.Value);

            return result.Error!.Type == PlantProcess.Application.Common.Results.ApplicationErrorType.NotFound
                ? Results.NotFound(new { code = result.Error!.Code, message = result.Error!.Message })
                : Results.Problem(title: result.Error!.Code, detail: result.Error!.Message, statusCode: StatusCodes.Status400BadRequest);
        });

        group.MapPost("/{id:guid}/validate", async (
            Guid id,
            IRelationshipValidationService validation,
            CancellationToken cancellationToken) =>
        {
            // The body is empty on purpose. The server derives the state from evidence;
            // a request that could name the state it wanted would be a way to assert
            // proof without producing it.
            try
            {
                var result = await validation.ValidateAsync(id, cancellationToken);
                if (result.IsSuccess) return Results.Ok(result.Value);

                return result.Error!.Type == PlantProcess.Application.Common.Results.ApplicationErrorType.NotFound
                    ? Results.NotFound(new { code = result.Error!.Code, message = result.Error!.Message })
                    : Results.Problem(title: result.Error!.Code, detail: result.Error!.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (RelationshipPlanExecutionException unexecutable)
            {
                // The relationship could not be evaluated against the mapped model. Nothing
                // was persisted; the plant is told which member did not map.
                return Results.Problem(
                    title: RelationshipPlanExecutionException.Reason,
                    detail: unexecutable.Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        });

        return app;
    }
}
