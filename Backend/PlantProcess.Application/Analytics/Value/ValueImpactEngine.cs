
using System.Globalization;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_RANGE.
/// Deterministic bounded euro-value engine:
/// - emits Low / Expected(Mid) / High;
/// - preserves monotonic range ordering even for negative/improvement factors;
/// - abstains instead of fabricating a number when required assumptions are missing;
/// - attaches provenance handle + input JSON to every term.
/// - production stop minutes must be attributable production-stop time, not raw equipment-stop time.
/// </summary>
public sealed class ValueImpactEngine : IValueImpactEngine
{
    public ValueImpactResult Compute(ValueImpactInputs inputs, CostAssumptionSet assumptions)
    {
        var currency = string.IsNullOrWhiteSpace(assumptions.Currency) ? "EUR" : assumptions.Currency.Trim().ToUpperInvariant();

        var defectCostBand = inputs.UseScrapCost ? assumptions.ScrapCostPerTon : assumptions.DowngradeDeltaPerTon;
        var missing = RequiredAssumptionGaps(inputs, assumptions, defectCostBand);

        if (missing.Count > 0)
        {
            return ValueImpactResult.Abstained(
                currency,
                assumptions.Version,
                "Insufficient basis: missing or invalid required assumption(s): " + string.Join(", ", missing) + ".");
        }

        var findingHandle = ProvenanceHandle.Finding(
            inputs.FindingRef,
            inputs.CoilId is null ? null : "coil:" + inputs.CoilId);

        var terms = new[]
        {
            Term(
                "DefectDowngradeOrScrap",
                Json(new Dictionary<string, object?>
                {
                    ["findingRef"] = inputs.FindingRef,
                    ["coilId"] = inputs.CoilId,
                    ["defectCode"] = inputs.DefectCode,
                    ["defectRateDelta"] = inputs.DefectRateDelta,
                    ["monthlyVolumeTons"] = inputs.MonthlyVolumeTons,
                    ["affectedTons"] = inputs.DefectAffectedTons,
                    ["assumption"] = inputs.UseScrapCost ? "scrap_cost_per_ton" : "downgrade_delta_per_ton"
                }),
                inputs.DefectAffectedTons,
                defectCostBand!,
                findingHandle),

            Term(
                "AttributableProductionStop",
                Json(new Dictionary<string, object?>
                {
                    ["findingRef"] = inputs.FindingRef,
                    ["coilId"] = inputs.CoilId,
                    ["productionStopMinutes"] = inputs.ProductionStopMinutes,
                    ["assumption"] = "downtime_cost_per_min"
                }),
                inputs.ProductionStopMinutes,
                assumptions.DowntimeCostPerMin!,
                findingHandle),

            Term(
                "YieldLoss",
                Json(new Dictionary<string, object?>
                {
                    ["findingRef"] = inputs.FindingRef,
                    ["coilId"] = inputs.CoilId,
                    ["yieldLossTons"] = inputs.YieldLossTons,
                    ["assumption"] = "grade_premium_per_ton"
                }),
                inputs.YieldLossTons,
                assumptions.GradePremiumPerTon!,
                findingHandle)
        };

        var result = new ValueImpactResult(
            currency,
            RoundMoney(terms.Sum(x => x.Low)),
            RoundMoney(terms.Sum(x => x.Mid)),
            RoundMoney(terms.Sum(x => x.High)),
            terms,
            assumptions.Version,
            IsAbstained: false,
            AbstainReason: null);

        if (!result.IsMonotonic)
        {
            return ValueImpactResult.Abstained(
                currency,
                assumptions.Version,
                "Internal value-engine guard: computed value range was not monotonic.");
        }

        return result;
    }

    private static IReadOnlyList<string> RequiredAssumptionGaps(
        ValueImpactInputs inputs,
        CostAssumptionSet assumptions,
        CostBand? defectCostBand)
    {
        var missing = new List<string>();

        RequireBand(
            defectCostBand,
            inputs.UseScrapCost ? "scrap_cost_per_ton" : "downgrade_delta_per_ton",
            missing);

        RequireBand(assumptions.DowntimeCostPerMin, "downtime_cost_per_min", missing);
        RequireBand(assumptions.GradePremiumPerTon, "grade_premium_per_ton", missing);

        return missing;
    }

    private static void RequireBand(CostBand? band, string name, List<string> missing)
    {
        if (band is null)
        {
            missing.Add(name);
            return;
        }

        if (!band.IsComplete)
        {
            missing.Add(name + " must satisfy low <= expected <= high");
        }
    }

    private static ValueImpactTerm Term(
        string name,
        string inputsJson,
        decimal factor,
        CostBand band,
        ProvenanceHandle handle)
    {
        var candidates = new[]
        {
            factor * band.Low,
            factor * band.Mid,
            factor * band.High
        }
        .Select(RoundMoney)
        .OrderBy(x => x)
        .ToArray();

        var expected = RoundMoney(factor * band.Mid);

        return new ValueImpactTerm(
            name,
            inputsJson,
            candidates[0],
            expected,
            candidates[2],
            handle);
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Json(IReadOnlyDictionary<string, object?> values)
    {
        static string Scalar(object? value)
        {
            return value switch
            {
                null => "null",
                string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
                bool b => b ? "true" : "false",
                decimal d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => "\"" + value.ToString()?.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            };
        }

        return "{" + string.Join(",", values.Select(x => "\"" + x.Key + "\":" + Scalar(x.Value))) + "}";
    }
}
