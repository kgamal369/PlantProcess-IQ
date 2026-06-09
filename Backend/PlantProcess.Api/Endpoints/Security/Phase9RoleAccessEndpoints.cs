using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Security.Rbac;

namespace PlantProcess.Api.Endpoints.Security;

/// <summary>
/// PPIQ_REALIZATION_T051_FORMAL_ROLE_ACCESS_MATRIX.
/// PPIQ_REALIZATION_T052_EXECUTIVE_PERSONA_SCOPE_DASHBOARD.
/// PPIQ_REALIZATION_T053_PERSONA_PAGE_VISIBILITY_ROUTE_GUARDS.
/// </summary>
public static class Phase9RoleAccessEndpoints
{
    public static IEndpointRouteBuilder MapPhase9RoleAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/p09")
            .WithTags("Phase 9 Role Access")
            .RequireAuthorization();

        group.MapGet("/access/matrix", () =>
        {
            return Results.Ok(new
            {
                marker = "PPIQ_REALIZATION_T051_FORMAL_ROLE_ACCESS_MATRIX",
                roles = FormalRoleAccessMatrix.Matrix()
            });
        });

        group.MapGet("/access/effective", (ClaimsPrincipal user) =>
        {
            var role = ResolveRole(user);
            var tier = ResolveTier(user);

            return Results.Ok(new
            {
                role,
                tier,
                persona = FormalRoleAccessMatrix.PersonaFor(role),
                effective = FormalRoleAccessMatrix.Matrix().Single(x => x.Role == role),
                marker = "PPIQ_REALIZATION_T053_PERSONA_PAGE_VISIBILITY_ROUTE_GUARDS"
            });
        });

        group.MapGet("/access/check/{capability}", (string capability, ClaimsPrincipal user) =>
        {
            var role = ResolveRole(user);
            var tier = ResolveTier(user);
            var parsedCapability = FormalRoleAccessMatrix.ParseCapability(capability);
            var decision = FormalRoleAccessMatrix.Resolve(role, tier, parsedCapability);

            return Results.Ok(decision);
        });

        group.MapGet("/access/enforce/{capability}", (string capability, ClaimsPrincipal user) =>
        {
            var role = ResolveRole(user);
            var tier = ResolveTier(user);
            var parsedCapability = FormalRoleAccessMatrix.ParseCapability(capability);
            var decision = FormalRoleAccessMatrix.Resolve(role, tier, parsedCapability);

            return decision.Allowed
                ? Results.Ok(decision)
                : Results.Problem(
                    title: "Access denied by formal role/access matrix",
                    detail: decision.Reason,
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?>
                    {
                        ["role"] = role.ToString(),
                        ["tier"] = tier.ToString(),
                        ["capability"] = parsedCapability.ToString()
                    });
        });

        group.MapGet("/executive/dashboard", (ClaimsPrincipal user) =>
        {
            var role = ResolveRole(user);
            var tier = ResolveTier(user);
            var decision = FormalRoleAccessMatrix.Resolve(role, tier, PlantCapability.ExecutiveDashboardView);

            if (!decision.Allowed)
            {
                return Results.Problem(
                    title: "Executive dashboard not available",
                    detail: decision.Reason,
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(new
            {
                marker = "PPIQ_REALIZATION_T052_EXECUTIVE_PERSONA_SCOPE_DASHBOARD",
                role,
                tier,
                persona = "executive",
                cards = new[]
                {
                    new { code = "value", title = "Value / ROI", value = "EUR 28k-56k", caveat = "Projected value range; not causal attribution." },
                    new { code = "quality", title = "Quality risk", value = "Ready for review", caveat = "Decision summary only; open engineering pages for details." },
                    new { code = "downtime", title = "Downtime", value = "Monitored", caveat = "Summary-level view." },
                    new { code = "recommendations", title = "Recommendations", value = "Human approval required", caveat = "No automatic process write-back." }
                },
                hiddenEngineerPages = new[] { "/admin", "/connectors", "/mapping", "/developer", "/phase8/assistant-config" }
            });
        });

        return app;
    }

    private static FormalPlantRole ResolveRole(ClaimsPrincipal user)
    {
        var roleText =
            user.FindFirst("plant_role")?.Value ??
            user.FindFirst("role")?.Value ??
            user.FindFirst(ClaimTypes.Role)?.Value ??
            user.FindFirst("compatibility_role")?.Value;

        return FormalRoleAccessMatrix.ParseRole(roleText);
    }

    private static CommercialTier ResolveTier(ClaimsPrincipal user)
    {
        var tierText =
            user.FindFirst("license_tier")?.Value ??
            user.FindFirst("tier")?.Value ??
            user.FindFirst("commercial_tier")?.Value;

        return FormalRoleAccessMatrix.ParseTier(tierText);
    }
}