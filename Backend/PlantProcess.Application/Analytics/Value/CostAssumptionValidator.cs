namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ-T016/T017: tenant cost assumptions must be non-negative and ordered low <= mid <= high.
/// Missing bands are allowed because the value engine must abstain instead of fabricating money.
/// </summary>
public static class CostAssumptionValidator
{
    public static IReadOnlyList<string> Validate(CostAssumptionSet? set)
    {
        var errors = new List<string>();

        if (set is null)
        {
            errors.Add("costAssumptionSet: request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(set.Currency))
            errors.Add("currency: currency is required.");

        Check(errors, "costPerTon", set.CostPerTon);
        Check(errors, "downgradeDeltaPerTon", set.DowngradeDeltaPerTon);
        Check(errors, "scrapCostPerTon", set.ScrapCostPerTon);
        Check(errors, "downtimeCostPerMin", set.DowntimeCostPerMin);
        Check(errors, "gradePremiumPerTon", set.GradePremiumPerTon);
        Check(errors, "energyPricePerMwh", set.EnergyPricePerMwh);

        return errors;
    }

    private static void Check(List<string> errors, string name, CostBand? band)
    {
        if (band is null)
            return;

        if (band.Low < 0m || band.Mid < 0m || band.High < 0m)
            errors.Add($"{name}: values must be non-negative (low={band.Low}, mid={band.Mid}, high={band.High}).");

        if (!(band.Low <= band.Mid && band.Mid <= band.High))
            errors.Add($"{name}: must satisfy low <= mid <= high (low={band.Low}, mid={band.Mid}, high={band.High}).");
    }
}