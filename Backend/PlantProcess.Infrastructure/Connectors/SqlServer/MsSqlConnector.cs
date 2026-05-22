// ============================================================
// FILE: Backend/PlantProcess.Infrastructure/Connectors/SqlServer/MsSqlConnector.cs
//
// Full SQL Server / MSSQL connector — matches the PostgreSqlConnector
// interface contract exactly.
//
// REQUIRED NuGet package (add to PlantProcess.Infrastructure.csproj):
//   <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
// ============================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Connectors.SqlServer;

/// <summary>
/// SQL Server / MSSQL data source connector.
/// Supports connection test, table/view discovery, field discovery,
/// full-read and incremental (cursor-based) read.
/// All SQL identifiers are validated and bracket-quoted to prevent injection.
/// </summary>
public sealed class MsSqlConnector : IDataSourceConnector, ISchemaReader, IDataSourceReader
{
    private string? _lastError;

    public string ProviderType => "SqlServer";

    // ── Connection Test ───────────────────────────────────────────────────

    public async Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(connectionProfile));
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                "SELECT DB_NAME(), SYSTEM_USER, @@VERSION;",
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var metadata = new Dictionary<string, string?>();

            if (await reader.ReadAsync(cancellationToken))
            {
                metadata["database"] = reader.GetString(0);
                metadata["user"] = reader.GetString(1);
                metadata["version"] = reader.GetString(2)[..Math.Min(200, reader.GetString(2).Length)];
            }

            metadata["readOnlyEnforced"] = connectionProfile.ReadOnlyEnforced
                .ToString(CultureInfo.InvariantCulture);

            return Success("SQL Server connection succeeded.", metadata);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Failure($"SQL Server connection test failed: {ex.Message}");
        }
    }

    // ── Dataset Discovery ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        var schemaName = string.IsNullOrWhiteSpace(connectionProfile.SchemaName)
            ? "dbo"
            : connectionProfile.SchemaName.Trim();

        // Enumerate both BASE TABLE and VIEW from INFORMATION_SCHEMA
        const string sql = """
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME,
                TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_TYPE IN ('BASE TABLE', 'VIEW')
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """;

        var datasets = new List<DiscoveredSourceDataset>();

        await using var connection = new SqlConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schemaName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var kind = reader.GetString(2).Equals("VIEW", StringComparison.OrdinalIgnoreCase)
                ? "SqlServerView"
                : "SqlServerTable";

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
                    provider = "SqlServer"
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
            ? connectionProfile.SchemaName ?? "dbo"
            : datasetDefinition.SourceSchemaName;

        // INFORMATION_SCHEMA.COLUMNS with primary key hint via KEY_COLUMN_USAGE
        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.ORDINAL_POSITION,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                CASE
                    WHEN kcu.COLUMN_NAME IS NOT NULL THEN 1
                    ELSE 0
                END AS IS_PRIMARY_KEY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON  kcu.TABLE_SCHEMA     = c.TABLE_SCHEMA
                AND kcu.TABLE_NAME       = c.TABLE_NAME
                AND kcu.COLUMN_NAME      = c.COLUMN_NAME
                AND kcu.CONSTRAINT_NAME  LIKE 'PK%'
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME   = @table
            ORDER BY c.ORDINAL_POSITION;
            """;

        var fields = new List<DiscoveredSourceField>();

        await using var connection = new SqlConnection(BuildConnectionString(connectionProfile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@table", datasetDefinition.SourceObjectName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var fieldName = reader.GetString(0);
            var sourceType = reader.GetString(1);
            var ordinal = reader.GetInt32(2);
            var nullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var isPk = !reader.IsDBNull(7) && reader.GetInt32(7) == 1;

            fields.Add(new DiscoveredSourceField(
                FieldName: fieldName,
                DisplayName: fieldName,
                SourceDataType: MapSqlServerType(sourceType),
                Ordinal: ordinal,
                IsNullable: nullable,
                MaxLength: reader.IsDBNull(4) ? null : (int?)reader.GetValue(4),
                NumericPrecision: reader.IsDBNull(5) ? null : (int?)reader.GetValue(5),
                NumericScale: reader.IsDBNull(6) ? null : (int?)reader.GetValue(6),
                SampleValue: null,
                IsPrimaryKeyCandidate: isPk ||
                    fieldName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase),
                IsTimestampCandidate:
                    sourceType.Contains("datetime", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("date", StringComparison.OrdinalIgnoreCase) ||
                    sourceType.Equals("timestamp", StringComparison.OrdinalIgnoreCase)));
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
        var schemaName = BracketIdentifier(
            datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName ?? "dbo");
        var tableName = BracketIdentifier(datasetDefinition.SourceObjectName);
        var limit = Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, 1000);

        // SQL Server uses TOP N instead of LIMIT
        var sql = $"SELECT TOP (@limit) * FROM {schemaName}.{tableName};";

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
            throw new InvalidOperationException("CursorField is required for SQL Server incremental reads.");

        var schemaName = BracketIdentifier(
            datasetDefinition.SourceSchemaName ?? connectionProfile.SchemaName ?? "dbo");
        var tableName = BracketIdentifier(datasetDefinition.SourceObjectName);
        var cursorField = BracketIdentifier(request.CursorFieldName);
        var limit = Math.Clamp(request.Limit <= 0 ? 1000 : request.Limit, 1, 5000);

        // Use TOP + ORDER BY for cursor-based incremental reads on SQL Server
        var sql = $"""
            SELECT TOP (@limit) *
            FROM {schemaName}.{tableName}
            WHERE {cursorField} > @lastCursor
            ORDER BY {cursorField} ASC;
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
        Action<SqlCommand> configure,
        CancellationToken cancellationToken)
    {
        var rows = new List<DataSourceRow>();

        await using var connection = new SqlConnection(BuildConnectionString(profile));
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
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
            throw new InvalidOperationException("SQL Server HostName is required.");

        if (string.IsNullOrWhiteSpace(profile.DatabaseName))
            throw new InvalidOperationException("SQL Server DatabaseName is required.");

        // Resolve credentials: prefer env-var secrets over stored values
        var username = Environment.GetEnvironmentVariable(profile.SecretReference ?? "")
            ?? profile.SecretReference;

        var password =
            Environment.GetEnvironmentVariable($"{profile.SecretReference}_PASSWORD") ??
            Environment.GetEnvironmentVariable("PLANTPROCESS_SQLSERVER_PASSWORD") ??
            "";

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = profile.Port.HasValue
                ? $"{profile.HostName},{profile.Port.Value}"
                : profile.HostName,
            InitialCatalog = profile.DatabaseName,
            ApplicationName = "PlantProcessIQ_ReadOnly",
            ConnectTimeout = 15,
            CommandTimeout = 30,
            Encrypt = SqlConnectionEncryptOption.Optional,  // Optional for internal networks
            TrustServerCertificate = true                    // Allow self-signed certs in plant environments
        };

        // Prefer SQL auth if credentials are provided, otherwise use Windows auth
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            builder.UserID = username;
            builder.Password = password;
            builder.IntegratedSecurity = false;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Maps SQL Server system type names to normalised PlantProcess type tokens.
    /// </summary>
    private static string MapSqlServerType(string sourceType) =>
        sourceType.ToLowerInvariant() switch
        {
            "int" or "bigint" or "smallint" or "tinyint" => "integer",
            "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" => "decimal",
            "datetime" or "datetime2" or "smalldatetime" or "date" => "datetime",
            "datetimeoffset" => "datetime",
            "time" => "string",
            "bit" => "boolean",
            "uniqueidentifier" => "string",
            "binary" or "varbinary" or "image" => "binary",
            _ => "string"
        };

    /// <summary>
    /// Wraps an identifier in SQL Server square brackets, validating it contains
    /// no bracket characters to prevent bracket-escape injection.
    /// </summary>
    private static string BracketIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("SQL Server identifier is required.");

        var clean = value.Trim();

        if (clean.Contains('[') || clean.Contains(']'))
            throw new InvalidOperationException(
                $"Unsafe SQL Server identifier '{value}' — contains bracket characters.");

        // Allow letters, digits, underscores, spaces and dots (schema.table patterns)
        if (clean.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or ' ' or '.')))
            throw new InvalidOperationException(
                $"Unsafe SQL Server identifier '{value}'.");

        return $"[{clean}]";
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
