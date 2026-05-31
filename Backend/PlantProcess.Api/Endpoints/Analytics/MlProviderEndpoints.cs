
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class MlProviderEndpoints
{
    public static IEndpointRouteBuilder MapMlProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ml/providers")
            .WithTags("ML Providers")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/narrative/proof", GetNarrativeProofAsync)
            .WithSummary("Prove narrative provider abstraction and safe/offline behavior");

        return app;
    }

    private static async Task<IResult> GetNarrativeProofAsync(
        INarrativeProvider provider,
        CancellationToken cancellationToken)
    {
        var result = await provider.GenerateAsync(
            new NarrativeRequest(
                Purpose: "phase45-provider-proof",
                Audience: "engineering-leadership",
                AllowExternalProvider: false,
                Findings: new[]
                {
                    new NarrativeFindingInput(
                        FindingStatus: "EvidenceForReview",
                        FeatureKey: "thermal.true_superheat_c",
                        OutcomeKey: "quality.defect_rate_per_m2",
                        Method: "pearson",
                        EffectSize: 0.9242,
                        QValue: 0.00013,
                        EffectiveN: 40,
                        Direction: "positive",
                        StrengthBucket: "very_strong",
                        ConfoundingNote: "Heat-clustered effective-n applied; diagnostic association only.",
                        SafeMetadata: new Dictionary<string, string>
                        {
                            ["rawPlantDataIncluded"] = "false",
                            ["materialIdsIncluded"] = "false",
                            ["sourceRowsIncluded"] = "false"
                        })
                }),
            cancellationToken);

        return Results.Ok(new
        {
            provider = result.ProviderKey,
            result.ModelKey,
            result.UsedExternalApi,
            result.RawPlantDataIncluded,
            result.SafetyNotes,
            result.Text
        });
    }
}
