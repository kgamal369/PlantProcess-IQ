using System.Globalization;
using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Connectors.PostgreSql;

public sealed class PostgreSqlConnector : IDataSourceConnector, ISchemaReader, IDataSourceReader
{
    private string? _lastError;

    public string ProviderType => "PostgreSql";

    public async Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(BuildConnectionString(connectionProfile));
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT current_database(), current_user, version();", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var metadata = new Dictionary<string, string?>();

            if (await reader.ReadAsync(cancellationToken))
            {
                metadata["database"] = reader.GetString(0);
                metadata["user"] = reader.GetString(1);
                metadata["version"] = reader.GetString(2);
            }

            metadata["readOnlyEnforced"] = connectionProfile.ReadOnlyEnforced.ToString(CultureInfo.InvariantCulture);

            return Success("PostgreSQL connection succeeded.", metadata);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Failure($"PostgreSQL connection test failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        var schemaName = string.IsNullOrWhiteSpace(connectionProfile.SchemaName)
            ? "public"
            : connectionProfile.SchemaName.Trim();

        const string sql = """
            SELECT table_schema, table_name, table_type
            FROM information_schema.tables
            WHERE table_schema = @schema
              AND table_type IN ('BASE TABLE', 'VIEW')
            ORDER BY table_schema, table_name;
            """;

        var datasets = new List<DiscoveredSourceDataset>();

        await using var connection = new NpgsqlConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schemaName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var kind = reader.GetString(2).Equals("VIEW", StringComparison.OrdinalIgnoreCase)
                ? "PostgreSqlView"
                : "PostgreSqlTable";

            datasets.Add(new DiscoveredSourceDataset(
                DatasetCode: NormalizeCode($"{schema}_{table}"),
                DatasetName: $"{schema}.{table}",
                DatasetKind: kind,
                SourceObjectName: table,
                SourceSchemaName: schema,
                DatasetOptionsJson: JsonSerializer.Serialize(new
                {
                    schema,
                    table,
                    provider = "PostgreSql"
                })));
        }

        return datasets;
    }

    public async Task<IReadOnlyList<DiscoveredSourceField>> DiscoverFieldsForDatasetAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        CancellationToken cancellationToken)
    {
        var schemaName = string.IsNullOrWhiteSpace(datasetDefinition.SourceSchemaName)
            ? connectionProfile.SchemaName ?? "public"
            : datasetDefinition.SourceSchemaName;

        const string sql = """
            SELECT
                c.column_name,
                c.data_type,
                c.ordinal_position,
                c.is_nullable,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale
            FROM information_schema.columns c
            WHERE c.table_schema = @schema
              AND c.table_name = @table
            ORDER BY c.ordinal_position;
            """;

        var fields = new List<DiscoveredSourceField>();

        await using var connection = new NpgsqlConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schemaName);
        command.Parameters.AddWithValue("table", datasetDefinition.SourceObjectName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var fieldName = reader.GetString(0);
            var sourceType = reader.GetString(1);
            var ordinal = reader.GetInt32(2);
            var nullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase);

            fields.Add(new DiscoveredSourceField(
                FieldName: fieldName,
                DisplayName: fieldName,
                SourceDataType: MapPostgresType(sourceType),
                Ordinal: ordinal,
                IsNullable: nullable,
                MaxLength: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                NumericPrecision: reader.IsDBNull(5) ? null : reader.GetInt32(5),
                NumericScale: reader.IsDBNull(6) ? null : reader.GetInt32(6),
                SampleValue: null,
                IsPrimaryKeyCandidate: fieldName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                                       fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase),
                IsTimestampCandidate: sourceType.Contains("timestamp", StringComparison.OrdinalIgnoreCase) ||
                                      sourceType.Equals("date", StringComparison.OrdinalIgnoreCase)));
        }

        return fields;
    }

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken)
    {
        var schemaName = SafeIdentifier(datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName ?? "public");
        var tableName = SafeIdentifier(datasetDefinition.SourceObjectName);
        var limit = Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, 1000);
        var sql = $"SELECT * FROM \"{schemaName}\".\"{tableName}\" LIMIT @limit;";

        return await ExecuteReadAsync(connectionProfile, sql, command =>
        {
            command.Parameters.AddWithValue("limit", limit);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceIncrementalReadRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CursorFieldName))
        {
            throw new InvalidOperationException("CursorField is required for PostgreSQL incremental reads.");
        }

        var schemaName = SafeIdentifier(datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName ?? "public");
        var tableName = SafeIdentifier(datasetDefinition.SourceObjectName);
        var cursorField = SafeIdentifier(request.CursorFieldName);
        var limit = Math.Clamp(request.Limit <= 0 ? 1000 : request.Limit, 1, 5000);
        var sql =
            $"SELECT * FROM \"{schemaName}\".\"{tableName}\" " +
            $"WHERE \"{cursorField}\" > @lastCursor " +
            $"ORDER BY \"{cursorField}\" ASC " +
            "LIMIT @limit;";

        return await ExecuteReadAsync(connectionProfile, sql, command =>
        {
            command.Parameters.AddWithValue("lastCursor", request.LastCursorValue ?? "");
            command.Parameters.AddWithValue("limit", limit);
        }, cancellationToken);
    }

    public string? GetLastError() => _lastError;

    private static async Task<IReadOnlyList<DataSourceRow>> ExecuteReadAsync(
        ConnectionProfile profile,
        string sql,
        Action<NpgsqlCommand> configure,
        CancellationToken cancellationToken)
    {
        var rows = new List<DataSourceRow>();

        await using var connection = new NpgsqlConnection(BuildConnectionString(profile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        configure(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        long rowNumber = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            rowNumber++;

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                values[reader.GetName(i)] = reader.IsDBNull(i)
                    ? null
                    : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(new DataSourceRow(rowNumber, values));
        }

        return rows;
    }

    private static string BuildConnectionString(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.HostName))
            throw new InvalidOperationException("PostgreSQL HostName is required.");

        if (string.IsNullOrWhiteSpace(profile.DatabaseName))
            throw new InvalidOperationException("PostgreSQL DatabaseName is required.");

        var username = Environment.GetEnvironmentVariable(profile.SecretReference ?? "")
            ?? profile.SecretReference;

        var password =
            Environment.GetEnvironmentVariable($"{profile.SecretReference}_PASSWORD") ??
            Environment.GetEnvironmentVariable("PLANTPROCESS_POSTGRES_PASSWORD") ??
            "";

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostName,
            Port = profile.Port ?? 5432,
            Database = profile.DatabaseName,
            Username = username,
            Password = password,
            ApplicationName = "PlantProcessIQ_ReadOnly",
            Timeout = 15,
            CommandTimeout = 30,
            IncludeErrorDetail = false
        };

        return builder.ConnectionString;
    }

    private static DataSourceConnectionTestResult Success(
        string message,
        IReadOnlyDictionary<string, string?> metadata)
    {
        return new DataSourceConnectionTestResult(true, message, DateTime.UtcNow, metadata);
    }

    private static DataSourceConnectionTestResult Failure(string message)
    {
        return new DataSourceConnectionTestResult(false, message, DateTime.UtcNow, new Dictionary<string, string?>());
    }

    private static string MapPostgresType(string sourceType)
    {
        return sourceType.ToLowerInvariant() switch
        {
            "integer" or "bigint" or "smallint" => "integer",
            "numeric" or "decimal" or "real" or "double precision" => "decimal",
            "timestamp without time zone" or "timestamp with time zone" or "date" => "datetime",
            "boolean" => "boolean",
            _ => "string"
        };
    }

    private static string NormalizeCode(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray();

        return new string(chars).Replace("__", "_").Trim('_');
    }

    private static string SafeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Identifier is required.");

        var clean = value.Trim();

        if (clean.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            throw new InvalidOperationException($"Unsafe PostgreSQL identifier '{value}'.");

        return clean;
    }
}