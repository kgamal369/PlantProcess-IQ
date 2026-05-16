using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Services.Integration.Connectors;

/// <summary>
/// Provider-level connector contract.
///
/// Implementations live in Infrastructure:
/// - CSV connector
/// - Excel connector
/// - PostgreSQL connector
/// - SQL Server connector
/// - Oracle connector
/// - MySQL connector
/// - REST API connector
///
/// Application depends only on this abstraction.
/// </summary>
public interface IDataSourceConnector
{
    string ProviderType { get; }

    Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken);

    string? GetLastError();
}