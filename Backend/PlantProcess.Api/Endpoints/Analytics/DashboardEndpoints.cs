using PlantProcess.Api.Extensions;
using PlantProcess.Application.Services.Analytics;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/dashboard")
            .WithTags("Dashboard");

        group.MapGet("/overview", GetOverviewAsync);
        group.MapGet("/quality", GetQualityAsync);
        group.MapGet("/risk", GetRiskAsync);
        group.MapGet("/data-quality", GetDataQualityAsync);

        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOverviewAsync(siteId, fromUtc, toUtc, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetQualityAsync(
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetQualityDashboardAsync(siteId, fromUtc, toUtc, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetRiskAsync(
        Guid? siteId,
        int? highRiskTake,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetRiskDashboardAsync(siteId, highRiskTake ?? 20, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetDataQualityAsync(
        Guid? siteId,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDataQualityDashboardAsync(siteId, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}
