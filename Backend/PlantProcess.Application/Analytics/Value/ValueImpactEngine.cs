using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// T-041: deterministic §7.5 impact computation. Produces a RANGE from the assumption bands (never a
/// single guaranteed number), attaches inputs + a provenance handle (T-101) to every term, and ABSTAINS
/// when any required assumption band is missing (T-043).
///
///   Impact = defect_rate_delta * monthly_volume_tons * (downgrade|scrap)_cost_per_ton
///          + production_stop_minutes * downtime_cost_per_min        (production-stop, NOT raw downtime)
///          + yield_loss_tons * grade_premium_per_ton
/// </summary>
public sealed class ValueImpactEngine : IValueImpactEngine
{
    public ValueImpactResult Compute(ValueImpactInputs inputs, CostAssumptionSet a)
    {
        var currency = string.IsNullOrWhiteSpace(a.Currency) ? "EUR" : a.Currency;

        var defectCostBand = inputs.UseScrapCost ? a.ScrapCostPerTon : a.DowngradeDeltaPerTon;
        var missing = new List<string>();
        if (defectCostBand is null || !defectCostBand.IsComplete) missing.Add(inputs.UseScrapCost ? "scrap_cost_per_ton" : "downgrade_delta_per_ton");
        if (a.DowntimeCostPerMin is null || !a.DowntimeCostPerMin.IsComplete) missing.Add("downtime_cost_per_min");
        if (a.GradePremiumPerTon is null || !a.GradePremiumPerTon.IsComplete) missing.Add("grade_premium_per_ton");

        if (missing.Count > 0)
            return ValueImpactResult.Abstained(currency, a.Version,
                $"Insufficient basis: missing required assumption(s): {string.Join(", ", missing)}.");

        var findingHandle = ProvenanceHandle.Finding(inputs.FindingRef, inputs.CoilId is null ? null : $"coil:{inputs.CoilId}");

        var defect = Term("DefectDowngradeOrScrap",
            $"{{\"defectRateDelta\":{inputs.DefectRateDelta},\"monthlyVolumeTons\":{inputs.MonthlyVolumeTons},\"costType\":\"{(inputs.UseScrapCost ? "scrap" : "downgrade")}\"}}",
            inputs.DefectRateDelta * inputs.MonthlyVolumeTons, defectCostBand!, findingHandle);

        var downtime = Term("AttributableDowntime",
            $"{{\"productionStopMinutes\":{inputs.ProductionStopMinutes}}}",
            inputs.ProductionStopMinutes, a.DowntimeCostPerMin!, findingHandle);

        var yieldLoss = Term("YieldLoss",
            $"{{\"yieldLossTons\":{inputs.YieldLossTons}}}",
            inputs.YieldLossTons, a.GradePremiumPerTon!, findingHandle);

        var terms = new[] { defect, downtime, yieldLoss };
        decimal low = 0m, mid = 0m, high = 0m;
        foreach (var t in terms) { low += t.Low; mid += t.Mid; high += t.High; }

        return new ValueImpactResult(currency, Round(low), Round(mid), Round(high), terms, a.Version, false, null);
    }

    private static ValueImpactTerm Term(string name, string inputsJson, decimal quantity, CostBand cost, ProvenanceHandle handle)
        => new(name, inputsJson, Round(quantity * cost.Low), Round(quantity * cost.Mid), Round(quantity * cost.High), handle);

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}