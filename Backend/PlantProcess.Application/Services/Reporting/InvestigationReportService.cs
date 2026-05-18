using PlantProcess.Application.Analytics.Contracts;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Reporting;


namespace PlantProcess.Application.Services.Reporting;

public sealed class InvestigationReportService : IInvestigationReportService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IFeatureEngineeringService _featureEngineeringService;
    private readonly ILogger<InvestigationReportService> _logger;

    public InvestigationReportService(
        IPlantProcessDbContext dbContext,
        IFeatureEngineeringService featureEngineeringService,
        ILogger<InvestigationReportService> logger)
    {
        _dbContext = dbContext;
        _featureEngineeringService = featureEngineeringService;
        _logger = logger;
    }

    public async Task<ApplicationResult<InvestigationReportDto>> BuildMaterialInvestigationReportAsync(
        Guid materialUnitId,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        var vectorResult = await _featureEngineeringService.BuildMaterialFeatureVectorAsync(materialUnitId, cancellationToken);
        if (vectorResult.IsFailure || vectorResult.Value is null)
            return ApplicationResult<InvestigationReportDto>.Failure(vectorResult.Error ?? ApplicationError.Unexpected("Could not build feature vector."));

        var vector = vectorResult.Value;
        var risks = await _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderByDescending(x => x.ScoredAtUtc)
            .Take(5)
            .Select(x => new { x.RiskType, x.Score, x.RiskClass, x.ModelVersion, x.ScoredAtUtc, x.MainContributorsJson })
            .ToListAsync(cancellationToken);

        var latestRisk = risks.FirstOrDefault();
        var executiveSummary = BuildExecutiveSummary(vector, latestRisk?.Score, latestRisk?.RiskClass);
        var riskInterpretation = latestRisk is null
            ? "No calculated risk score is currently stored for this material. Run POST /risk-scores/materials/{id}/calculate first."
            : $"Latest calculated risk is {latestRisk.Score:P1} ({latestRisk.RiskClass ?? "Unclassified"}) using {latestRisk.ModelVersion ?? "unknown model"}.";

        var sections = new List<InvestigationReportSectionDto>
        {
            new("MATERIAL", "Material Identity", new[]
            {
                $"Material: {vector.MaterialCode} ({vector.MaterialUnitType})",
                $"Product family: {vector.ProductFamily ?? "n/a"}",
                $"Grade/recipe: {vector.GradeOrRecipe ?? "n/a"}",
                $"Production window UTC: {FormatDate(vector.ProductionStartUtc)} â†’ {FormatDate(vector.ProductionEndUtc)}",
                $"Feature version: {vector.FeatureVersion}"
            }),
            new("PROCESS", "Process Timeline Summary", new[]
            {
                $"Process steps: {vector.ProcessStepCount}",
                $"Completed steps: {vector.CompletedProcessStepCount}",
                $"Running steps: {vector.RunningProcessStepCount}",
                $"Aborted steps: {vector.AbortedProcessStepCount}",
                $"Distinct equipment: {vector.DistinctEquipmentCount}",
                $"Total process duration: {vector.TotalProcessDurationMinutes:N1} minutes",
                $"Total downtime/delay: {vector.TotalDowntimeMinutes:N1} minutes"
            }),
            new("PARAMETERS", "Parameter Feature Summary", vector.ParameterAggregates
                .OrderByDescending(x => x.AnomalyRatio)
                .Take(10)
                .Select(x => $"{x.ParameterCode}: count={x.ObservationCount}, avg={FormatDecimal(x.AverageValue)}, min={FormatDecimal(x.MinValue)}, max={FormatDecimal(x.MaxValue)}, anomalyRatio={x.AnomalyRatio:P1}")
                .DefaultIfEmpty("No parameter observations found.")
                .ToList()),
            new("QUALITY", "Quality Signals", vector.QualitySignals
                .OrderBy(x => x.EventAtUtc)
                .Take(20)
                .Select(x => $"{x.EventAtUtc:u} â€” {x.EventType} / {x.DefectCode ?? "n/a"} / {x.Decision ?? "n/a"}: {x.Description ?? ""}")
                .DefaultIfEmpty("No quality events found.")
                .ToList()),
            new("RISK", "Risk Scores", risks
                .Select(x => $"{x.ScoredAtUtc:u} â€” {x.RiskType}: {x.Score:P1} ({x.RiskClass ?? "Unclassified"}) model={x.ModelVersion ?? "n/a"}")
                .DefaultIfEmpty("No risk scores stored yet.")
                .ToList()),
            new("DQ", "Data Quality Signals", vector.DataQualitySignals
                .Take(20)
                .Select(x => $"{x.Severity} â€” {x.IssueType}: {x.Description}")
                .DefaultIfEmpty("No data-quality issues found for this material.")
                .ToList()),
            new("CORRELATION", "Correlation Context", vector.CorrelationHints
                .Take(20)
                .Select(x => $"{x.CorrelationType}: {x.SubjectCode} â†’ {x.OutcomeCode}, score={FormatDecimal(x.Score)}")
                .DefaultIfEmpty("No saved correlation hints matched this material.")
                .ToList())
        };

        var report = new InvestigationReportDto(
            vector.MaterialUnitId,
            vector.MaterialCode,
            vector.MaterialUnitType,
            vector.ProductFamily,
            vector.GradeOrRecipe,
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(requestedBy) ? "PlantProcess IQ" : requestedBy.Trim(),
            executiveSummary,
            riskInterpretation,
            vector.ProcessStepCount,
            vector.ParameterObservationCount,
            vector.QualityEventCount,
            vector.DataQualityIssueCount,
            risks.Count,
            sections);

        _logger.LogInformation("Built investigation report. MaterialUnitId={MaterialUnitId}, Sections={Sections}", materialUnitId, report.Sections.Count);
        return ApplicationResult<InvestigationReportDto>.Success(report);
    }

    public async Task<ApplicationResult<InvestigationPdfReportResult>> BuildMaterialInvestigationPdfAsync(
        Guid materialUnitId,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        var reportResult = await BuildMaterialInvestigationReportAsync(materialUnitId, requestedBy, cancellationToken);
        if (reportResult.IsFailure || reportResult.Value is null)
            return ApplicationResult<InvestigationPdfReportResult>.Failure(reportResult.Error ?? ApplicationError.Unexpected("Could not build report."));

        var report = reportResult.Value;
        var pdfBytes = SimplePdfWriter.CreatePdf(BuildPlainTextReport(report));
        var safeCode = string.Join("_", report.MaterialCode.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var fileName = $"PlantProcessIQ_Investigation_{safeCode}_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";

        return ApplicationResult<InvestigationPdfReportResult>.Success(new InvestigationPdfReportResult(
            materialUnitId,
            fileName,
            "application/pdf",
            pdfBytes));
    }

    private static string BuildPlainTextReport(InvestigationReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PlantProcess IQ â€” Material Investigation Report");
        sb.AppendLine($"Generated UTC : {report.GeneratedAtUtc:u}");
        sb.AppendLine($"Generated By  : {report.GeneratedBy}");
        sb.AppendLine($"Material      : {report.MaterialCode} ({report.MaterialUnitType})");
        sb.AppendLine();
        sb.AppendLine("Executive Summary");
        sb.AppendLine(report.ExecutiveSummary);
        sb.AppendLine();
        sb.AppendLine("Risk Interpretation");
        sb.AppendLine(report.RiskInterpretation);
        sb.AppendLine();

        foreach (var section in report.Sections)
        {
            sb.AppendLine(section.SectionTitle);
            foreach (var line in section.Lines)
                sb.AppendLine("- " + line);
            sb.AppendLine();
        }

        sb.AppendLine("Disclaimer: PlantProcess IQ provides correlation and risk-support evidence. It does not prove root cause without process-engineer validation.");
        return sb.ToString();
    }

    private static string BuildExecutiveSummary(PlantProcess.Application.Analytics.Contracts.MaterialFeatureVectorDto vector, decimal? score, string? riskClass)
    {
        var risk = score.HasValue ? $"latest risk score is {score.Value:P1} ({riskClass ?? "Unclassified"})" : "no calculated risk score is stored yet";
        return $"Material {vector.MaterialCode} has {vector.ProcessStepCount} process step(s), {vector.ParameterObservationCount} parameter observation(s), {vector.DefectEventCount} defect signal(s), and {vector.DataQualityIssueCount} data-quality issue(s). The {risk}.";
    }

    private static string FormatDate(DateTime? value) => value.HasValue ? value.Value.ToString("u") : "n/a";
    private static string FormatDecimal(decimal? value) => value.HasValue ? value.Value.ToString("0.######") : "n/a";

    private static class SimplePdfWriter
    {
        public static byte[] CreatePdf(string text)
        {
            var lines = text.Replace("\r", "").Split('\n')
                .SelectMany(WrapLine)
                .Take(220)
                .ToList();

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\n" };
            var offsets = new List<long>();

            void Write(string value) { writer.Write(value); writer.Flush(); }
            void Obj(int number, string body)
            {
                offsets.Add(stream.Position);
                Write($"{number} 0 obj\n{body}\nendobj\n");
            }

            Write("%PDF-1.4\n");
            Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
            Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
            Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 9 Tf");
            content.AppendLine("50 800 Td");
            foreach (var line in lines)
            {
                content.AppendLine($"({EscapePdf(line)}) Tj");
                content.AppendLine("0 -12 Td");
            }
            content.AppendLine("ET");

            var contentBytes = Encoding.ASCII.GetBytes(content.ToString());
            offsets.Add(stream.Position);
            Write($"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
            writer.Flush();
            stream.Write(contentBytes, 0, contentBytes.Length);
            Write("\nendstream\nendobj\n");

            var xref = stream.Position;
            Write("xref\n0 6\n0000000000 65535 f \n");
            foreach (var offset in offsets)
                Write($"{offset:0000000000} 00000 n \n");
            Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            writer.Flush();
            return stream.ToArray();
        }

        private static IEnumerable<string> WrapLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return new[] { " " };
            const int length = 92;
            var chunks = new List<string>();
            var remaining = line.Trim();
            while (remaining.Length > length)
            {
                chunks.Add(remaining[..length]);
                remaining = remaining[length..];
            }
            chunks.Add(remaining);
            return chunks;
        }

        private static string EscapePdf(string value)
        {
            return value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }
    }
}




