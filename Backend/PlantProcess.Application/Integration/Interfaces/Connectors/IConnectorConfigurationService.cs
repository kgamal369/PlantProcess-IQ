using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Interfaces.Connectors;

public interface IConnectorConfigurationService
{
    IReadOnlyList<ProviderTypeDto> GetProviderTypes();

    Task<ApplicationResult<IReadOnlyList<ConnectionProfileDto>>> GetConnectionProfilesAsync(
        Guid? sourceSystemDefinitionId,
        string? providerType,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ConnectionProfileDto>> GetConnectionProfileByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ConnectionProfileDto>> CreateConnectionProfileAsync(
        CreateConnectionProfileRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ConnectionProfileDto>> UpdateConnectionProfileAsync(
        Guid id,
        UpdateConnectionProfileRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ConnectionProfileDto>> ActivateConnectionProfileAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ConnectionProfileDto>> DeactivateConnectionProfileAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ConnectionProfileDto>> TestConnectionProfileAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>> GetDatasetsAsync(
        Guid? connectionProfileId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SourceDatasetDefinitionDto>> CreateDatasetAsync(
        CreateSourceDatasetDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CsvSchemaDiscoveryResult>> DiscoverCsvSchemaAsync(
        Guid sourceDatasetDefinitionId,
        CsvSchemaDiscoveryRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CsvPreviewResult>> PreviewCsvAsync(
        Guid sourceDatasetDefinitionId,
        CsvPreviewRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CsvImportSnapshotResult>> ImportCsvSnapshotAsync(
        Guid sourceDatasetDefinitionId,
        CsvImportSnapshotRequest request,
        CancellationToken cancellationToken);
}


