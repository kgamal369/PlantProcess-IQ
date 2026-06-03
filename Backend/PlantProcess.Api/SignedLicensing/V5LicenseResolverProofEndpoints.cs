using Microsoft.AspNetCore.Mvc;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

namespace PlantProcess.Api.SignedLicensing;

public sealed record ResolverProofRequest(
    string Feature,
    string? DbTierOverride);

public static class V5LicenseResolverProofEndpoints
{
    public static IEndpointRouteBuilder MapV5LicenseResolverProofEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/licensing/resolver-proof")
            .WithTags("V5 License Resolver Proof");

        group.MapGet("/current", (ILicenseService licenseService) =>
        {
            var status = licenseService.GetStatus();

            return Results.Ok(new
            {
                status.Tier,
                status.Source,
                status.DisplayName,
                status.EffectiveFromUtc,
                status.Limits,
                sourceOfTruth = "ILicenseService",
                expectedImplementation = "VerifiedEd25519LicenseService",
                dbEditableTierIgnored = true
            });
        });

        group.MapPost("/feature-check", (
            [FromBody] ResolverProofRequest request,
            ILicenseService licenseService) =>
        {
            var parsed = Enum.TryParse<LicenseFeature>(request.Feature, ignoreCase: true, out var feature)
                ? feature
                : LicenseFeature.BrandedReportPdf;

            var gate = licenseService.EnsureFeatureEnabled(parsed);
            var status = licenseService.GetStatus();

            return Results.Ok(new
            {
                feature = parsed.ToString(),
                allowed = gate.IsSuccess,
                currentTier = status.Tier,
                source = status.Source,
                ignoredDbTierOverride = request.DbTierOverride,
                dbEditableTierIgnored = true,
                requiredTier = licenseService.GetRequiredTierForFeature(parsed),
                message = gate.IsSuccess
                    ? "Allowed by verified Ed25519 license."
                    : gate.Error?.Message
            });
        });

        group.MapGet("/implementation", (ILicenseService licenseService) =>
        {
            return Results.Ok(new
            {
                serviceType = licenseService.GetType().FullName,
                isVerifiedEd25519Resolver =
                    licenseService.GetType().Name.Contains("VerifiedEd25519", StringComparison.OrdinalIgnoreCase),
                sourceOfTruth = "public.ppiq_v_ed25519_current_entitlements"
            });
        });

        return app;
    }
}