using PlantProcess.Api.Extensions;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

namespace PlantProcess.Api.Filters;

public sealed class LicenseFeatureEndpointFilter : IEndpointFilter
{
    private readonly LicenseFeature _feature;

    public LicenseFeatureEndpointFilter(LicenseFeature feature)
    {
        _feature = feature;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var licenseService = context.HttpContext.RequestServices.GetRequiredService<ILicenseService>();

        var gate = licenseService.EnsureFeatureEnabled(_feature);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        return await next(context);
    }
}