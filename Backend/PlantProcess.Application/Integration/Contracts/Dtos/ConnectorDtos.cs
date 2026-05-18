namespace PlantProcess.Application.Integration.Contracts.Dtos;

public sealed record ProviderTypeDto(
    string ProviderType,
    string DisplayName,
    string Description,
    bool IsAvailableNow,
    bool RequiresSecretReference,
    bool SupportsSchemaDiscovery,
    bool SupportsSnapshotImport,
    bool SupportsIncrementalImport);

public sealed record ConnectionProfileDto(
    Guid Id,
    Guid SourceSystemDefinitionId,
    string SourceSystemCode,
    string SourceSystemName,
    string ConnectionProfileCode,
    string ConnectionProfileName,
    string ProviderType,
    string ConnectionMode,
    string? HostName,
    int? Port,
    string? DatabaseName,
    string? SchemaName,
    string? FileRootPath,
    string? ApiBaseUrl,
    string? SecretReference,
    string ConnectionOptionsJson,
    bool IsActive,
    bool ReadOnlyEnforced,
    string? Description,
    DateTime? LastTestedAtUtc,
    string? LastTestStatus,
    string? LastTestMessage,
    bool IsSynthetic,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateConnectionProfileRequest(
    Guid SourceSystemDefinitionId,
    string ConnectionProfileCode,
    string ConnectionProfileName,
    string ProviderType,
    string? ConnectionMode,
    string? HostName,
    int? Port,
    string? DatabaseName,
    string? SchemaName,
    string? FileRootPath,
    string? ApiBaseUrl,
    string? SecretReference,
    string? ConnectionOptionsJson,
    bool? ReadOnlyEnforced,
    string? Description,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record UpdateConnectionProfileRequest(
    string ConnectionProfileName,
    string? ConnectionMode,
    string? HostName,
    int? Port,
    string? DatabaseName,
    string? SchemaName,
    string? FileRootPath,
    string? ApiBaseUrl,
    string? SecretReference,
    string? ConnectionOptionsJson,
    bool? ReadOnlyEnforced,
    string? Description);

public sealed record SourceDatasetDefinitionDto(
    Guid Id,
    Guid ConnectionProfileId,
    string ConnectionProfileCode,
    string ProviderType,
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string SourceObjectName,
    string? SourceSchemaName,
    string? PrimaryTimestampField,
    string? IncrementalCursorField,
    string? LastCursorValue,
    int RefreshIntervalSeconds,
    string DatasetOptionsJson,
    bool IsActive,
    string? Description,
    bool IsSynthetic,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateSourceDatasetDefinitionRequest(
    Guid ConnectionProfileId,
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string SourceObjectName,
    string? SourceSchemaName,
    string? PrimaryTimestampField,
    string? IncrementalCursorField,
    int? RefreshIntervalSeconds,
    string? DatasetOptionsJson,
    string? Description,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record SourceFieldDefinitionDto(
    Guid Id,
    Guid SourceDatasetDefinitionId,
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
    bool IsTimestampCandidate,
    bool IsActive);

public sealed record CsvSchemaDiscoveryRequest(
    string CsvText,
    string? FileName,
    string? Delimiter,
    bool? HasHeader,
    int? MaxRowsToAnalyze,
    bool PersistFields);

public sealed record CsvPreviewRequest(
    string CsvText,
    string? Delimiter,
    bool? HasHeader,
    int? MaxRows);

public sealed record CsvImportSnapshotRequest(
    string CsvText,
    string? FileName,
    string? Delimiter,
    bool? HasHeader,
    string? ImportBatchCode,
    string? Checksum,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record CsvSchemaDiscoveryResult(
    Guid SourceDatasetDefinitionId,
    string DatasetCode,
    string SourceObjectName,
    string Delimiter,
    bool HasHeader,
    int AnalyzedRowCount,
    IReadOnlyList<SourceFieldDefinitionDto> Fields);

public sealed record CsvPreviewResult(
    string Delimiter,
    bool HasHeader,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

public sealed record CsvImportSnapshotResult(
    Guid ImportBatchId,
    string ImportBatchCode,
    Guid SourceDatasetDefinitionId,
    Guid ConnectionProfileId,
    Guid SourceSystemDefinitionId,
    string SourceObjectName,
    int RowCount,
    string Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);


