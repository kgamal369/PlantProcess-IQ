// ============================================================
// FILE: Backend/PlantProcess.Infrastructure/Connectors/MySql/MySqlConnector.cs
//
// Full MySQL / MariaDB connector — matches the PostgreSqlConnector
// interface contract exactly.
//
// REQUIRED NuGet package (add to PlantProcess.Infrastructure.csproj):
//   <PackageReference Include="MySqlConnector" Version="2.3.7" />
//   (MySqlConnector — the async-first community driver, NOT Oracle's MySql.Data)
// ============================================================

using System.Globalization;
using System.Text.Json;
using MySqlConnector;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Connectors.MySql;

/// <summary>
/// MySQL / MariaDB data source connector.
/// Supports connection test, table/view discovery, field discovery,
/// full-read and incremental (cursor-based) read.
/// All SQL identifiers are backtick-quoted to prevent injection.
/// Compatible with MySQL 5.7+, MySQL 8.x, and MariaDB 10.x+.
/// </summary>
public sealed class MySqlDataConnector : IDataSourceConnector, ISchemaReader, IDataSourceReader
{
    private string? _lastError;

    public string ProviderType => "MySql";

    // ── Connection Test ───────────────────────────────────────────────────

    public async Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new MySqlConnection(BuildConnectionString(connectionProfile));
            await connection.OpenAsync(cancellationToken);

            await using var command = new MySqlCommand(
                "SELECT DATABASE(), USER(), VERSION();",
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var metadata = new Dictionary<string, string?>();

            if (await reader.ReadAsync(cancellationToken))
            {
                metadata["database"] = reader.IsDBNull(0) ? null : reader.GetString(0);
                metadata["user"] = reader.IsDBNull(1) ? null : reader.GetString(1);
                metadata["version"] = reader.IsDBNull(2) ? null : reader.GetString(2);
            }

            metadata["readOnlyEnforced"] = connectionProfile.ReadOnlyEnforced
                .ToString(CultureInfo.InvariantCulture);

            return Success("MySQL connection succeeded.", metadata);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Failure($"MySQL connection test failed: {ex.Message}");
        }
    }

    // ── Dataset Discovery ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        // In MySQL, the "schema" is the database name
        var databaseName = string.IsNullOrWhiteSpace(connectionProfile.SchemaName)
            ? connectionProfile.DatabaseName
            : connectionProfile.SchemaName.Trim();

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException(
                "MySQL DatabaseName or SchemaName is required for dataset discovery.");

        const string sql = """
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME,
                TABLE_TYPE
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_TYPE IN ('BASE TABLE', 'VIEW')
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """;

        var datasets = new List<DiscoveredSourceDataset>();

        await using var connection = new MySqlConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", databaseName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var kind = reader.GetString(2).Equals("VIEW", StringComparison.OrdinalIgnoreCase)
                ? "MySqlView"
                : "MySqlTable";

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
                    provider = "MySql"
                })));
        }

        return datasets;
    }

    // ── Field Discovery ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<DiscoveredSourceField>> DiscoverFieldsForDatasetAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        CancellationToken cancellationToken)
    {
        var schemaName = string.IsNullOrWhiteSpace(datasetDefinition.SourceSchemaName)
            ? connectionProfile.SchemaName ?? connectionProfile.DatabaseName ?? ""
            : datasetDefinition.SourceSchemaName;

        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.ORDINAL_POSITION,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.COLUMN_KEY
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME   = @table
            ORDER BY c.ORDINAL_POSITION;
            """;

        var fields = new List<DiscoveredSourceField>();

        await using var connection = new MySqlConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@table", datasetDefinition.SourceObjectName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var fieldName = reader.GetString(0);
            var sourceType = reader.GetString(1);
            var ordinal = reader.GetInt32(2);
            var nullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var columnKey = reader.IsDBNull(7) ? "" : reader.GetString(7);
            var isPk = columnKey.Equals("PRI", StringComparison.OrdinalIgnoreCase);

            fields.Add(new DiscoveredSourceField(
                FieldName: fieldName,
                DisplayName: fieldName,
                SourceDataType: MapMySqlType(sourceType),
                Ordinal: ordinal,
                IsNullable: nullable,
                MaxLength: reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                NumericPrecision: reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                NumericScale: reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6)),
                SampleValue: null,
                IsPrimaryKeyCandidate: isPk ||
                    fieldName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase),
                IsTimestampCandidate:
                    sourceType.Contains("datetime", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Contains("timestamp", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("date", StringComparison.OrdinalIgnoreCase)));
        }

        return fields;
    }

    // ── Full Read ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken)
    {
        var schemaName = BacktickIdentifier(
            datasetDefinition.SourceSchemaName ??
            connectionProfile.SchemaName ??
            connectionProfile.DatabaseName ?? "");
        var tableName = BacktickIdentifier(datasetDefinition.SourceObjectName);
        var limit = Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, 1000);

        var sql = $"SELECT * FROM {schemaName}.{tableName} LIMIT @limit;";

        return await ExecuteReadAsync(connectionProfile, sql, command =>
        {
            command.Parameters.AddWithValue("@limit", limit);
        }, cancellationToken);
    }

    // ── Incremental (cursor) Read ─────────────────────────────────────────

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceIncrementalReadRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CursorFieldName))
            throw new InvalidOperationException(
                "CursorField is required for MySQL incremental reads.");

        var schemaName = BacktickIdentifier(
            datasetDefinition.SourceSchemaName ??
            connectionProfile.SchemaName ??
            connectionProfile.DatabaseName ?? "");
        var tableName = BacktickIdentifier(datasetDefinition.SourceObjectName);
        var cursorField = BacktickIdentifier(request.CursorFieldName);
        var limit = Math.Clamp(request.Limit <= 0 ? 1000 : request.Limit, 1, 5000);

        var sql = $"""
            SELECT *
            FROM {schemaName}.{tableName}
            WHERE {cursorField} > @lastCursor
            ORDER BY {cursorField} ASC
            LIMIT @limit;
            """;

        return await ExecuteReadAsync(connectionProfile, sql, command =>
        {
            command.Parameters.AddWithValue("@lastCursor", request.LastCursorValue ?? "");
            command.Parameters.AddWithValue("@limit", limit);
        }, cancellationToken);
    }

    public string? GetLastError() => _lastError;

    // ── Private Helpers ───────────────────────────────────────────────────

    private static async Task<IReadOnlyList<DataSourceRow>> ExecuteReadAsync(
        ConnectionProfile profile,
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        var rows = new List<DataSourceRow>();

        await using var connection = new MySqlConnection(BuildConnectionString(profile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
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
            throw new InvalidOperationException("MySQL HostName is required.");

        if (string.IsNullOrWhiteSpace(profile.DatabaseName))
            throw new InvalidOperationException("MySQL DatabaseName is required.");

        // Resolve credentials from env-var secrets
        var username = Environment.GetEnvironmentVariable(profile.SecretReference ?? "")
            ?? profile.SecretReference
            ?? "root";

        var password =
            Environment.GetEnvironmentVariable($"{profile.SecretReference}_PASSWORD") ??
            Environment.GetEnvironmentVariable("PLANTPROCESS_MYSQL_PASSWORD") ??
            "";

        var builder = new MySqlConnectionStringBuilder
        {
            Server = profile.HostName,
            Port = (uint)(profile.Port ?? 3306),
            Database = profile.DatabaseName,
            UserID = username,
            Password = password,
            ApplicationName = "PlantProcessIQ_ReadOnly",
            ConnectionTimeout = 15,
            DefaultCommandTimeout = 30,
            AllowPublicKeyRetrieval = true,   // Required for MySQL 8 with default_authentication_plugin
            SslMode = MySqlSslMode.Preferred  // Prefer TLS but fall back for internal networks
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Maps MySQL column type names to normalised PlantProcess type tokens.
    /// </summary>
    private static string MapMySqlType(string sourceType) =>
        sourceType.ToLowerInvariant() switch
        {
            "int" or "integer" or "bigint" or "smallint" or "tinyint" or "mediumint" => "integer",
            "decimal" or "numeric" or "float" or "double" or "real" => "decimal",
            "datetime" or "timestamp" or "date" => "datetime",
            "time" or "year" => "string",
            "tinyint(1)" or "bool" or "boolean" => "boolean",
            "blob" or "mediumblob" or "longblob" or "tinyblob" or "binary" or "varbinary" => "binary",
            _ => "string"
        };

    /// <summary>
    /// Wraps a MySQL identifier in backticks, validating no backtick characters
    /// exist in the input to prevent backtick-escape injection.
    /// </summary>
    private static string BacktickIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("MySQL identifier is required.");

        var clean = value.Trim();

        if (clean.Contains('`'))
            throw new InvalidOperationException(
                $"Unsafe MySQL identifier '{value}' — contains backtick character.");

        if (clean.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or ' ' or '.')))
            throw new InvalidOperationException(
                $"Unsafe MySQL identifier '{value}'.");

        return $"`{clean}`";
    }

    private static string NormalizeCode(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray();

        return new string(chars).Replace("__", "_").Trim('_');
    }

    private static DataSourceConnectionTestResult Success(
        string message,
        IReadOnlyDictionary<string, string?> metadata)
        => new(true, message, DateTime.UtcNow, metadata);

    private static DataSourceConnectionTestResult Failure(string message)
        => new(false, message, DateTime.UtcNow, new Dictionary<string, string?>());
}
