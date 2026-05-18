using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;

namespace PlantProcess.Application.Integration.Interfaces.SourceSystems;

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


