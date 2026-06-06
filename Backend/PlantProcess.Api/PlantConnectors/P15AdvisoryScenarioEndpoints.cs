using PlantProcess.Application.Advisory;

namespace PlantProcess.Api.PlantConnectors;

public static class P15AdvisoryScenarioEndpoints
{
    public static IEndpointRouteBuilder MapP15AdvisoryScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p15/advisory/scenarios")
            .WithTags("P15 Advisory Scenario Simulation");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            marker = "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE",
            phase = "P15",
            task = "T-096",
            mode = "deterministic-what-if-projection",
            projectionOnly = true,
            automaticWriteBack = false,
            outOfEnvelopeAbstain = true
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE",
            contract = "Deterministic what-if scenario simulation engine",
            safetyRules = new[]
            {
                P15AdvisoryValueContract.ProjectionOnlyStatement,
                "No automatic process write-back.",
                "Out-of-envelope parameter adjustments return abstain/insufficient support.",
                "Same request and seed must return the same projection.",
                "Weak or missing evidence blocks supported projection."
            },
            routes = new[]
            {
                "GET /api/p15/advisory/scenarios/health",
                "GET /api/p15/advisory/scenarios/contract",
                "GET /api/p15/advisory/scenarios/sample-request",
                "POST /api/p15/advisory/scenarios/simulate",
                "POST /api/p15/advisory/scenarios/simulate-demo"
            }
        }));

        group.MapGet("/sample-request", () =>
        {
            var service = new P15ScenarioSimulationService();
            return Results.Ok(service.BuildDemoRequest());
        });

        group.MapPost("/simulate", (P15ScenarioRequest request) =>
        {
            var service = new P15ScenarioSimulationService();
            var response = service.Simulate(request);
            return Results.Ok(response);
        });

        group.MapPost("/simulate-demo", () =>
        {
            var service = new P15ScenarioSimulationService();
            var request = service.BuildDemoRequest();
            var first = service.Simulate(request);
            var second = service.Simulate(request);

            return Results.Ok(new
            {
                deterministic = first == second,
                request,
                first,
                second
            });
        });

        return app;
    }
}
