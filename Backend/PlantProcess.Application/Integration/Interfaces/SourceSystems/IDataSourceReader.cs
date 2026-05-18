using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Interfaces.SourceSystems;

/// <summary>
/// Reads rows from a configured source dataset.
///
/// The connector returns raw values only.
/// It does not create canonical entities.
/// It does not apply business mapping.
/// It does not normalize units.
///
/// That work belongs to mapping and canonical refresh services.
/// </summary>
public interface IDataSourceReader
{
    string ProviderType { get; }

    Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceIncrementalReadRequest request,
        CancellationToken cancellationToken);
}



