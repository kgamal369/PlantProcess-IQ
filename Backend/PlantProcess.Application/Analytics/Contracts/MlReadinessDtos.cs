namespace PlantProcess.Application.Analytics.Contracts;

public sealed record QualityTrainingLabelDto(
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    string LabelCode,
    bool HasDefect,
    bool IsRejected,
    bool IsDowngraded,
    bool IsReworked,
    string? PrimaryDefectCode,
    string? PrimaryDefectName,
    string? PrimaryDefectCategory,
    int QualityEventCount,
    int UpstreamObservationCount,
    int GenealogyEdgeCount,
    DateTime? FirstQualityEventAtUtc,
    DateTime? LastObservationAtUtc);

public sealed record QualityTrainingLabelPreviewDto(
    DateTime GeneratedAtUtc,
    int RequestedLimit,
    int ReturnedCount,
    IReadOnlyList<QualityTrainingLabelDto> Labels);

public sealed record MlReadinessMetricDto(
    string Code,
    string Name,
    decimal CurrentValue,
    decimal RequiredValue,
    string Unit,
    bool IsReady,
    string Status,
    string Message);

public sealed record MlReadinessScoreDto(
    DateTime GeneratedAtUtc,
    string OverallStatus,
    decimal ScorePercent,
    bool CanStartTraining,
    string TrainingStatus,
    string HonestPositioning,
    IReadOnlyList<MlReadinessMetricDto> Metrics,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NextActions);

public sealed record MlJobReadinessDto(
    Guid JobId,
    string JobCode,
    string JobName,
    string JobType,
    bool IsEnabled,
    string LastRunStatus,
    string ScheduleExpression,
    string ReadinessStatus,
    string Reason);

public sealed record ModelRegistryLifecycleDto(
    Guid Id,
    string ModelCode,
    string ModelName,
    string ModelType,
    string ModelVersion,
    string RiskType,
    bool IsActive,
    string LifecycleState,
    string GovernanceMessage,
    DateTime RegisteredAtUtc);

public sealed record CorrelationLifecycleDto(
    Guid Id,
    string CorrelationType,
    string SubjectCode,
    string OutcomeCode,
    decimal? Score,
    string LifecycleState,
    string GovernanceMessage,
    DateTime CalculatedAtUtc);

public sealed record MlWorkspaceReadinessDto(
    DateTime GeneratedAtUtc,
    MlReadinessScoreDto Readiness,
    QualityTrainingLabelPreviewDto LabelPreview,
    IReadOnlyList<MlJobReadinessDto> MlJobs,
    IReadOnlyList<ModelRegistryLifecycleDto> ModelRegistry,
    IReadOnlyList<CorrelationLifecycleDto> Correlations,
    string CurrentIntelligence,
    string FutureMlLifecycle,
    string Disclaimer);