using System.Data;
using System.Globalization;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Connectors.Oracle;

/// <summary>
/// PPIQ-WF-002
/// Oracle read-only source connector.
///
/// Scope:
/// - Connection test
/// - Schema discovery
/// - Field discovery
/// - Snapshot reads
/// - Incremental cursor reads
///
/// Security:
/// - No raw password stored in ConnectionProfile.
/// - Uses SecretReference convention:
///     SecretReference = env var containing Oracle username
///     {SecretReference}_PASSWORD = env var containing password
/// - Optional direct connection string env var:
///     SecretReference = env var containing full Oracle connection string
///
/// Product guard:
/// - Generic Oracle connector for manufacturing source systems.
/// - Not steel-specific.
/// - Does not write to Oracle.
/// </summary>
public sealed class OracleConnector :
    IDataSourceConnector,
    ISchemaReader,
    IDataSourceReader
{
    private string? _lastError;

    public string ProviderType => "Oracle";

    public async Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new OracleConnection(BuildConnectionString(connectionProfile));
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AS CURRENT_SCHEMA,
                    SYS_CONTEXT('USERENV', 'DB_NAME') AS DB_NAME
                FROM DUAL
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            string? currentSchema = null;
            string? dbName = null;

            if (await reader.ReadAsync(cancellationToken))
            {
                currentSchema = reader["CURRENT_SCHEMA"]?.ToString();
                dbName = reader["DB_NAME"]?.ToString();
            }

            return new DataSourceConnectionTestResult(
                IsSuccess: true,
                Message: "Oracle read-only connection succeeded.",
                TestedAtUtc: DateTime.UtcNow,
                Metadata: new Dictionary<string, string?>
                {
                    ["provider"] = ProviderType,
                    ["host"] = connectionProfile.HostName,
                    ["port"] = (connectionProfile.Port ?? 1521).ToString(CultureInfo.InvariantCulture),
                    ["databaseName"] = connectionProfile.DatabaseName,
                    ["schemaName"] = connectionProfile.SchemaName,
                    ["currentSchema"] = currentSchema,
                    ["dbName"] = dbName
                });
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;

            return new DataSourceConnectionTestResult(
                IsSuccess: false,
                Message: $"Oracle connection failed: {ex.Message}",
                TestedAtUtc: DateTime.UtcNow,
                Metadata: new Dictionary<string, string?>
                {
                    ["provider"] = ProviderType,
                    ["host"] = connectionProfile.HostName,
                    ["port"] = (connectionProfile.Port ?? 1521).ToString(CultureInfo.InvariantCulture),
                    ["databaseName"] = connectionProfile.DatabaseName,
                    ["schemaName"] = connectionProfile.SchemaName
                });
        }
    }

    public async Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        var schema = NormalizeOracleName(connectionProfile.SchemaName);

        await using var connection = new OracleConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                OWNER,
                OBJECT_NAME,
                OBJECT_TYPE
            FROM ALL_OBJECTS
            WHERE OBJECT_TYPE IN ('TABLE', 'VIEW')
              AND OWNER = :owner
              AND OBJECT_NAME NOT LIKE 'BIN$%'
            ORDER BY OWNER, OBJECT_TYPE, OBJECT_NAME
            FETCH FIRST 500 ROWS ONLY
            """;

        command.Parameters.Add(new OracleParameter("owner", OracleDbType.Varchar2)
        {
            Value = schema
        });

        var result = new List<DiscoveredSourceDataset>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var owner = reader["OWNER"]?.ToString() ?? schema;
            var objectName = reader["OBJECT_NAME"]?.ToString() ?? "";
            var objectType = reader["OBJECT_TYPE"]?.ToString() ?? "TABLE";

            if (string.IsNullOrWhiteSpace(objectName))
                continue;

            result.Add(new DiscoveredSourceDataset(
                DatasetCode: $"{owner}_{objectName}".ToUpperInvariant(),
                DatasetName: objectName,
                DatasetKind: objectType.Equals("VIEW", StringComparison.OrdinalIgnoreCase)
                    ? "View"
                    : "Table",
                SourceObjectName: objectName,
                SourceSchemaName: owner,
                DatasetOptionsJson: JsonSerializer.Serialize(new
                {
                    provider = ProviderType,
                    owner,
                    objectType
                })));
        }

        return result;
    }

    public async Task<IReadOnlyList<DiscoveredSourceField>> DiscoverFieldsForDatasetAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        CancellationToken cancellationToken)
    {
        var schema = NormalizeOracleName(datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName);
        var table = NormalizeOracleName(datasetDefinition.SourceObjectName);

        await using var connection = new OracleConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                COLUMN_NAME,
                DATA_TYPE,
                COLUMN_ID,
                NULLABLE,
                DATA_LENGTH,
                DATA_PRECISION,
                DATA_SCALE
            FROM ALL_TAB_COLUMNS
            WHERE OWNER = :owner
              AND TABLE_NAME = :tableName
            ORDER BY COLUMN_ID
            """;

        command.Parameters.Add(new OracleParameter("owner", OracleDbType.Varchar2)
        {
            Value = schema
        });

        command.Parameters.Add(new OracleParameter("tableName", OracleDbType.Varchar2)
        {
            Value = table
        });

        var fields = new List<DiscoveredSourceField>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader["COLUMN_NAME"]?.ToString() ?? "";
            var dataType = reader["DATA_TYPE"]?.ToString() ?? "UNKNOWN";
            var ordinal = ToInt(reader["COLUMN_ID"]) ?? fields.Count + 1;

            fields.Add(new DiscoveredSourceField(
                FieldName: columnName,
                DisplayName: columnName,
                SourceDataType: dataType,
                Ordinal: ordinal,
                IsNullable: string.Equals(reader["NULLABLE"]?.ToString(), "Y", StringComparison.OrdinalIgnoreCase),
                MaxLength: ToInt(reader["DATA_LENGTH"]),
                NumericPrecision: ToInt(reader["DATA_PRECISION"]),
                NumericScale: ToInt(reader["DATA_SCALE"]),
                SampleValue: null,
                IsPrimaryKeyCandidate: IsPrimaryKeyCandidate(columnName),
                IsTimestampCandidate: IsTimestampCandidate(columnName, dataType)));
        }

        return fields;
    }

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken)
    {
        var limit = ClampLimit(request.Limit);
        var schema = NormalizeOracleName(request.SourceSchemaName ?? datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName);
        var table = NormalizeOracleName(request.SourceObjectName);

        var sql =
            $"""
             SELECT *
             FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(table)}
             FETCH FIRST {limit} ROWS ONLY
             """;

        return await ExecuteRowsAsync(connectionProfile, sql, configure: null, cancellationToken);
    }

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceIncrementalReadRequest request,
        CancellationToken cancellationToken)
    {
        var limit = ClampLimit(request.Limit);
        var schema = NormalizeOracleName(request.SourceSchemaName ?? datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName);
        var table = NormalizeOracleName(request.SourceObjectName);
        var cursorField = NormalizeOracleName(request.CursorFieldName);

        var sql =
            string.IsNullOrWhiteSpace(request.LastCursorValue)
                ? $"""
                   SELECT *
                   FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(table)}
                   ORDER BY {QuoteIdentifier(cursorField)}
                   FETCH FIRST {limit} ROWS ONLY
                   """
                : $"""
                   SELECT *
                   FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(table)}
                   WHERE {QuoteIdentifier(cursorField)} > :lastCursor
                   ORDER BY {QuoteIdentifier(cursorField)}
                   FETCH FIRST {limit} ROWS ONLY
                   """;

        return await ExecuteRowsAsync(
            connectionProfile,
            sql,
            configure: command =>
            {
                if (!string.IsNullOrWhiteSpace(request.LastCursorValue))
                {
                    command.Parameters.Add(new OracleParameter("lastCursor", OracleDbType.Varchar2)
                    {
                        Value = request.LastCursorValue
                    });
                }
            },
            cancellationToken);
    }

    public string? GetLastError() => _lastError;

    private static async Task<IReadOnlyList<DataSourceRow>> ExecuteRowsAsync(
        ConnectionProfile profile,
        string sql,
        Action<OracleCommand>? configure,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(profile));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.BindByName = true;

        configure?.Invoke(command);

        var rows = new List<DataSourceRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        long rowNumber = 1;

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i)
                    ? null
                    : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);

                values[reader.GetName(i)] = value;
            }

            rows.Add(new DataSourceRow(rowNumber++, values));
        }

        return rows;
    }

    private static string BuildConnectionString(ConnectionProfile profile)
    {
        var options = ParseOptions(profile.ConnectionOptionsJson);

        if (!string.IsNullOrWhiteSpace(profile.SecretReference))
        {
            var direct = Environment.GetEnvironmentVariable(profile.SecretReference);

            if (!string.IsNullOrWhiteSpace(direct) &&
                direct.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return direct;
            }
        }

        var host = Require(profile.HostName, "Oracle HostName");
        var port = profile.Port ?? 1521;

        var serviceName =
            GetString(options, "serviceName") ??
            GetString(options, "ServiceName") ??
            profile.DatabaseName;

        var sid =
            GetString(options, "sid") ??
            GetString(options, "SID");

        if (string.IsNullOrWhiteSpace(serviceName) && string.IsNullOrWhiteSpace(sid))
            throw new InvalidOperationException("Oracle connection requires DatabaseName/serviceName or sid in ConnectionOptionsJson.");

        var username = ResolveSecretValue(profile.SecretReference, "Oracle username");
        var password = ResolveSecretValue($"{profile.SecretReference}_PASSWORD", "Oracle password");

        var descriptor =
            !string.IsNullOrWhiteSpace(serviceName)
                ? $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={serviceName})))"
                : $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SID={sid})))";

        var builder = new OracleConnectionStringBuilder
        {
            DataSource = descriptor,
            UserID = username,
            Password = password,
            Pooling = true,
            ValidateConnection = true
        };

        return builder.ConnectionString;
    }

    private static Dictionary<string, object?> ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string? GetString(Dictionary<string, object?> options, string key)
    {
        if (!options.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString(),
            JsonElement e => e.ToString(),
            _ => value.ToString()
        };
    }

    private static string ResolveSecretValue(string? envVarName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(envVarName))
            throw new InvalidOperationException($"{displayName} environment variable name is missing.");

        var value = Environment.GetEnvironmentVariable(envVarName);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{displayName} environment variable '{envVarName}' is not set.");

        return value;
    }

    private static string Require(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");

        return value.Trim();
    }

    private static string NormalizeOracleName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Oracle schema/object/column name is required.");

        return value.Trim().Trim('"').ToUpperInvariant();
    }

    private static string QuoteIdentifier(string value)
    {
        var normalized = NormalizeOracleName(value);

        if (normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '$' || ch == '#')))
            throw new InvalidOperationException($"Unsafe Oracle identifier: {value}");

        return $"\"{normalized}\"";
    }

    private static int ClampLimit(int value)
    {
        if (value < 1) return 100;
        if (value > 10_000) return 10_000;
        return value;
    }

    private static int? ToInt(object? value)
    {
        if (value is null || value == DBNull.Value)
            return null;

        if (int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static bool IsPrimaryKeyCandidate(string columnName)
    {
        return columnName.Equals("ID", StringComparison.OrdinalIgnoreCase) ||
               columnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase) ||
               columnName.EndsWith("_NO", StringComparison.OrdinalIgnoreCase) ||
               columnName.EndsWith("_CODE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTimestampCandidate(string columnName, string dataType)
    {
        return dataType.Contains("DATE", StringComparison.OrdinalIgnoreCase) ||
               dataType.Contains("TIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
               columnName.EndsWith("_AT", StringComparison.OrdinalIgnoreCase) ||
               columnName.EndsWith("_TIME", StringComparison.OrdinalIgnoreCase) ||
               columnName.EndsWith("_UTC", StringComparison.OrdinalIgnoreCase);
    }
}