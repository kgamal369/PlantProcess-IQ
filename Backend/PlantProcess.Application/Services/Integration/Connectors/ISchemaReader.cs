using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Contracts.Integration.Dtos;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Services.Integration.Connectors;

/// <summary>
/// Discovers datasets and fields from a configured source connection.
/// </summary>
public interface ISchemaReader
{
    string ProviderType { get; }

    Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DiscoveredSourceField>> DiscoverFieldsForDatasetAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        CancellationToken cancellationToken);
}