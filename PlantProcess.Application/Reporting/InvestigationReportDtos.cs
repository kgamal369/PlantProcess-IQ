namespace PlantProcess.Application.Contracts.Reporting;

public sealed record InvestigationReportDto(
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    string? ProductFamily,
    string? GradeOrRecipe,
    DateTime GeneratedAtUtc,
    string GeneratedBy,
    string ExecutiveSummary,
    string RiskInterpretation,
    int ProcessStepCount,
    int ParameterObservationCount,
    int QualityEventCount,
    int DataQualityIssueCount,
    int RiskScoreCount,
    IReadOnlyList<InvestigationReportSectionDto> Sections);

public sealed record InvestigationReportSectionDto(
    string SectionCode,
    string SectionTitle,
    IReadOnlyList<string> Lines);

public sealed record InvestigationPdfReportResult(
    Guid MaterialUnitId,
    string FileName,
    string ContentType,
    byte[] Content);
