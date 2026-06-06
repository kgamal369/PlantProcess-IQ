using PlantProcess.Application.Advisory;

namespace PlantProcess.Api.PlantConnectors;

public static class P15ValueRealizationEndpoints
{
    private static readonly Dictionary<string, P15ValueRealizationLedgerEntry> Ledger = new(StringComparer.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapP15ValueRealizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p15/value-realization")
            .WithTags("P15 Value Realization");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            marker = "PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING",
            phase = "P15",
            task = "T-098",
            mode = "baseline-vs-actual-value-realization",
            baselineVsActual = true,
            attributionCaveatRequired = true,
            linksToRecommendation = true
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING",
            contract = "Baseline-vs-actual value realization ledger",
            guardrails = new[]
            {
                "Baseline and actual windows must use the same KPI metric.",
                "Realized value must link to a source recommendation.",
                "Attribution caveat must be visible.",
                "Correlation is not causation.",
                "Changing actual value changes realized value."
            },
            routes = new[]
            {
                "GET /api/p15/value-realization/health",
                "GET /api/p15/value-realization/contract",
                "GET /api/p15/value-realization/demo-request",
                "POST /api/p15/value-realization/calculate",
                "POST /api/p15/value-realization/calculate-demo",
                "GET /api/p15/value-realization/ledger"
            }
        }));

        group.MapGet("/demo-request", () =>
        {
            var service = new P15ValueRealizationService();
            return Results.Ok(service.BuildDemoRequest());
        });

        group.MapPost("/calculate", (P15ValueRealizationRequest request) =>
        {
            var service = new P15ValueRealizationService();
            var response = service.Calculate(request);

            if (response.LedgerEntry is not null)
            {
                Ledger[response.LedgerEntry.LedgerEntryId] = response.LedgerEntry;
            }

            return Results.Ok(response);
        });

        group.MapPost("/calculate-demo", () =>
        {
            var service = new P15ValueRealizationService();
            var requestA = service.BuildDemoRequest(actualValue: 91.5m);
            var requestB = service.BuildDemoRequest(actualValue: 94.0m);
            var resultA = service.Calculate(requestA);
            var resultB = service.Calculate(requestB);

            if (resultA.LedgerEntry is not null) Ledger[resultA.LedgerEntry.LedgerEntryId] = resultA.LedgerEntry;
            if (resultB.LedgerEntry is not null) Ledger[resultB.LedgerEntry.LedgerEntryId] = resultB.LedgerEntry;

            return Results.Ok(new
            {
                changingActualValueChangesRealizedValue = resultA.LedgerEntry?.RealizedValue.ExpectedValue != resultB.LedgerEntry?.RealizedValue.ExpectedValue,
                first = resultA,
                second = resultB
            });
        });

        group.MapGet("/ledger", () => Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            count = Ledger.Count,
            entries = Ledger.Values.OrderByDescending(item => item.CreatedAtUtc).ToArray()
        }));

        return app;
    }
}
