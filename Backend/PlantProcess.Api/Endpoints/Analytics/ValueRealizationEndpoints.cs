
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Analytics.Value;
using PlantProcess.Infrastructure.Analytics;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_ENDPOINTS.
/// Records tracked realized value separately from projected value impact.
/// </summary>
public static class ValueRealizationEndpoints
{
    public static IEndpointRouteBuilder MapValueRealizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/value/realization")
            .WithTags("Value / Realization")
            .RequireAuthorization();

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_REALIZATION_T039_VALUE_REALIZATION_ENDPOINTS",
            caveat = ValueRealizationCaveats.AttributionCaveat,
            routes = new[]
            {
                "POST /api/value/realization/calculate",
                "POST /api/value/realization/record",
                "GET /api/value/realization/ledger"
            },
            guardrails = new[]
            {
                "Potential value and realized value are separated.",
                "Ledger recording requires tenant context.",
                "Baseline and actual windows must use same metric and unit.",
                "Correlation is not causation."
            }
        }));

        group.MapPost("/calculate", (ValueRealizationRequest request, [Microsoft.AspNetCore.Mvc.FromServices] IValueRealizationService service) =>
        {
            var result = service.Calculate(request);
            return Results.Ok(result);
        });

        group.MapPost("/record", async (
            ValueRealizationRequest request,
            ClaimsPrincipal user,
            [Microsoft.AspNetCore.Mvc.FromServices] IValueRealizationService service,
            [Microsoft.AspNetCore.Mvc.FromServices] NpgsqlValueRealizationRepository repo,
            CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tenant"] = new[] { "no_tenant" }
                });
            }

            var result = service.Calculate(request);

            if (result.IsAbstained)
            {
                return Results.Ok(new
                {
                    recorded = false,
                    result,
                    reason = result.AbstainReason
                });
            }

            var id = await repo.RecordAsync(tenantId, result, user.Identity?.Name ?? "unknown", ct);

            return Results.Ok(new
            {
                recorded = true,
                id,
                result
            });
        });

        group.MapGet("/ledger", async (
            int? take,
            ClaimsPrincipal user,
            [Microsoft.AspNetCore.Mvc.FromServices] NpgsqlValueRealizationRepository repo,
            CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tenant"] = new[] { "no_tenant" }
                });
            }

            var rows = await repo.ListRecentAsync(tenantId, take ?? 25, ct);

            return Results.Ok(new
            {
                caveat = ValueRealizationCaveats.AttributionCaveat,
                rows
            });
        });

        return app;
    }

    private static bool TryTenant(ClaimsPrincipal user, out Guid tenantId)
        => PlantProcess.Application.Security.Tenancy.TenantClaims.TryResolve(user, out tenantId);
}
