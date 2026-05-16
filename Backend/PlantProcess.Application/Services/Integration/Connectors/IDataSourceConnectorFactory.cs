namespace PlantProcess.Application.Services.Integration.Connectors;

/// <summary>
/// Resolves provider-specific connector components.
///
/// Implementation must live in Infrastructure.
/// Application services depend only on this interface.
/// </summary>
public interface IDataSourceConnectorFactory
{
    IDataSourceConnector GetConnector(string providerType);

    ISchemaReader GetSchemaReader(string providerType);

    IDataSourceReader GetDataSourceReader(string providerType);
}