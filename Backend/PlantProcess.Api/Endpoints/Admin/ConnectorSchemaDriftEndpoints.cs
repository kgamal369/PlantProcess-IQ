using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Integration.Services.SourceSystems;

namespace PlantProcess.Api.Endpoints.Admin;

public static class ConnectorSchemaDriftEndpoints
{
    public static IEndpointRouteBuilder MapConnectorSchemaDriftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/connectors")
            // PPIQ-T14: license paywall - tier resolution via ILicenseService (toggle with T22 ForceTier)
            .RequireLicenseFeature(LicenseFeature.DbLinkConfiguration)
            .WithTags("Admin - Connectors")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapPost("/datasets/{datasetId:guid}/schema-drift-check", CheckSchemaDriftAsync)
            .WithSummary("Check a dataset for upstream schema drift")
            .WithDescription("Compares the persisted field baseline against the live source schema, returns typed drift findings naming each changed column, and (by default) degrades dependent mappings.");

        return app;
    }

    private static async Task<IResult> CheckSchemaDriftAsync(
        Guid datasetId,
        SchemaDriftCheckRequest? request,
        IConnectorSchemaDriftService driftService,
        CancellationToken cancellationToken)
    {
        var degrade = request?.DegradeAffectedMappings ?? true;

        var result = await driftService.CheckDatasetAsync(datasetId, degrade, cancellationToken);

        return string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, statusCode: StatusCodes.Status409Conflict);
    }

    public sealed record SchemaDriftCheckRequest(bool DegradeAffectedMappings);
}