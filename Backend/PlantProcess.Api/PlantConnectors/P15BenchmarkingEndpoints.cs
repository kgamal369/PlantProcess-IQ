using PlantProcess.Application.Advisory;

namespace PlantProcess.Api.PlantConnectors;

public static class P15BenchmarkingEndpoints
{
    public static IEndpointRouteBuilder MapP15BenchmarkingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p15/benchmarking")
            .WithTags("P15 Cross-Plant Benchmarking");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            marker = "PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING",
            phase = "P15",
            task = "T-100",
            mode = "privacy-preserving-cross-plant-industry-benchmarking",
            anonymizedAggregateOnly = true,
            minimumCohortEnforced = true,
            noCrossTenantRowExposure = true
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING",
            contract = "Privacy-preserving cross-plant and industry benchmark dashboard",
            guardrails = new[]
            {
                "No identifiable cross-tenant row exposure.",
                "Only anonymized aggregate bands are returned.",
                "Minimum cohort size is enforced.",
                "Below-minimum cohort benchmark is suppressed.",
                "Reference bands are configuration/template driven."
            },
            routes = new[]
            {
                "GET /api/p15/benchmarking/health",
                "GET /api/p15/benchmarking/contract",
                "GET /api/p15/benchmarking/demo-request",
                "GET /api/p15/benchmarking/summary",
                "GET /api/p15/benchmarking/suppressed-demo",
                "POST /api/p15/benchmarking/benchmark"
            }
        }));

        group.MapGet("/demo-request", () =>
        {
            var service = new P15BenchmarkingService();
            return Results.Ok(service.BuildDemoRequest());
        });

        group.MapGet("/summary", () =>
        {
            var service = new P15BenchmarkingService();
            return Results.Ok(service.BuildDemoDashboard());
        });

        group.MapGet("/suppressed-demo", () =>
        {
            var service = new P15BenchmarkingService();
            var request = service.BuildDemoRequest(minimumCohortSize: 8);
            return Results.Ok(service.Benchmark(request, cohortSize: 3));
        });

        group.MapPost("/benchmark", (P15BenchmarkRequest request) =>
        {
            var service = new P15BenchmarkingService();
            return Results.Ok(service.Benchmark(request, cohortSize: Math.Max(request.MinimumCohortSize, 9)));
        });

        return app;
    }
}
