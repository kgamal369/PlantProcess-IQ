using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Reporting;

public static class CustomerDemoReportEndpoints
{
    public static IEndpointRouteBuilder MapCustomerDemoReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/customer-demo")
            .WithTags("Reports - Customer Demo");

        group.MapGet("/phase1.pdf", GeneratePhase1PdfReport)
            .WithName("GeneratePhase1CustomerDemoPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/phase1-summary", GetPhase1ReportSummary)
            .WithName("GetPhase1CustomerDemoReportSummary")
            .Produces<Phase1ReportSummary>();

        return app;
    }

    private static async Task<IResult> GetPhase1ReportSummary(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var summary = await BuildSummaryAsync(dbContext, cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> GeneratePhase1PdfReport(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var summary = await BuildSummaryAsync(dbContext, cancellationToken);

        var lines = new List<string>
        {
            "PlantProcess IQ",
            "Phase 1 Customer Demo Report",
            "",
            $"Generated UTC: {summary.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}",
            "",
            "Executive Summary",
            "PlantProcess IQ connected source-shaped demo schemas, staged raw data, prepared mapping views, and enabled safe dashboard expression execution.",
            "",
            "Important Truth Statement",
            "This report shows evidence-based process/quality investigation. It does not claim guaranteed root cause or production ML prediction.",
            "",
            "Source Data Coverage",
            $"MeltShop heats: {summary.MeltShopHeatCount:N0}",
            $"Caster slabs: {summary.CasterPieceCount:N0}",
            $"HSM coils: {summary.HsmCoilCount:N0}",
            $"HSM pass measurements: {summary.HsmPassMeasurementCount:N0}",
            $"Pickling orders: {summary.PicklingOrderCount:N0}",
            $"QA lab results: {summary.QaLabResultCount:N0}",
            $"Surface defects: {summary.SurfaceDefectCount:N0}",
            $"Downtime events: {summary.DowntimeEventCount:N0}",
            "",
            "PlantProcess IQ Operational Layer",
            $"Connection profiles: {summary.ConnectionProfileCount:N0}",
            $"Source datasets: {summary.SourceDatasetCount:N0}",
            $"Import batches: {summary.ImportBatchCount:N0}",
            $"Staging records: {summary.StagingRecordCount:N0}",
            $"Schema views: {summary.SchemaViewCount:N0}",
            $"Mapping definitions: {summary.MappingDefinitionCount:N0}",
            $"Job definitions: {summary.JobDefinitionCount:N0}",
            $"Job run history records: {summary.JobRunHistoryCount:N0}",
            "",
            "Quality / Investigation Layer",
            $"Material units: {summary.MaterialUnitCount:N0}",
            $"Quality events: {summary.QualityEventCount:N0}",
            $"Parameter observations: {summary.ParameterObservationCount:N0}",
            $"Risk scores: {summary.RiskScoreCount:N0}",
            $"Data quality issues: {summary.DataQualityIssueCount:N0}",
            "",
            "Recommended Demo Narrative",
            "1. Show connector truth and provider honesty.",
            "2. Show source-shaped schemas and staging/latest-copy layer.",
            "3. Show schema mapping and safe view preview.",
            "4. Show scheduled import jobs and job monitor.",
            "5. Show dashboard widget expression preview.",
            "6. Show customer PDF report as the story close.",
            "",
            "Positioning",
            "PlantProcess IQ is not MES, not SCADA, not Level 2, and not BI-only. It is a manufacturing intelligence layer above existing systems."
        };

        var pdfBytes = SimplePdfWriter.Create("PlantProcess IQ Phase 1 Demo Report", lines);

        return Results.File(
            pdfBytes,
            "application/pdf",
            $"PlantProcessIQ_Phase1_Demo_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    private static async Task<Phase1ReportSummary> BuildSummaryAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var meltShopHeatCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_meltshop_pg.heats", cancellationToken);
        var casterPieceCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_caster_oracle_shape.cast_pieces", cancellationToken);
        var hsmCoilCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_hsm_oracle_shape.hsm_coils", cancellationToken);
        var hsmPassMeasurementCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_hsm_oracle_shape.hsm_pass_measurements", cancellationToken);
        var picklingOrderCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_pkl_mssql_shape.pickle_orders", cancellationToken);
        var qaLabResultCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_pkl_mssql_shape.qa_lab_results", cancellationToken);
        var surfaceDefectCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_inspection_mysql_shape.parsytec_surface_defects", cancellationToken);
        var downtimeEventCount = await CountSqlAsync(dbContext, "SELECT COUNT(*) FROM src_inspection_mysql_shape.downtime_events", cancellationToken);

        return new Phase1ReportSummary(
            GeneratedAtUtc: DateTime.UtcNow,
            MeltShopHeatCount: meltShopHeatCount,
            CasterPieceCount: casterPieceCount,
            HsmCoilCount: hsmCoilCount,
            HsmPassMeasurementCount: hsmPassMeasurementCount,
            PicklingOrderCount: picklingOrderCount,
            QaLabResultCount: qaLabResultCount,
            SurfaceDefectCount: surfaceDefectCount,
            DowntimeEventCount: downtimeEventCount,
            ConnectionProfileCount: await dbContext.ConnectionProfiles.CountAsync(cancellationToken),
            SourceDatasetCount: await dbContext.SourceDatasetDefinitions.CountAsync(cancellationToken),
            ImportBatchCount: await dbContext.ImportBatches.CountAsync(cancellationToken),
            StagingRecordCount: await dbContext.StagingRecords.CountAsync(cancellationToken),
            SchemaViewCount: await dbContext.SchemaViewDefinitions.CountAsync(cancellationToken),
            MappingDefinitionCount: await dbContext.MappingDefinitions.CountAsync(cancellationToken),
            JobDefinitionCount: await dbContext.JobDefinitions.CountAsync(cancellationToken),
            JobRunHistoryCount: await dbContext.JobRunHistories.CountAsync(cancellationToken),
            MaterialUnitCount: await dbContext.MaterialUnits.CountAsync(cancellationToken),
            QualityEventCount: await dbContext.QualityEvents.CountAsync(cancellationToken),
            ParameterObservationCount: await dbContext.ParameterObservations.CountAsync(cancellationToken),
            RiskScoreCount: await dbContext.RiskScores.CountAsync(cancellationToken),
            DataQualityIssueCount: await dbContext.DataQualityIssues.CountAsync(cancellationToken));
    }

    private static async Task<int> CountSqlAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }
}

public sealed record Phase1ReportSummary(
    DateTime GeneratedAtUtc,
    int MeltShopHeatCount,
    int CasterPieceCount,
    int HsmCoilCount,
    int HsmPassMeasurementCount,
    int PicklingOrderCount,
    int QaLabResultCount,
    int SurfaceDefectCount,
    int DowntimeEventCount,
    int ConnectionProfileCount,
    int SourceDatasetCount,
    int ImportBatchCount,
    int StagingRecordCount,
    int SchemaViewCount,
    int MappingDefinitionCount,
    int JobDefinitionCount,
    int JobRunHistoryCount,
    int MaterialUnitCount,
    int QualityEventCount,
    int ParameterObservationCount,
    int RiskScoreCount,
    int DataQualityIssueCount);

internal static class SimplePdfWriter
{
    public static byte[] Create(string title, IReadOnlyList<string> lines)
    {
        var objects = new List<string>();
        var content = BuildContent(lines);

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");

        var sb = new StringBuilder();
        sb.AppendLine("%PDF-1.4");

        var offsets = new List<int> { 0 };

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.AppendLine($"{i + 1} 0 obj");
            sb.AppendLine(objects[i]);
            sb.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(sb.ToString());

        sb.AppendLine("xref");
        sb.AppendLine($"0 {objects.Count + 1}");
        sb.AppendLine("0000000000 65535 f ");

        foreach (var offset in offsets.Skip(1))
            sb.AppendLine($"{offset:0000000000} 00000 n ");

        sb.AppendLine("trailer");
        sb.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R /Info << /Title ({EscapePdf(title)}) >> >>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string BuildContent(IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();

        sb.AppendLine("BT");
        sb.AppendLine("/F1 18 Tf");
        sb.AppendLine("50 800 Td");
        sb.AppendLine($"({EscapePdf(lines.FirstOrDefault() ?? "PlantProcess IQ Report")}) Tj");

        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("0 -24 Td");

        foreach (var line in lines.Skip(1).Take(58))
        {
            var safe = EscapePdf(line.Length > 105 ? line[..105] : line);
            sb.AppendLine($"({safe}) Tj");
            sb.AppendLine("0 -13 Td");
        }

        sb.AppendLine("ET");

        return sb.ToString();
    }

    private static string EscapePdf(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}