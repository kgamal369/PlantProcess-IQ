using PlantProcess.Api.Extensions;
using PlantProcess.Application.Services.Reporting;

namespace PlantProcess.Api.Endpoints.Reporting;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports")
            .WithTags("Reports");

        group.MapGet("/materials/{materialUnitId:guid}/investigation", GetInvestigationReportAsync);
        group.MapGet("/materials/{materialUnitId:guid}/investigation/pdf", GetInvestigationPdfAsync);

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
}

