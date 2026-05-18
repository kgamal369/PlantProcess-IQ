namespace PlantProcess.Application.Integration.Contracts.Dtos;

/// <summary>
/// Connection test result returned by a provider-specific connector.
/// </summary>
public sealed record DataSourceConnectionTestResult(
    bool IsSuccess,
    string Message,
    DateTime TestedAtUtc,
    IReadOnlyDictionary<string, string?> Metadata);

/// <summary>
/// A discovered dataset from a source system.
///
/// Examples:
/// - CSV file
/// - Excel sheet
/// - SQL table
/// - SQL view
/// - API endpoint
/// </summary>
public sealed record DiscoveredSourceDataset(
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string SourceObjectName,
    string? SourceSchemaName,
    string DatasetOptionsJson);

/// <summary>
/// A discovered field/column from a dataset.
/// </summary>
public sealed record DiscoveredSourceField(
    string FieldName,
    string DisplayName,
    string SourceDataType,
    int Ordinal,
    bool IsNullable,
    int? MaxLength,
    int? NumericPrecision,
    int? NumericScale,
    string? SampleValue,
    bool IsPrimaryKeyCandidate,
    bool IsTimestampCandidate);

/// <summary>
/// Generic source-row representation returned by all connectors.
/// Values stay as raw strings at connector boundary.
/// Canonical conversion happens later in mapping/transform services.
/// </summary>
public sealed record DataSourceRow(
    long RowNumber,
    IReadOnlyDictionary<string, string?> Values);

/// <summary>
/// Request for reading rows from a source dataset.
/// </summary>
public sealed record DataSourceReadRequest(
    Guid ConnectionProfileId,
    Guid? SourceDatasetDefinitionId,
    string SourceObjectName,
    string? SourceSchemaName,
    int Limit,
    string? DatasetOptionsJson);

/// <summary>
/// Request for incremental reading based on a cursor/key.
/// </summary>
public sealed record DataSourceIncrementalReadRequest(
    Guid ConnectionProfileId,
    Guid? SourceDatasetDefinitionId,
    string SourceObjectName,
    string? SourceSchemaName,
    string CursorFieldName,
    string? LastCursorValue,
    int Limit,
    string? DatasetOptionsJson);



