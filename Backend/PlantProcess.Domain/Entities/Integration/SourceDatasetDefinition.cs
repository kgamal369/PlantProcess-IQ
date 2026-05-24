using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Represents one source object inside a connection profile.
/// 
/// Examples:
/// - CSV file name
/// - Excel sheet
/// - SQL table
/// - SQL view
/// - API endpoint
/// 
/// This is the bridge between DB Link Configuration and Schema Configuration.
/// </summary>
public class SourceDatasetDefinition : BaseEntity
{
    public Guid ConnectionProfileId { get; private set; }

    public string DatasetCode { get; private set; } = null!;

    public string DatasetName { get; private set; } = null!;

    public string DatasetKind { get; private set; } = null!;
    // CsvFile, ExcelSheet, SqlTable, SqlView, ApiEndpoint

    public DateTime? NextRunAtUtc { get; private set; }

    public string SourceObjectName { get; private set; } = null!;

    public string? SourceSchemaName { get; private set; }

    public string? PrimaryTimestampField { get; private set; }

    public string? IncrementalCursorField { get; private set; }

    public string? LastCursorValue { get; private set; }

    public int RefreshIntervalSeconds { get; private set; } = 300;

    public string DatasetOptionsJson { get; private set; } = "{}";

    public bool IsActive { get; private set; } = true;

    public string? Description { get; private set; }

    /// Advance the next-run timestamp after a successful import.
    /// Uses RefreshIntervalSeconds as configured.
    public void ScheduleNextRunAfterSuccess()
    {
        NextRunAtUtc = DateTime.UtcNow.AddSeconds(RefreshIntervalSeconds);
        MarkAsUpdated();
    }
    
    /// Advance the next-run timestamp after a failed import.
    /// Uses a 2x back-off so a broken connection does not hammer the
    /// source every tick.
    public void ScheduleNextRunAfterFailure()
    {
        // 2x back-off, capped at 24 hours to prevent indefinite postponement
        var backoffSeconds = Math.Min(RefreshIntervalSeconds * 2, 86_400);
        NextRunAtUtc = DateTime.UtcNow.AddSeconds(backoffSeconds);
        MarkAsUpdated();
    }

    /// Force the next run to happen on the next tick. Used when the
    /// user changes the configuration or manually triggers a refresh.
    public void ScheduleNextRunImmediately()
    {
        NextRunAtUtc = null;
        MarkAsUpdated();
    }


    private SourceDatasetDefinition()
    {
    }

    public SourceDatasetDefinition(
        Guid connectionProfileId,
        string datasetCode,
        string datasetName,
        string datasetKind,
        string sourceObjectName,
        bool isSynthetic,
        string? sourceSchemaName = null,
        string? primaryTimestampField = null,
        string? incrementalCursorField = null,
        int refreshIntervalSeconds = 300,
        string? datasetOptionsJson = null,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (connectionProfileId == Guid.Empty)
            throw new ArgumentException("Connection profile ID is required.", nameof(connectionProfileId));

        if (string.IsNullOrWhiteSpace(datasetCode))
            throw new ArgumentException("Dataset code is required.", nameof(datasetCode));

        if (string.IsNullOrWhiteSpace(datasetName))
            throw new ArgumentException("Dataset name is required.", nameof(datasetName));

        if (string.IsNullOrWhiteSpace(datasetKind))
            throw new ArgumentException("Dataset kind is required.", nameof(datasetKind));

        if (string.IsNullOrWhiteSpace(sourceObjectName))
            throw new ArgumentException("Source object name is required.", nameof(sourceObjectName));

        if (refreshIntervalSeconds < 30)
            throw new ArgumentOutOfRangeException(nameof(refreshIntervalSeconds), "Refresh interval must be at least 30 seconds.");

        ConnectionProfileId = connectionProfileId;
        DatasetCode = datasetCode.Trim();
        DatasetName = datasetName.Trim();
        DatasetKind = NormalizeDatasetKind(datasetKind);
        SourceObjectName = sourceObjectName.Trim();
        SourceSchemaName = Clean(sourceSchemaName);
        PrimaryTimestampField = Clean(primaryTimestampField);
        IncrementalCursorField = Clean(incrementalCursorField);
        RefreshIntervalSeconds = refreshIntervalSeconds;
        DatasetOptionsJson = string.IsNullOrWhiteSpace(datasetOptionsJson)
            ? "{}"
            : datasetOptionsJson.Trim();
        Description = Clean(description);

        IsActive = true;
        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void Update(
        string datasetName,
        string sourceObjectName,
        string? sourceSchemaName,
        string? primaryTimestampField,
        string? incrementalCursorField,
        int refreshIntervalSeconds,
        string? datasetOptionsJson,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(datasetName))
            throw new ArgumentException("Dataset name is required.", nameof(datasetName));

        if (string.IsNullOrWhiteSpace(sourceObjectName))
            throw new ArgumentException("Source object name is required.", nameof(sourceObjectName));

        if (refreshIntervalSeconds < 30)
            throw new ArgumentOutOfRangeException(nameof(refreshIntervalSeconds), "Refresh interval must be at least 30 seconds.");

        DatasetName = datasetName.Trim();
        SourceObjectName = sourceObjectName.Trim();
        SourceSchemaName = Clean(sourceSchemaName);
        PrimaryTimestampField = Clean(primaryTimestampField);
        IncrementalCursorField = Clean(incrementalCursorField);
        RefreshIntervalSeconds = refreshIntervalSeconds;
        DatasetOptionsJson = string.IsNullOrWhiteSpace(datasetOptionsJson)
            ? "{}"
            : datasetOptionsJson.Trim();
        Description = Clean(description);

        MarkAsUpdated();
    }

    public void UpdateLastCursorValue(string? value)
    {
        LastCursorValue = Clean(value);
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

    private static string NormalizeDatasetKind(string value)
    {
        var normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "csv" => "CsvFile",
            "csvfile" => "CsvFile",
            "excel" => "ExcelSheet",
            "excelsheet" => "ExcelSheet",
            "sqltable" => "SqlTable",
            "table" => "SqlTable",
            "sqlview" => "SqlView",
            "view" => "SqlView",
            "api" => "ApiEndpoint",
            "endpoint" => "ApiEndpoint",
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