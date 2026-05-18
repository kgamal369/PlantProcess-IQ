using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;

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


