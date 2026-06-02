using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>
/// T-036: returns a stored risk score with a RESOLVABLE evidence handle and a stamped model version,
/// and refuses to surface a seed/synthetic-backed score as a live finding.
/// </summary>
public static class RiskEvidenceEndpoints
{
    public static IEndpointRouteBuilder MapRiskEvidenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/risk-scores")
            .WithTags("Analytics Risk Evidence")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/evidence", async (Guid id, IPlantProcessDbContext db, IProvenanceResolver resolver, CancellationToken ct) =>
        {
            var score = await db.RiskScores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (score is null)
                return Results.NotFound(new { message = $"Risk score '{id}' was not found." });

            var handle = ProvenanceHandle.Finding(score.Id.ToString(), "risk-score");
            var resolution = await resolver.ResolveAsync(handle, ct);

            if (!resolution.Resolvable)
                return Results.NotFound(new { message = "Risk score evidence cannot be resolved.", reason = resolution.Reason });

            if (!resolution.IsLive)
                return Results.Ok(new
                {
                    id = score.Id,
                    isLive = false,
                    reason = "Score derives from seed/synthetic data and is not a live finding.",
                    evidenceHandle = new { kind = handle.Kind.ToString(), id = handle.Id }
                });

            return Results.Ok(new
            {
                id = score.Id,
                isLive = true,
                riskType = score.RiskType,
                score = score.Score,
                riskClass = score.RiskClass,
                modelVersion = score.ModelVersion,
                contributors = score.MainContributorsJson,
                explanation = score.ExplanationJson,
                evidenceHandle = new { kind = handle.Kind.ToString(), id = handle.Id, artifact = resolution.ArtifactRef }
            });
        });

        return app;
    }
}