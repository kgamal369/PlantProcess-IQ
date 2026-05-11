namespace PlantProcess.Application.Contracts.Analytics;

public sealed record ParameterDefectCorrelationQuery(
    string ParameterCode,
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int RequestedBins,
    int MinimumObservationsPerBin,
    bool PersistResult);

public sealed record ParameterDefectCorrelationResult(
    string ParameterCode,
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int MaterialPopulation,
    int DefectMaterialCount,
    decimal OverallDefectRate,
    int BinCount,
    decimal? StrongestLift,
    string? StrongestBinLabel,
    Guid? PersistedCorrelationResultId,
    IReadOnlyCollection<ParameterDefectCorrelationBin> Bins);

public sealed record ParameterDefectCorrelationBin(
    int BinNumber,
    string BinLabel,
    decimal MinValue,
    decimal MaxValue,
    int ObservationCount,
    int MaterialCount,
    int DefectMaterialCount,
    decimal DefectRate,
    decimal LiftVsOverall);

public sealed record EquipmentDefectRateQuery(
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int MinimumMaterialsPerEquipment);

public sealed record EquipmentDefectRateResult(
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int EquipmentCount,
    IReadOnlyCollection<EquipmentDefectRateRow> Rows);

public sealed record EquipmentDefectRateRow(
    Guid? EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    string EquipmentType,
    int MaterialCount,
    int DefectMaterialCount,
    decimal DefectRate);

public sealed record OperationDefectRateQuery(
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int MinimumMaterialsPerOperation);

public sealed record OperationDefectRateResult(
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int OperationCount,
    IReadOnlyCollection<OperationDefectRateRow> Rows);

public sealed record OperationDefectRateRow(
    string OperationType,
    string? OperationCode,
    int MaterialCount,
    int DefectMaterialCount,
    decimal DefectRate);

public sealed record MaterialCorrelationContextResult(
    Guid MaterialUnitId,
    string MaterialCode,
    IReadOnlyCollection<MaterialParameterRiskIndicator> ParameterIndicators,
    IReadOnlyCollection<MaterialEquipmentRiskIndicator> EquipmentIndicators);

public sealed record MaterialParameterRiskIndicator(
    string ParameterCode,
    string ParameterName,
    decimal NumericValue,
    string? UnitOfMeasure,
    string? RiskBinLabel,
    decimal? RiskBinDefectRate,
    decimal? LiftVsOverall,
    DateTime ObservedAtUtc);

public sealed record MaterialEquipmentRiskIndicator(
    Guid? EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    decimal? EquipmentDefectRate,
    int? EquipmentMaterialCount);
