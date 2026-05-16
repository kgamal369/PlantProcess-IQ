using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Represents a configured source connection used by PlantProcess IQ.
/// 
/// IMPORTANT:
/// This entity stores connection metadata only.
/// It must not store raw passwords or secrets.
/// Secret values are referenced through SecretReference and will later be resolved
/// by a vault/provider layer.
/// 
/// Phase 3 supported MVP provider:
/// - CSV
/// 
/// Planned providers:
/// - Excel
/// - PostgreSQL
/// - SqlServer
/// - Oracle
/// - RestApi
/// </summary>
public class ConnectionProfile : BaseEntity
{
    public Guid SourceSystemDefinitionId { get; private set; }

    public string ConnectionProfileCode { get; private set; } = null!;

    public string ConnectionProfileName { get; private set; } = null!;

    public string ProviderType { get; private set; } = null!;
    // Csv, Excel, PostgreSql, SqlServer, Oracle, RestApi

    public string ConnectionMode { get; private set; } = "Snapshot";
    // Snapshot, Incremental, StreamingLater

    public string? HostName { get; private set; }

    public int? Port { get; private set; }

    public string? DatabaseName { get; private set; }

    public string? SchemaName { get; private set; }

    public string? FileRootPath { get; private set; }

    public string? ApiBaseUrl { get; private set; }

    public string? SecretReference { get; private set; }

    public string ConnectionOptionsJson { get; private set; } = "{}";

    public string ImportScheduleExpression { get; private set; } = "Every 15 minutes";

    public int ImportIntervalMinutes { get; private set; } = 15;

    public bool IsActive { get; private set; } = true;

    public bool ReadOnlyEnforced { get; private set; } = true;

    public string? Description { get; private set; }

    public DateTime? LastTestedAtUtc { get; private set; }

    public string? LastTestStatus { get; private set; }

    public string? LastTestMessage { get; private set; }

    private ConnectionProfile()
    {
    }

    public ConnectionProfile(
        Guid sourceSystemDefinitionId,
        string connectionProfileCode,
        string connectionProfileName,
        string providerType,
        bool isSynthetic,
        string connectionMode = "Snapshot",
        string? hostName = null,
        int? port = null,
        string? databaseName = null,
        string? schemaName = null,
        string? fileRootPath = null,
        string? apiBaseUrl = null,
        string? secretReference = null,
        string? connectionOptionsJson = null,
        bool readOnlyEnforced = true,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (sourceSystemDefinitionId == Guid.Empty)
            throw new ArgumentException("Source system definition ID is required.", nameof(sourceSystemDefinitionId));

        if (string.IsNullOrWhiteSpace(connectionProfileCode))
            throw new ArgumentException("Connection profile code is required.", nameof(connectionProfileCode));

        if (string.IsNullOrWhiteSpace(connectionProfileName))
            throw new ArgumentException("Connection profile name is required.", nameof(connectionProfileName));

        if (string.IsNullOrWhiteSpace(providerType))
            throw new ArgumentException("Provider type is required.", nameof(providerType));

        SourceSystemDefinitionId = sourceSystemDefinitionId;
        ConnectionProfileCode = connectionProfileCode.Trim();
        ConnectionProfileName = connectionProfileName.Trim();
        ProviderType = NormalizeProviderType(providerType);
        ConnectionMode = string.IsNullOrWhiteSpace(connectionMode)
            ? "Snapshot"
            : connectionMode.Trim();

        HostName = Clean(hostName);
        Port = port;
        DatabaseName = Clean(databaseName);
        SchemaName = Clean(schemaName);
        FileRootPath = Clean(fileRootPath);
        ApiBaseUrl = Clean(apiBaseUrl);
        SecretReference = Clean(secretReference);
        ConnectionOptionsJson = string.IsNullOrWhiteSpace(connectionOptionsJson)
            ? "{}"
            : connectionOptionsJson.Trim();

        ReadOnlyEnforced = readOnlyEnforced;
        Description = Clean(description);
        IsActive = true;

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void Update(
        string connectionProfileName,
        string connectionMode,
        string? hostName,
        int? port,
        string? databaseName,
        string? schemaName,
        string? fileRootPath,
        string? apiBaseUrl,
        string? secretReference,
        string? connectionOptionsJson,
        bool readOnlyEnforced,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(connectionProfileName))
            throw new ArgumentException("Connection profile name is required.", nameof(connectionProfileName));

        ConnectionProfileName = connectionProfileName.Trim();
        ConnectionMode = string.IsNullOrWhiteSpace(connectionMode)
            ? "Snapshot"
            : connectionMode.Trim();

        HostName = Clean(hostName);
        Port = port;
        DatabaseName = Clean(databaseName);
        SchemaName = Clean(schemaName);
        FileRootPath = Clean(fileRootPath);
        ApiBaseUrl = Clean(apiBaseUrl);
        SecretReference = Clean(secretReference);
        ConnectionOptionsJson = string.IsNullOrWhiteSpace(connectionOptionsJson)
            ? "{}"
            : connectionOptionsJson.Trim();

        ReadOnlyEnforced = readOnlyEnforced;
        Description = Clean(description);

        MarkAsUpdated();
    }

    public void UpdateImportSchedule(string scheduleExpression, int importIntervalMinutes)
    {
        if (string.IsNullOrWhiteSpace(scheduleExpression))
            throw new ArgumentException("Schedule expression is required.", nameof(scheduleExpression));

        if (importIntervalMinutes < 2)
            throw new ArgumentOutOfRangeException(nameof(importIntervalMinutes), "Import interval must be at least 2 minutes.");

        if (importIntervalMinutes > 7 * 24 * 60)
            throw new ArgumentOutOfRangeException(nameof(importIntervalMinutes), "Import interval cannot exceed 7 days.");

        ImportScheduleExpression = scheduleExpression.Trim();
        ImportIntervalMinutes = importIntervalMinutes;

        MarkAsUpdated();
    }

    public void MarkTestResult(bool isSuccess, string message)
    {
        LastTestedAtUtc = DateTime.UtcNow;
        LastTestStatus = isSuccess ? "Success" : "Failed";
        LastTestMessage = string.IsNullOrWhiteSpace(message)
            ? (isSuccess ? "Connection test succeeded." : "Connection test failed.")
            : message.Trim();

        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    private static string NormalizeProviderType(string value)
    {
        var normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "csv" => "Csv",
            "excel" => "Excel",
            "xlsx" => "Excel",
            "postgres" => "PostgreSql",
            "postgresql" => "PostgreSql",
            "sqlserver" => "SqlServer",
            "mssql" => "SqlServer",
            "oracle" => "Oracle",
            "api" => "RestApi",
            "restapi" => "RestApi",
            _ => normalized
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}