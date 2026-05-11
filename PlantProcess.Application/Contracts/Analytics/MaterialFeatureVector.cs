namespace PlantProcess.Application.Contracts.Analytics;

/// <summary>
/// Material-level feature vector used by Phase H risk prediction.
///
/// Design rules:
/// - Generic manufacturing concepts only; no steel-only hard-coding in the contract.
/// - Steel/aluminum/pharma/etc. details appear only as parameter codes, operation codes, defect codes, and templates.
/// - This DTO is intentionally inspectable through GET /analytics/features/{materialUnitId} before scoring.
/// </summary>
public sealed record MaterialFeatureVectorDto(
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    Guid SiteId,
    string? ProductFamily,
    string? GradeOrRecipe,
    DateTime? ProductionStartUtc,
    DateTime? ProductionEndUtc,
    decimal ProductionDurationMinutes,
    int ProcessStepCount,
    int CompletedProcessStepCount,
    int RunningProcessStepCount,
    int AbortedProcessStepCount,
    int DistinctEquipmentCount,
    int DistinctCrewCount,
    decimal TotalProcessDurationMinutes,
    decimal TotalDowntimeMinutes,
    int DowntimeEventCount,
    int ParameterObservationCount,
    int ParameterAnomalyCount,
    decimal ParameterAnomalyRatio,
    int QualityEventCount,
    int DefectEventCount,
    int DataQualityIssueCount,
    string FeatureVersion,
    DateTime GeneratedAtUtc,
    IReadOnlyList<ProcessStepFeatureDto> ProcessSteps,
    IReadOnlyList<EquipmentExposureFeatureDto> EquipmentExposure,
    IReadOnlyList<ParameterAggregateFeatureDto> ParameterAggregates,
    IReadOnlyList<QualitySignalFeatureDto> QualitySignals,
    IReadOnlyList<DataQualitySignalFeatureDto> DataQualitySignals,
    IReadOnlyList<CorrelationHintFeatureDto> CorrelationHints);

public sealed record ProcessStepFeatureDto(
    Guid ProcessStepExecutionId,
    Guid? EquipmentId,
    string? EquipmentCode,
    string? EquipmentName,
    Guid? OperationDefinitionId,
    string OperationType,
    string? OperationCode,
    string? CrewCode,
    string ExecutionStatus,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    decimal DurationMinutes);

public sealed record EquipmentExposureFeatureDto(
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    string EquipmentType,
    int ProcessStepCount,
    decimal TotalDurationMinutes,
    int ParameterObservationCount,
    int QualityEventCount);

public sealed record ParameterAggregateFeatureDto(
    Guid ParameterDefinitionId,
    string ParameterCode,
    string ParameterName,
    string? ParameterCategory,
    string? IndustryTemplate,
    string ValueType,
    string? UnitOfMeasure,
    int ObservationCount,
    decimal? MinValue,
    decimal? MaxValue,
    decimal? AverageValue,
    decimal? StandardDeviation,
    decimal? LatestNumericValue,
    string? LatestTextValue,
    bool? LatestBooleanValue,
    DateTime? LatestObservedAtUtc,
    decimal? ExpectedMinValue,
    decimal? ExpectedMaxValue,
    int BelowExpectedCount,
    int AboveExpectedCount,
    int MissingValueCount,
    int InvalidQualityFlagCount,
    decimal AnomalyRatio);

public sealed record QualitySignalFeatureDto(
    Guid QualityEventId,
    string EventType,
    string? DefectCode,
    string? DefectName,
    string? DefectCategory,
    string? Severity,
    string? Decision,
    DateTime EventAtUtc,
    string? Description);

public sealed record DataQualitySignalFeatureDto(
    Guid DataQualityIssueId,
    string IssueType,
    string Severity,
    string Description,
    string? AffectedEntityName,
    Guid? AffectedEntityId,
    DateTime CreatedAtUtc);

public sealed record CorrelationHintFeatureDto(
    Guid CorrelationResultId,
    string CorrelationType,
    string SubjectCode,
    string OutcomeCode,
    decimal? Score,
    DateTime CalculatedAtUtc);
