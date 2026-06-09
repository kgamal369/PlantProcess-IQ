using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Licensing.Phase10;

namespace PlantProcess.Api.Endpoints.Licensing;

/// <summary>
/// PPIQ_REALIZATION_T058_LIVE_TIER_TOGGLE_DEMO.
/// PPIQ_REALIZATION_T059_GRACE_EXPIRY_OVERAGE_FLOWS.
/// PPIQ_REALIZATION_T061_OFFLINE_SIGNED_LICENSE_ACTIVATION.
/// </summary>
public static class Phase10LicenseEndpoints
{
    public static IEndpointRouteBuilder MapPhase10LicenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/license/phase10")
            .WithTags("Admin - License Phase 10")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/demo", () =>
        {
            var now = DateTimeOffset.UtcNow;

            return Results.Ok(new
            {
                marker = "PPIQ_REALIZATION_T058_LIVE_TIER_TOGGLE_DEMO",
                generatedAtUtc = now,
                supportedTiers = Enum.GetNames<Phase10LicenseTier>(),
                defaultGraceDays = Phase10LicenseLifecycleEngine.DefaultGraceDays,
                sampleLifecycle = Phase10LicenseLifecycleEngine.Evaluate(
                    new Phase10LicenseLifecycleRequest(
                        Phase10LicenseTier.Pro,
                        now.AddDays(12),
                        now,
                        GraceDays: 30,
                        WarningDays: 30,
                        UsageCount: 3,
                        LicensedLimit: 10))
            });
        });

        group.MapPost("/lifecycle/evaluate", ([FromBody] Phase10LicenseLifecycleRequest request) =>
        {
            return Results.Ok(Phase10LicenseLifecycleEngine.Evaluate(request));
        });

        group.MapPost("/offline-activation/create-demo-envelope", ([FromBody] Phase10CreateDemoEnvelopeRequest request) =>
        {
            var issued = request.IssuedAtUtc ?? DateTimeOffset.UtcNow;
            var expires = request.ExpiresAtUtc ?? issued.AddYears(1);

            var envelope = Phase10OfflineLicenseActivation.CreateSignedDemoEnvelope(
                string.IsNullOrWhiteSpace(request.LicenseId) ? "lic-phase10-demo" : request.LicenseId,
                string.IsNullOrWhiteSpace(request.TenantId) ? "tenant-demo" : request.TenantId,
                request.Tier,
                issued,
                expires);

            return Results.Ok(new
            {
                marker = "PPIQ_REALIZATION_T061_OFFLINE_SIGNED_LICENSE_ACTIVATION",
                envelope
            });
        });

        group.MapPost("/offline-activation/verify", ([FromBody] Phase10OfflineLicenseEnvelope envelope) =>
        {
            var result = Phase10OfflineLicenseActivation.Verify(envelope);

            return result.Accepted
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        return app;
    }
}

public sealed record Phase10CreateDemoEnvelopeRequest(
    string LicenseId,
    string TenantId,
    Phase10LicenseTier Tier,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc);