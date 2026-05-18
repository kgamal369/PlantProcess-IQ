namespace PlantProcess.Application.Integration.Contracts.Dtos;

public sealed record SchemaViewDefinitionDto(
    Guid Id,
    string SchemaViewCode,
    string SchemaViewName,
    string ViewKind,
    Guid? PrimarySourceDatasetDefinitionId,
    string SqlText,
    string SourceDatasetIdsJson,
    string OutputSchemaJson,
    int MaxPreviewRows,
    int TimeoutSeconds,
    bool IsApproved,
    bool IsActive,
    DateTime? LastValidatedAtUtc,
    string? LastValidationStatus,
    string? LastValidationMessage,
    string? Description,
    bool IsSynthetic,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateSchemaViewDefinitionRequest(
    string SchemaViewCode,
    string SchemaViewName,
    string ViewKind,
    Guid? PrimarySourceDatasetDefinitionId,
    string SqlText,
    string? SourceDatasetIdsJson,
    int? MaxPreviewRows,
    int? TimeoutSeconds,
    string? Description,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record UpdateSchemaViewDefinitionRequest(
    string SchemaViewName,
    string ViewKind,
    Guid? PrimarySourceDatasetDefinitionId,
    string SqlText,
    string? SourceDatasetIdsJson,
    int? MaxPreviewRows,
    int? TimeoutSeconds,
    string? Description);

public sealed record SchemaViewPreviewRequest(
    string? SqlText,
    int? MaxRows,
    int? TimeoutSeconds);

public sealed record SchemaViewPreviewColumnDto(
    string ColumnName,
    string DataType,
    int Ordinal);

public sealed record SchemaViewPreviewResult(
    bool IsSuccess,
    string Message,
    int RowCount,
    long DurationMs,
    IReadOnlyList<SchemaViewPreviewColumnDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

public sealed record SqlSafetyValidationResultDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ReferencedTables);

public sealed record KpiDefinitionDto(
    Guid Id,
    Guid? SchemaViewDefinitionId,
    string KpiCode,
    string KpiName,
    string KpiCategory,
    string ValueExpression,
    string? Unit,
    string? DimensionExpression,
    string? FilterExpression,
    string AggregationType,
    string KpiOptionsJson,
    bool IsActive,
    string? Description,
    bool IsSynthetic,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateKpiDefinitionRequest(
    Guid? SchemaViewDefinitionId,
    string KpiCode,
    string KpiName,
    string KpiCategory,
    string ValueExpression,
    string? Unit,
    string? DimensionExpression,
    string? FilterExpression,
    string? AggregationType,
    string? KpiOptionsJson,
    string? Description,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);



