using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_TOP_LEVEL_CONTRACTS

public sealed record ConnectorTruthMatrixResponse(
    DateTime GeneratedAtUtc,
    string OperatingRule,
    IReadOnlyList<ConnectorProviderTruthRow> Providers);

public sealed record ConnectorProviderTruthRow(
    int SortOrder,
    string ProviderType,
    string DisplayName,
    string Description,
    bool IsImplemented,
    bool IsDemoCertified,
    bool IsAvailableNow,
    bool RequiresSecretReference,
    bool SupportsConnectionTest,
    bool SupportsSchemaDiscovery,
    bool SupportsSnapshotImport,
    bool SupportsIncrementalImport,
    string StatusLabel,
    string Limitation,
    int ActiveConnectionProfiles,
    int TotalConnectionProfiles,
    int ActiveSourceDatasets,
    int TotalSourceDatasets);

public sealed record ConnectorCertificationResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<ConnectorCertificationRow> Providers);

public sealed record ConnectorCertificationRow(
    string ProviderType,
    string EnvironmentVariableName,
    bool HasCertificationConnectionString,
    string CertificationStatus,
    bool IsDemoCertified,
    string Message);

public sealed record SourceScheduleBoardResponse(
    DateTime GeneratedAtUtc,
    int TotalDatasets,
    int DueNowDatasets,
    IReadOnlyList<SourceScheduleRow> Rows);

public sealed record SourceScheduleRow(
    Guid SourceDatasetDefinitionId,
    Guid ConnectionProfileId,
    string ConnectionProfileCode,
    string ConnectionProfileName,
    string ProviderType,
    Guid SourceSystemDefinitionId,
    string SourceSystemCode,
    string SourceSystemName,
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string? SourceSchemaName,
    string SourceObjectName,
    string? PrimaryTimestampField,
    string? IncrementalCursorField,
    string? LastCursorValue,
    int RefreshIntervalSeconds,
    DateTime? NextRunAtUtc,
    bool IsDatasetActive,
    bool IsConnectionActive,
    bool IsDueNow,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record RunDueSourceImportsRequest(
    int? MaxDatasetsPerRun,
    int? MaxRowsPerDataset);

public sealed record RunDueSourceImportsResponse(
    DateTime CompletedAtUtc,
    int MaxDatasetsPerRun,
    int MaxRowsPerDataset,
    long DurationMs,
    int DatasetsProcessed,
    int TotalRowsImported,
    int DatasetsFailedCount,
    IReadOnlyList<RunDueSourceDatasetResult> DatasetResults);

public sealed record RunDueSourceDatasetResult(
    string DatasetId,
    string DatasetCode,
    int RowsImported,
    string? ErrorMessage);

public sealed record UpdateDatasetCursorRequest(string? LastCursorValue);

public sealed record StagingSummaryResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<StagingSummaryRow> Rows);

public sealed record StagingSummaryRow(
    Guid ImportBatchId,
    Guid SourceSystemDefinitionId,
    string ImportBatchCode,
    string ImportType,
    string Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? SourceObjectName,
    string? FileName,
    int? RowCount,
    string? ErrorMessage,
    int StagingRecordCount,
    int PendingCount,
    int MappedCount,
    int FailedCount,
    int SkippedCount);

public sealed record StagingRecordsResponse(
    DateTime GeneratedAtUtc,
    int Count,
    IReadOnlyList<StagingRecordRow> Rows);

public sealed record StagingRecordRow(
    Guid Id,
    Guid ImportBatchId,
    string SourceObjectName,
    int RowNumber,
    string RawJson,
    bool IsProcessed,
    DateTime? ProcessedAtUtc,
    string ProcessingStatus,
    string? ProcessingError,
    Guid? CanonicalEntityId,
    string? CanonicalEntityName,
    string? SourceSystem,
    string? SourceRecordId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SchemaMappingWorkbenchResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<WorkbenchDatasetRow> Datasets,
    IReadOnlyList<WorkbenchSourceFieldRow> SourceFields,
    IReadOnlyList<CanonicalTargetRow> CanonicalTargets,
    IReadOnlyList<WorkbenchMappingRow> Mappings,
    IReadOnlyList<WorkbenchSchemaViewRow> SchemaViews);

public sealed record WorkbenchDatasetRow(
    Guid Id,
    string DatasetCode,
    string DatasetName,
    string DatasetKind,
    string ProviderType,
    string? SourceSchemaName,
    string SourceObjectName,
    bool IsActive);

public sealed record WorkbenchSourceFieldRow(
    Guid Id,
    Guid SourceDatasetDefinitionId,
    string FieldName,
    string DisplayName,
    string SourceDataType,
    int Ordinal,
    bool IsNullable,
    string? SampleValue,
    bool IsPrimaryKeyCandidate,
    bool IsTimestampCandidate,
    bool IsActive);

public sealed record CanonicalTargetRow(
    string EntityName,
    string FieldName,
    string DataType,
    bool IsRequired,
    string Description);

public sealed record WorkbenchMappingRow(
    Guid Id,
    string MappingCode,
    string MappingName,
    string SourceObjectName,
    string TargetEntityName,
    string MappingJson,
    string MappingVersion,
    bool IsActive,
    string? Description);

public sealed record WorkbenchSchemaViewRow(
    Guid Id,
    string SchemaViewCode,
    string SchemaViewName,
    string ViewKind,
    Guid? PrimarySourceDatasetDefinitionId,
    string SourceDatasetIdsJson,
    bool IsApproved,
    bool IsActive,
    string? LastValidationStatus,
    string? LastValidationMessage);

public sealed record PreviewSchemaViewRequest(
    string SqlText,
    int? MaxRows,
    int? TimeoutSeconds);

public sealed record SchemaViewPreviewResponse(
    bool IsSuccess,
    string Message,
    int RowCount,
    long DurationMs,
    IReadOnlyList<SchemaViewPreviewColumn> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed record SchemaViewPreviewColumn(
    string ColumnName,
    string DataType,
    int Ordinal);

public sealed record ImportJobConfigurationBoardResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<MappingImportJobCandidateRow> MappingCandidates,
    IReadOnlyList<ImportJobConfigurationRow> ExistingImportJobs);

public sealed record MappingImportJobCandidateRow(
    Guid MappingDefinitionId,
    string MappingCode,
    string MappingName,
    string SourceObjectName,
    string TargetEntityName,
    bool IsMappingActive,
    Guid? ExistingJobDefinitionId,
    string? ExistingJobCode,
    bool HasEnabledJob,
    string? ExistingScheduleExpression,
    string? LastRunStatus,
    DateTime? NextRunAtUtc);
