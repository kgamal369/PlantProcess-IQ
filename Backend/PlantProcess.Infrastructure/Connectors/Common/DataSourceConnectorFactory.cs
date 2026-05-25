// ============================================================
// FILE: Backend/PlantProcess.Infrastructure/Connectors/Common/DataSourceConnectorFactory.cs
// CHANGES: Added MySQL and MSSQL provider type normalisation mappings.
// ============================================================

using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;

namespace PlantProcess.Infrastructure.Connectors.Common;

public sealed class DataSourceConnectorFactory : IDataSourceConnectorFactory
{
    private readonly IReadOnlyDictionary<string, IDataSourceConnector> _connectors;
    private readonly IReadOnlyDictionary<string, ISchemaReader> _schemaReaders;
    private readonly IReadOnlyDictionary<string, IDataSourceReader> _dataReaders;

    public DataSourceConnectorFactory(
        IEnumerable<IDataSourceConnector> connectors,
        IEnumerable<ISchemaReader> schemaReaders,
        IEnumerable<IDataSourceReader> dataReaders)
    {
        _connectors = connectors.ToDictionary(
            x => NormalizeProvider(x.ProviderType),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        _schemaReaders = schemaReaders.ToDictionary(
            x => NormalizeProvider(x.ProviderType),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        _dataReaders = dataReaders.ToDictionary(
            x => NormalizeProvider(x.ProviderType),
            x => x,
            StringComparer.OrdinalIgnoreCase);
    }

    public IDataSourceConnector GetConnector(string providerType)
    {
        var key = NormalizeProvider(providerType);

        if (_connectors.TryGetValue(key, out var connector))
            return connector;

        throw new NotSupportedException(
            $"No data-source connector is registered for provider type '{providerType}'. " +
            $"Supported types: csv, excel, postgresql, sqlserver, mysql, oracle.");
    }

    public ISchemaReader GetSchemaReader(string providerType)
    {
        var key = NormalizeProvider(providerType);

        if (_schemaReaders.TryGetValue(key, out var schemaReader))
            return schemaReader;

        throw new NotSupportedException(
            $"No schema reader is registered for provider type '{providerType}'.");
    }

    public IDataSourceReader GetDataSourceReader(string providerType)
    {
        var key = NormalizeProvider(providerType);

        if (_dataReaders.TryGetValue(key, out var dataReader))
            return dataReader;

        throw new NotSupportedException(
            $"No data reader is registered for provider type '{providerType}'.");
    }

    /// <summary>
    /// Normalises all recognised provider type aliases to a canonical key.
    /// New connectors should add their aliases here.
    /// </summary>
    private static string NormalizeProvider(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
            throw new ArgumentException("Provider type is required.", nameof(providerType));

        return providerType.Trim().ToLowerInvariant() switch
        {
            // File-based
            "csv"          => "csv",
            "csvfile"      => "csv",
            "excel"        => "excel",
            "excelsheet"   => "excel",
            "xlsx"         => "excel",

            // PostgreSQL
            "postgres"     => "postgresql",
            "postgresql"   => "postgresql",

            // SQL Server / MSSQL
            "sqlserver"    => "sqlserver",
            "mssql"        => "sqlserver",
            "microsoftsql" => "sqlserver",
            "sql server"   => "sqlserver",

            // MySQL / MariaDB
            "mysql"        => "mysql",
            "mariadb"      => "mysql",
            "maria"        => "mysql",

            // Oracle
            "oracle"       => "oracle",
            "ora"          => "oracle",
            "oracledb"     => "oracle",
            "oracle db"    => "oracle",

            // Fall-through for future connectors (oracle, etc.)
            _              => providerType.Trim().ToLowerInvariant()
        };
    }
}
