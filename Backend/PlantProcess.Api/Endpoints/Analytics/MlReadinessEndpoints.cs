using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class MlReadinessEndpoints
{
    public static IEndpointRouteBuilder MapMlReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/ml-readiness")
            .WithTags("ML Readiness")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/score", GetReadinessAsync)
            .WithSummary("Get ML dataset readiness score")
            .WithDescription("Returns honest ML readiness gates. Does not train a model.");

        group.MapGet("/labels/preview", GetLabelPreviewAsync)
            .WithSummary("Preview quality training labels")
            .WithDescription("Builds material-level quality labels from canonical quality events and genealogy.");

        group.MapGet("/jobs", GetMlJobsAsync)
            .WithSummary("Get ML job definitions")
            .WithDescription("Shows planned ML jobs as disabled/honest until training prerequisites are ready.");

        group.MapPost("/jobs/ensure", EnsureMlJobsAsync)
            .WithSummary("Ensure disabled ML job definitions exist")
            .WithDescription("Creates the four planned disabled ML job definitions if missing.");

        group.MapGet("/workspace", GetWorkspaceAsync)
            .WithSummary("Get ML readiness workspace")
            .WithDescription("Aggregates readiness, labels, ML jobs, model registry, and correlation lifecycle.");

        return app;
    }

    private static async Task<IResult> GetReadinessAsync(
        IMlReadinessService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetReadinessAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLabelPreviewAsync(
        int? limit,
        IQualityLabelBuilderService service,
        CancellationToken cancellationToken)
    {
        var result = await service.BuildPreviewAsync(limit ?? 50, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetMlJobsAsync(
        IMlReadinessService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMlJobsAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> EnsureMlJobsAsync(
        IMlReadinessService service,
        CancellationToken cancellationToken)
    {
        await service.EnsureMlJobDefinitionsAsync(cancellationToken);
        var result = await service.GetMlJobsAsync(cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            message = "ML job definitions ensured. They remain disabled until readiness gates are green.",
            jobs = result
        });
    }

    private static async Task<IResult> GetWorkspaceAsync(
        int? labelPreviewLimit,
        IMlReadinessService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetWorkspaceAsync(
            labelPreviewLimit ?? 25,
            cancellationToken);

        return Results.Ok(result);
    }
}