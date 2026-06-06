using PlantProcess.Application.Advisory;

namespace PlantProcess.Api.PlantConnectors;

public static class P15RoiCfoDashboardEndpoints
{
    public static IEndpointRouteBuilder MapP15RoiCfoDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p15/roi-cfo-dashboard")
            .WithTags("P15 ROI CFO Dashboard");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            marker = "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD",
            phase = "P15",
            task = "T-099",
            mode = "roi-cfo-value-dashboard",
            separatesPotentialVsRealized = true,
            exportEvidencePack = true,
            attributionCaveatVisible = true
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD",
            contract = "ROI/CFO value dashboard with potential vs realized value separation and exportable evidence pack",
            guardrails = new[]
            {
                "Potential value and realized value are separated.",
                "Realized value reconciles with value-realization ledger.",
                "Payback period is computed from realized value.",
                "Export evidence pack carries ledger IDs, provenance and caveats.",
                "Correlation is not causation."
            },
            routes = new[]
            {
                "GET /api/p15/roi-cfo-dashboard/health",
                "GET /api/p15/roi-cfo-dashboard/contract",
                "GET /api/p15/roi-cfo-dashboard/summary",
                "GET /api/p15/roi-cfo-dashboard/evidence-pack"
            }
        }));

        group.MapGet("/summary", () =>
        {
            var service = new P15RoiCfoDashboardService();
            return Results.Ok(service.BuildDemoDashboard());
        });

        group.MapGet("/evidence-pack", () =>
        {
            var service = new P15RoiCfoDashboardService();
            var dashboard = service.BuildDemoDashboard();
            return Results.Ok(dashboard.EvidencePack);
        });

        return app;
    }
}
