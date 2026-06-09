using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.AssistantRuntime;

namespace PlantProcess.Api.Endpoints.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP.
/// Suggestion outcome closed-loop API.
/// </summary>
public static class Phase8SuggestionOutcomeLoopEndpoints
{
    private static readonly ConcurrentDictionary<Guid, Phase8SuggestionOutcomeRecord> Outcomes = new();

    public static IEndpointRouteBuilder MapPhase8SuggestionOutcomeLoopEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/phase8")
            .WithTags("Phase 8 Suggestion Outcome Loop")
            .RequireAuthorization();

        group.MapPost("/suggestions/outcomes/actioned", ([FromBody] Phase8SuggestionOutcomeInput request) =>
        {
            var record = Phase8SuggestionOutcomeLoop.RecordOutcome(request);
            Outcomes[record.OutcomeId] = record;

            return Results.Ok(new
            {
                saved = true,
                record.OutcomeId,
                record.RecommendationId,
                record.OutcomeDirection,
                record.Confidence,
                record.CausalClaimMade,
                record.OutcomeCaveat,
                valueLoop = new
                {
                    appearsInValueLoop = true,
                    record.ValueLoopCaveat
                }
            });
        });

        group.MapGet("/suggestions/outcomes", () =>
        {
            return Results.Ok(Outcomes.Values.OrderByDescending(x => x.RecordedAtUtc).ToArray());
        });

        group.MapGet("/suggestions/outcomes/value-loop", () =>
        {
            return Results.Ok(Phase8SuggestionOutcomeLoop.BuildValueLoop(Outcomes.Values));
        });

        return app;
    }
}