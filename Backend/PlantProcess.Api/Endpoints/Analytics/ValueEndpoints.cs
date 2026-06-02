using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Analytics.Value;
using PlantProcess.Infrastructure.Analytics;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>T-041/T-042/T-043: cost-assumption management + ranged (or abstaining) value-impact computation.</summary>
public static class ValueEndpoints
{
    public sealed record BandDto(decimal Low, decimal Mid, decimal High);
    public sealed record CostAssumptionDto(
        string? Currency, BandDto? CostPerTon, BandDto? DowngradeDeltaPerTon, BandDto? ScrapCostPerTon,
        BandDto? DowntimeCostPerMin, BandDto? GradePremiumPerTon, BandDto? EnergyPricePerMwh);
    public sealed record ImpactRequest(
        string FindingRef, string? CoilId, string? DefectCode,
        decimal DefectRateDelta, decimal MonthlyVolumeTons, decimal ProductionStopMinutes, decimal YieldLossTons, bool UseScrapCost = false);

    public static IEndpointRouteBuilder MapValueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/value").WithTags("Value / Impact").RequireAuthorization();

        group.MapGet("/cost-assumptions", async (ClaimsPrincipal user, ICostAssumptionStore store, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });
            var set = await store.GetActiveAsync(tenantId, ct);
            return set is null ? Results.NotFound(new { message = "No cost assumptions configured for this tenant." }) : Results.Ok(set);
        });

        group.MapPut("/cost-assumptions", async (CostAssumptionDto dto, ClaimsPrincipal user, ICostAssumptionStore store, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });
            var set = ToSet(dto);
            var version = await store.CreateVersionAsync(tenantId, set, user.Identity?.Name ?? "unknown", ct);
            return Results.Ok(new { version });
        });

        group.MapPost("/impact", async (ImpactRequest req, ClaimsPrincipal user, ICostAssumptionStore store, IValueImpactEngine engine, NpgsqlValueImpactRepository repo, CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId)) return Results.BadRequest(new { error = "no_tenant" });

            var assumptions = await store.GetActiveAsync(tenantId, ct);
            if (assumptions is null)
            {
                var none = ValueImpactResult.Abstained("EUR", 0, "Insufficient basis: no cost assumptions configured for this tenant.");
                await repo.PersistAsync(tenantId, req.FindingRef, req.CoilId, req.DefectCode, none, ct);
                return Results.Ok(none);
            }

            var inputs = new ValueImpactInputs(req.FindingRef, req.CoilId, req.DefectCode,
                req.DefectRateDelta, req.MonthlyVolumeTons, req.ProductionStopMinutes, req.YieldLossTons, req.UseScrapCost);

            var result = engine.Compute(inputs, assumptions);
            await repo.PersistAsync(tenantId, req.FindingRef, req.CoilId, req.DefectCode, result, ct);
            return Results.Ok(result);
        });

        return app;
    }

    private static CostBand? ToBand(BandDto? b) => b is null ? null : new CostBand(b.Low, b.Mid, b.High);

    private static CostAssumptionSet ToSet(CostAssumptionDto d) => new(
        0, string.IsNullOrWhiteSpace(d.Currency) ? "EUR" : d.Currency!,
        ToBand(d.CostPerTon), ToBand(d.DowngradeDeltaPerTon), ToBand(d.ScrapCostPerTon),
        ToBand(d.DowntimeCostPerMin), ToBand(d.GradePremiumPerTon), ToBand(d.EnergyPricePerMwh));

    private static bool TryTenant(ClaimsPrincipal user, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var raw = user.FindFirst("tenant_id")?.Value;
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out tenantId);
    }
}