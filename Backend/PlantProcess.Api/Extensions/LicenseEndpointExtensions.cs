using PlantProcess.Api.Filters;
using PlantProcess.Application.Licensing.Contracts;

namespace PlantProcess.Api.Extensions;

public static class LicenseEndpointExtensions
{
    public static RouteHandlerBuilder RequireLicenseFeature(
        this RouteHandlerBuilder builder,
        LicenseFeature feature)
    {
        return builder.AddEndpointFilter(new LicenseFeatureEndpointFilter(feature));
    }

    public static RouteGroupBuilder RequireLicenseFeature(
        this RouteGroupBuilder builder,
        LicenseFeature feature)
    {
        builder.AddEndpointFilter(new LicenseFeatureEndpointFilter(feature));
        return builder;
    }
}