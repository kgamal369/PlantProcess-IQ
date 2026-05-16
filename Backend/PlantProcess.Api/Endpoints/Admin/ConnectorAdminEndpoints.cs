using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Services.Integration;

namespace PlantProcess.Api.Endpoints.Admin;

/// <summary>
/// Phase 3 — Connector Foundation API.
/// 
/// This group powers the Admin / DB Configuration page.
/// It introduces generic connection profiles, source datasets,
/// CSV schema discovery, CSV preview and CSV snapshot import.
/// </summary>
public static class ConnectorAdminEndpoints
{
    public static IEndpointRouteBuilder MapConnectorAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/connectors")
            .WithTags("Admin — Connectors");

        group.MapGet("/provider-types", GetProviderTypes)
            .WithSummary("Get supported connector provider types");

        group.MapGet("/connection-profiles", GetConnectionProfilesAsync)
            .WithSummary("Get connection profiles");

        group.MapGet("/connection-profiles/{id:guid}", GetConnectionProfileByIdAsync)
            .WithSummary("Get connection profile by ID");

        group.MapPost("/connection-profiles", CreateConnectionProfileAsync)
            .WithSummary("Create connection profile");

        group.MapPut("/connection-profiles/{id:guid}", UpdateConnectionProfileAsync)
            .WithSummary("Update connection profile");

        group.MapPatch("/connection-profiles/{id:guid}/activate", ActivateConnectionProfileAsync)
            .WithSummary("Activate connection profile");

        group.MapPatch("/connection-profiles/{id:guid}/deactivate", DeactivateConnectionProfileAsync)
            .WithSummary("Deactivate connection profile");

        group.MapPost("/connection-profiles/{id:guid}/test", TestConnectionProfileAsync)
            .WithSummary("Test connection profile");

        group.MapGet("/datasets", GetDatasetsAsync)
            .WithSummary("Get source datasets");

        group.MapPost("/datasets", CreateDatasetAsync)
            .WithSummary("Create source dataset");

        group.MapPost("/datasets/{id:guid}/discover-csv-schema", DiscoverCsvSchemaAsync)
            .WithSummary("Discover CSV schema");

        group.MapPost("/datasets/{id:guid}/preview-csv", PreviewCsvAsync)
            .WithSummary("Preview CSV rows");

        group.MapPost("/datasets/{id:guid}/import-csv-snapshot", ImportCsvSnapshotAsync)
            .WithSummary("Import CSV snapshot to raw staging records");

        return app;
    }

    private static IResult GetProviderTypes(IConnectorConfigurationService service)
    {
        return Results.Ok(service.GetProviderTypes());
    }

    private static async Task<IResult> GetConnectionProfilesAsync(
        Guid? sourceSystemDefinitionId,
        string? providerType,
        bool? includeInactive,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetConnectionProfilesAsync(
            sourceSystemDefinitionId,
            providerType,
            includeInactive ?? true,
            cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> GetConnectionProfileByIdAsync(
        Guid id,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetConnectionProfileByIdAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> CreateConnectionProfileAsync(
        CreateConnectionProfileRequest request,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateConnectionProfileAsync(request, cancellationToken);

        return result.ToHttpResult(value =>
            Results.Created($"/admin/connectors/connection-profiles/{value.Id}", value));
    }

    private static async Task<IResult> UpdateConnectionProfileAsync(
        Guid id,
        UpdateConnectionProfileRequest request,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateConnectionProfileAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ActivateConnectionProfileAsync(
        Guid id,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateConnectionProfileAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> DeactivateConnectionProfileAsync(
        Guid id,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateConnectionProfileAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> TestConnectionProfileAsync(
        Guid id,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.TestConnectionProfileAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> GetDatasetsAsync(
        Guid? connectionProfileId,
        bool? includeInactive,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDatasetsAsync(
            connectionProfileId,
            includeInactive ?? true,
            cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> CreateDatasetAsync(
        CreateSourceDatasetDefinitionRequest request,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateDatasetAsync(request, cancellationToken);

        return result.ToHttpResult(value =>
            Results.Created($"/admin/connectors/datasets/{value.Id}", value));
    }

    private static async Task<IResult> DiscoverCsvSchemaAsync(
        Guid id,
        CsvSchemaDiscoveryRequest request,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DiscoverCsvSchemaAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> PreviewCsvAsync(
        Guid id,
        CsvPreviewRequest request,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.PreviewCsvAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ImportCsvSnapshotAsync(
        Guid id,
        CsvImportSnapshotRequest request,
        IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ImportCsvSnapshotAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }
}