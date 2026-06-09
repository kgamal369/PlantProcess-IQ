
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.AssistantRuntime;
using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Api.Endpoints.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T045_T046_T047_PHASE8_ASSISTANT_HMI.
/// T-045: suggestion and recommendation HMI API.
/// T-047: assistant configuration from HMI.
/// T-046 runtime ask is mapped by MapAssistantEndpoints and consumed by the Phase 8 assistant page.
/// </summary>
public static class Phase8AssistantRuntimeEndpoints
{
    private static readonly ConcurrentDictionary<Guid, Phase8AssistantConfiguration> ConfigByTenant = new();

    public static IEndpointRouteBuilder MapPhase8AssistantRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/phase8")
            .WithTags("Phase 8 AI Assistant")
            .RequireAuthorization();

        group.MapGet("/suggestions/health", (ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var config = CurrentConfig(tenantId);

            return Results.Ok(new
            {
                status = config.EnableSuggestionWorkflow ? "Ready" : "Blocked",
                component = "phase8-suggestions",
                marker = "PPIQ_REALIZATION_T045_SUGGESTION_RECOMMENDATION_PAGE",
                config.Mode,
                config.GroundingPolicy,
                config.EvidencePolicy,
                config.NoEgress,
                config.RequireHumanApprovalForRecommendations
            });
        });

        group.MapPost("/suggestions/generate", (
            [FromBody] Phase8SuggestionRequest request,
            ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var config = CurrentConfig(tenantId);
            var response = Phase8SuggestionRecommendationEngine.Generate(request, config);

            return Results.Ok(response);
        });

        group.MapPost("/suggestions/decision", (
            [FromBody] Phase8SuggestionDecisionRequest request,
            ClaimsPrincipal user) =>
        {
            var userName = user.Identity?.Name ?? request.DecidedBy;
            var status = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase)
                ? "ApprovedForEngineeringReview"
                : "Dismissed";

            return Results.Ok(new Phase8SuggestionDecisionResponse(
                request.RecommendationId,
                status,
                "Decision recorded for HMI review by " + (string.IsNullOrWhiteSpace(userName) ? "unknown-user" : userName) + ". No automatic write-back was executed.",
                DateTimeOffset.UtcNow));
        });

        group.MapGet("/assistant-config", (ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            return Results.Ok(CurrentConfig(tenantId));
        });

        group.MapPut("/assistant-config", (
            [FromBody] Phase8AssistantConfiguration request,
            ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var updatedBy = user.Identity?.Name ?? "hmi";
            var validation = Phase8AssistantConfigurationValidator.ValidateAndNormalize(request, updatedBy);

            ConfigByTenant[tenantId] = validation.Normalized;

            return Results.Ok(new
            {
                saved = true,
                tenantId,
                validation.IsValid,
                validation.Normalized,
                validation.Findings
            });
        });

        group.MapPost("/assistant-config/reset", (ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var config = Phase8AssistantConfiguration.Default(user.Identity?.Name ?? "hmi");
            ConfigByTenant[tenantId] = config;

            return Results.Ok(new
            {
                saved = true,
                tenantId,
                normalized = config,
                findings = Array.Empty<string>()
            });
        });

        return app;
    }

    private static Phase8AssistantConfiguration CurrentConfig(Guid tenantId)
        => ConfigByTenant.GetOrAdd(tenantId, _ => Phase8AssistantConfiguration.Default("system"));

    private static Guid ResolveTenant(ClaimsPrincipal user)
    {
        return TenantClaims.TryResolve(user, out var tenantId)
            ? tenantId
            : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
