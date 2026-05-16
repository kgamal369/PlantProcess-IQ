namespace PlantProcess.Domain.Enums.Integration;

/// <summary>
/// Defines the operational job categories managed by PlantProcess IQ.
///
/// This enum is intentionally generic and not tied to any specific industry.
/// Jobs can represent raw source imports, canonical refreshes, ML learning,
/// data-quality scans, risk scoring, or future custom customer workflows.
/// </summary>
public enum JobDefinitionType
{
    DbLinkImport = 1,
    CanonicalRefresh = 2,

    MlParamsVsDefects = 10,
    MlParamsVsDowntime = 11,
    MlParamsVsKpis = 12,
    MlWeeklyFull = 13,

    DataQualityScan = 20,
    RiskScoring = 21,

    Custom = 99
}