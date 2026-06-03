namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ-T016: boundary validation for a cost-assumption set before it is persisted. A missing (null) band is
/// allowed - the value engine abstains on it. A PRESENT band must be non-negative and satisfy low &lt;= mid &lt;= high.
/// </summary>
public static class CostAssumptionValidator
{
    public static IReadOnlyList<string> Validate(CostAssumptionSet set)
    {
        var errors = new List<string>();
        if (set is null) { errors.Add("Cost assumption set is required."); return errors; }
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
        if (band is null) return;
        if (band.Low < 0m || band.Mid < 0m || band.High < 0m)
            errors.Add($"{name}: values must be non-negative (low={band.Low}, mid={band.Mid}, high={band.High}).");
        if (!(band.Low <= band.Mid && band.Mid <= band.High))
            errors.Add($"{name}: must satisfy low <= mid <= high (low={band.Low}, mid={band.Mid}, high={band.High}).");
    }
}