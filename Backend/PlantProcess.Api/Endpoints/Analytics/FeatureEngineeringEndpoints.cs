using PlantProcess.Api.Extensions;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Licensing.Contracts;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class FeatureEngineeringEndpoints
{
    public static IEndpointRouteBuilder MapFeatureEngineeringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/features")
            .WithTags("Feature Engineering")
            .RequireLicenseFeature(LicenseFeature.MlWorkspacePreview);

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


