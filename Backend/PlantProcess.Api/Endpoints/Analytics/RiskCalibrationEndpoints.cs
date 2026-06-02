using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Analytics.Calibration;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>T-037: surfaces calibration quality (Brier + reliability + abstain) for a risk type.</summary>
public static class RiskCalibrationEndpoints
{
    public static IEndpointRouteBuilder MapRiskCalibrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/risk-calibration")
            .WithTags("Analytics Risk Calibration")
            .RequireAuthorization();

        group.MapGet("/{riskType}", async (string riskType, int? buckets, IRiskCalibrationProvider provider, CancellationToken ct) =>
            Results.Ok(await provider.ComputeForRiskTypeAsync(riskType, buckets ?? 10, ct)));

        return app;
    }
}