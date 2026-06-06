using PlantProcess.Application.Advisory;

namespace PlantProcess.Api.PlantConnectors;

public static class P15HonestyCertificationEndpoints
{
    public static IEndpointRouteBuilder MapP15HonestyCertificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p15/honesty-certification")
            .WithTags("P15 Honesty Certification");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            marker = "PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION",
            phase = "P15",
            task = "T-101",
            mode = "recommendation-honesty-approval-certification",
            noCausalLanguage = true,
            weakEvidenceBlocked = true,
            approvalRequired = true,
            automaticWriteBackBlocked = true
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION",
            contract = "Adversarial honesty certification for Phase 15 advisory recommendations",
            guardrails = new[]
            {
                "No causal language.",
                "No guaranteed saving claim.",
                "Weak evidence blocks recommendation.",
                "Out-of-envelope scenario abstains.",
                "Approval command must be explicit.",
                "No automatic write-back path."
            },
            routes = new[]
            {
                "GET /api/p15/honesty-certification/health",
                "GET /api/p15/honesty-certification/contract",
                "GET /api/p15/honesty-certification/run"
            }
        }));

        group.MapGet("/run", () =>
        {
            var service = new P15HonestyCertificationService();
            return Results.Ok(service.RunCertification());
        });

        return app;
    }
}
