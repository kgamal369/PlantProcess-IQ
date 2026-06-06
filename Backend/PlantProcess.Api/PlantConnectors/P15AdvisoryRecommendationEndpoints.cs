using PlantProcess.Application.Advisory;

namespace PlantProcess.Api.PlantConnectors;

public static class P15AdvisoryRecommendationEndpoints
{
    private static readonly Dictionary<string, P15ApprovalResult> ApprovalLedger = new(StringComparer.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapP15AdvisoryRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p15/advisory/recommendations")
            .WithTags("P15 Advisory Recommendations");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            marker = "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT",
            phase = "P15",
            task = "T-097",
            mode = "guarded-recommendation-generator",
            expectedEImpactRange = true,
            confidenceEvidenceProvenance = true,
            humanApprovalRequired = true,
            automaticWriteBack = false
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT",
            contract = "Recommendation generator with expected e-impact, confidence, evidence, provenance and approval workflow",
            guardrails = new[]
            {
                "No causal language.",
                "Expected e-impact is projection-only.",
                "Confidence, evidence and provenance are required.",
                "Weak evidence blocks recommendation.",
                "Human approval is required.",
                "No automatic process write-back."
            },
            routes = new[]
            {
                "GET /api/p15/advisory/recommendations/health",
                "GET /api/p15/advisory/recommendations/contract",
                "GET /api/p15/advisory/recommendations/demo-request",
                "POST /api/p15/advisory/recommendations/generate",
                "POST /api/p15/advisory/recommendations/generate-demo",
                "POST /api/p15/advisory/recommendations/approve",
                "GET /api/p15/advisory/recommendations/approvals"
            }
        }));

        group.MapGet("/demo-request", () =>
        {
            var service = new P15RecommendationService();
            return Results.Ok(service.BuildDemoRequest());
        });

        group.MapPost("/generate", (P15RecommendationGenerationRequest request) =>
        {
            var service = new P15RecommendationService();
            return Results.Ok(service.Generate(request));
        });

        group.MapPost("/generate-demo", () =>
        {
            var service = new P15RecommendationService();
            var request = service.BuildDemoRequest();
            var first = service.Generate(request);
            var second = service.Generate(request);

            return Results.Ok(new
            {
                deterministic = first == second,
                request,
                first,
                second
            });
        });

        group.MapPost("/approve", (P15ApprovalCommand command) =>
        {
            var service = new P15RecommendationService();
            var result = service.Decide(command);
            ApprovalLedger[result.ApprovalRecordId] = result;
            return Results.Ok(result);
        });

        group.MapGet("/approvals", () => Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            count = ApprovalLedger.Count,
            approvals = ApprovalLedger.Values.OrderByDescending(item => item.DecidedAtUtc).ToArray()
        }));

        return app;
    }
}
