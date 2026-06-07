
using System.Globalization;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_TRACKING_SERVICE.
/// Computes baseline-vs-actual realized value and ROI while preserving attribution caveats.
/// </summary>
public sealed class ValueRealizationService : IValueRealizationService
{
    public ValueRealizationResult Calculate(ValueRealizationRequest request)
    {
        var validation = Validate(request);

        if (validation.Count > 0)
        {
            return ValueRealizationResult.Abstained(request, string.Join("; ", validation));
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "EUR"
            : request.Currency.Trim().ToUpperInvariant();

        var improvementUnits = request.Direction == ValueMetricDirection.LowerIsBetter
            ? request.BaselineWindow.Value - request.ActualWindow.Value
            : request.ActualWindow.Value - request.BaselineWindow.Value;

        var values = new[]
        {
            improvementUnits * request.ValuePerUnit.Low,
            improvementUnits * request.ValuePerUnit.Mid,
            improvementUnits * request.ValuePerUnit.High
        }
        .Select(RoundMoney)
        .OrderBy(x => x)
        .ToArray();

        var realizedMid = RoundMoney(improvementUnits * request.ValuePerUnit.Mid);

        // CaptureRateMid = realized expected value divided by projected/potential expected value.
        decimal? CaptureRateMid = request.PotentialValue.Mid == 0m
            ? null
            : RoundRatio(realizedMid / request.PotentialValue.Mid);

        // RoiMid = realized expected value divided by investment cost.
        decimal? RoiMid = request.InvestmentCost <= 0m
            ? null
            : RoundRatio(realizedMid / request.InvestmentCost);

        var status = realizedMid switch
        {
            > 0m => "PositiveTrackedValue",
            < 0m => "NegativeTrackedValue",
            _ => "NeutralTrackedValue"
        };

        var evidenceJson = Json(new Dictionary<string, object?>
        {
            ["trackingCode"] = request.TrackingCode,
            ["sourceRecommendationId"] = request.SourceRecommendationId,
            ["sourceValueImpactId"] = request.SourceValueImpactId,
            ["metricCode"] = request.BaselineWindow.MetricCode,
            ["direction"] = request.Direction.ToString(),
            ["baselineStartUtc"] = request.BaselineWindow.StartUtc,
            ["baselineEndUtc"] = request.BaselineWindow.EndUtc,
            ["baselineValue"] = request.BaselineWindow.Value,
            ["actualStartUtc"] = request.ActualWindow.StartUtc,
            ["actualEndUtc"] = request.ActualWindow.EndUtc,
            ["actualValue"] = request.ActualWindow.Value,
            ["improvementUnits"] = improvementUnits,
            ["unit"] = request.BaselineWindow.Unit,
            ["valuePerUnitLow"] = request.ValuePerUnit.Low,
            ["valuePerUnitExpected"] = request.ValuePerUnit.Mid,
            ["valuePerUnitHigh"] = request.ValuePerUnit.High,
            ["potentialLow"] = request.PotentialValue.Low,
            ["potentialExpected"] = request.PotentialValue.Mid,
            ["potentialHigh"] = request.PotentialValue.High,
            ["investmentCost"] = request.InvestmentCost,
            ["attributionCaveat"] = ValueRealizationCaveats.AttributionCaveat
        });

        return new ValueRealizationResult(
            request.TrackingCode,
            currency,
            request.SourceRecommendationId,
            request.SourceValueImpactId,
            request.BaselineWindow.MetricCode.Trim(),
            request.BaselineWindow.Value,
            request.ActualWindow.Value,
            improvementUnits,
            values[0],
            realizedMid,
            values[2],
            request.PotentialValue.Low,
            request.PotentialValue.Mid,
            request.PotentialValue.High,
            CaptureRateMid,
            RoiMid,
            status,
            IsAbstained: false,
            AbstainReason: null,
            ValueRealizationCaveats.AttributionCaveat,
            evidenceJson);
    }

    private static IReadOnlyList<string> Validate(ValueRealizationRequest request)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TrackingCode))
        {
            failures.Add("tracking_code_required");
        }

        if (string.IsNullOrWhiteSpace(request.SourceRecommendationId) && request.SourceValueImpactId is null)
        {
            failures.Add("source_recommendation_or_value_impact_link_required");
        }

        if (request.BaselineWindow is null || !request.BaselineWindow.IsValid)
        {
            failures.Add("valid_baseline_window_required");
        }

        if (request.ActualWindow is null || !request.ActualWindow.IsValid)
        {
            failures.Add("valid_actual_window_required");
        }

        if (request.BaselineWindow is not null && request.ActualWindow is not null)
        {
            if (!string.Equals(request.BaselineWindow.MetricCode, request.ActualWindow.MetricCode, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("baseline_and_actual_metric_must_match");
            }

            if (!string.Equals(request.BaselineWindow.Unit, request.ActualWindow.Unit, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("baseline_and_actual_unit_must_match");
            }
        }

        if (!request.ValuePerUnit.IsComplete)
        {
            failures.Add("value_per_unit_band_must_satisfy_low_expected_high");
        }

        if (!request.PotentialValue.IsComplete)
        {
            failures.Add("potential_value_band_must_satisfy_low_expected_high");
        }

        if (request.InvestmentCost < 0m)
        {
            failures.Add("investment_cost_cannot_be_negative");
        }

        return failures;
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRatio(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string Json(IReadOnlyDictionary<string, object?> values)
    {
        static string Scalar(object? value)
        {
            return value switch
            {
                null => "null",
                string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
                Guid g => "\"" + g.ToString("D") + "\"",
                DateTimeOffset d => "\"" + d.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) + "\"",
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
