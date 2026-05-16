using PlantProcess.Api.Extensions;
using PlantProcess.Application.Services.Analytics;
using PlantProcess.Application.Services.Analytics.Interfaces;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class FeatureEngineeringEndpoints
{
    public static IEndpointRouteBuilder MapFeatureEngineeringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/features")
            .WithTags("Feature Engineering");

        group.MapGet("/{materialUnitId:guid}", GetMaterialFeatureVectorAsync);

        return app;
    }

    private static async Task<IResult> GetMaterialFeatureVectorAsync(
        Guid materialUnitId,
        IFeatureEngineeringService service,
        CancellationToken cancellationToken)
    {
        var result = await service.BuildMaterialFeatureVectorAsync(materialUnitId, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}
