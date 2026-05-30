using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.DynamicContent;

public static class DynamicContentEndpoints
{
    public static IEndpointRouteBuilder MapDynamicContentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api")
            .WithTags("Dynamic Content")
            .RequireAuthorization("PlantProcessViewer");

        api.MapGet("/suggestions", GetSuggestionsAsync)
            .WithSummary("PPIQ-WF-019: Ranked investigation recommendations");

        api.MapGet("/pages/{slug}", GetPageAsync)
            .WithSummary("PPIQ-WF-020: Load dynamic user-defined platform page");

        return app;
    }

    private static async Task<IResult> GetSuggestionsAsync(
        PlantProcessDbContext dbContext,
        string? materialUnitId,
        string? context,
        CancellationToken cancellationToken)
    {
        var materialCount = await dbContext.MaterialUnits
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var riskCount = await dbContext.RiskScores
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var qualityCount = await dbContext.QualityEvents
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var recommendations = new[]
        {
            new SuggestionDto(
                "review-high-risk-materials",
                "Review high-risk materials first",
                "Risk scoring shows active evidence. Start with the highest-risk material and validate process/quality context before taking action.",
                "Risk",
                0.94,
                "/risk"),
            new SuggestionDto(
                "open-data-quality-findings",
                "Check data-quality findings before conclusions",
                "Investigation confidence depends on schema, mapping and source freshness. Review critical/high data-quality findings before interpreting contributors.",
                "DataQuality",
                0.88,
                "/data-quality"),
            new SuggestionDto(
                "run-correlation-followup",
                "Run a process-to-quality correlation follow-up",
                "Use correlation as directional evidence only. Engineering validation is still required before process changes.",
                "Correlation",
                0.82,
                "/correlations"),
            new SuggestionDto(
                "open-ml-readiness",
                "Review ML readiness gates",
                "No production prediction should be claimed until label, feature, sample and governance gates are passing.",
                "MLReadiness",
                0.76,
                "/ml-readiness")
        };

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            context = context ?? "current-investigation",
            materialUnitId,
            evidence = new
            {
                materialCount,
                riskCount,
                qualityCount
            },
            recommendations
        });
    }

    private static Task<IResult> GetPageAsync(string slug)
    {
        var normalized = string.IsNullOrWhiteSpace(slug)
            ? "missing"
            : slug.Trim().ToLowerInvariant();

        var known = new Dictionary<string, DynamicPageDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["executive-quality-review"] = new DynamicPageDto(
                "executive-quality-review",
                "Executive Quality Review",
                "Configurable quality-intelligence page for leadership review.",
                new[]
                {
                    new DynamicPageSectionDto("summary", "Quality intelligence summary", "Dashboard, risk, data-quality and ML readiness evidence in one review page."),
                    new DynamicPageSectionDto("actions", "Recommended actions", "Open ranked suggestions and validate evidence before process changes.")
                }),
            ["plant-engineer-daily"] = new DynamicPageDto(
                "plant-engineer-daily",
                "Plant Engineer Daily",
                "Daily generic manufacturing quality cockpit.",
                new[]
                {
                    new DynamicPageSectionDto("risk", "Risk watchlist", "Prioritize materials with high quality risk."),
                    new DynamicPageSectionDto("dq", "Data quality", "Confirm source freshness, mapping and drift before investigation.")
                })
        };

        if (!known.TryGetValue(normalized, out var page))
        {
            return Task.FromResult<IResult>(Results.NotFound(new
            {
                slug = normalized,
                message = "Dynamic page was not found.",
                statusCode = 404
            }));
        }

        return Task.FromResult<IResult>(Results.Ok(page));
    }

    private sealed record SuggestionDto(
        string Id,
        string Title,
        string Reasoning,
        string Category,
        double Score,
        string TargetRoute);

    private sealed record DynamicPageDto(
        string Slug,
        string Title,
        string Description,
        IReadOnlyList<DynamicPageSectionDto> Sections);

    private sealed record DynamicPageSectionDto(
        string Code,
        string Title,
        string Body);
}
