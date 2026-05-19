using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Readiness;
using PlantProcess.Application.Services.Readiness;
using PlantProcess.Application.Services.Reporting;

namespace PlantProcess.Api.Endpoints.Reporting;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization("PlantProcessViewer");

        reports.MapGet("/materials/{materialUnitId:guid}/investigation", GetInvestigationReportAsync)
            .WithSummary("Get material investigation report");

        reports.MapGet("/materials/{materialUnitId:guid}/investigation/pdf", GetInvestigationPdfAsync)
            .WithSummary("Export material investigation report as PDF");

        reports.MapPost("/readiness-assessment", CreateReadinessReportAsync)
            .WithSummary("Create commercial readiness report")
            .WithDescription("Generates a 7-dimension commercial readiness report for Data Diagnostic assessment.");

        reports.MapGet("/readiness-assessment", GetReadinessReportAsync)
            .WithSummary("Get commercial readiness report");

        reports.MapPost("/readiness-assessment/pdf", CreateReadinessPdfAsync)
            .WithSummary("Export commercial readiness report as PDF");

        reports.MapGet("/readiness-assessment/pdf", GetReadinessPdfAsync)
            .WithSummary("Download commercial readiness report PDF");

        var readiness = app.MapGroup("/readiness")
            .WithTags("Readiness")
            .RequireAuthorization("PlantProcessViewer");

        readiness.MapGet("/", GetReadinessAsync)
            .WithSummary("Get 7-dimension readiness score");

        readiness.MapPost("/report", CreateReadinessReportAsync)
            .WithSummary("Create commercial readiness report");

        readiness.MapGet("/report", GetReadinessReportAsync)
            .WithSummary("Get commercial readiness report");

        readiness.MapPost("/report/pdf", CreateReadinessPdfAsync)
            .WithSummary("Export readiness report PDF");

        readiness.MapGet("/report/pdf", GetReadinessPdfAsync)
            .WithSummary("Download readiness report PDF");

        return app;
    }

    private static async Task<IResult> GetInvestigationReportAsync(
        Guid materialUnitId,
        IInvestigationReportService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.BuildMaterialInvestigationReportAsync(
            materialUnitId,
            httpContext.User?.Identity?.Name,
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetInvestigationPdfAsync(
        Guid materialUnitId,
        IInvestigationReportService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.BuildMaterialInvestigationPdfAsync(
            materialUnitId,
            httpContext.User?.Identity?.Name,
            cancellationToken);

        return result.ToHttpResult(value => Results.File(
            value.Content,
            value.ContentType,
            value.FileName));
    }

    private static async Task<IResult> GetReadinessAsync(
        IApplicationReadinessService readinessService,
        CancellationToken cancellationToken)
    {
        var result = await readinessService.GetReadinessAsync(cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetReadinessReportAsync(
        IApplicationReadinessService readinessService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = new CommercialReadinessReportRequest(
            CustomerName: "Customer",
            PreparedBy: "PlantProcess IQ",
            RequestedBy: httpContext.User?.Identity?.Name);

        var result = await readinessService.BuildCommercialReadinessReportAsync(
            request,
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> CreateReadinessReportAsync(
        CommercialReadinessReportRequest request,
        IApplicationReadinessService readinessService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var normalized = request with
        {
            RequestedBy = request.RequestedBy ?? httpContext.User?.Identity?.Name
        };

        var result = await readinessService.BuildCommercialReadinessReportAsync(
            normalized,
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetReadinessPdfAsync(
        IApplicationReadinessService readinessService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = new CommercialReadinessReportRequest(
            CustomerName: "Customer",
            PreparedBy: "PlantProcess IQ",
            RequestedBy: httpContext.User?.Identity?.Name);

        var result = await readinessService.BuildCommercialReadinessPdfAsync(
            request,
            cancellationToken);

        return result.ToHttpResult(value => Results.File(
            value.Content,
            value.ContentType,
            value.FileName));
    }

    private static async Task<IResult> CreateReadinessPdfAsync(
        CommercialReadinessReportRequest request,
        IApplicationReadinessService readinessService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var normalized = request with
        {
            RequestedBy = request.RequestedBy ?? httpContext.User?.Identity?.Name
        };

        var result = await readinessService.BuildCommercialReadinessPdfAsync(
            normalized,
            cancellationToken);

        return result.ToHttpResult(value => Results.File(
            value.Content,
            value.ContentType,
            value.FileName));
    }
}