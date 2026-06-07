
namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_CONTRACTS.
/// Value-realization tracking separates projected/potential value from tracked realized value.
/// It is baseline-vs-actual evidence, not automatic causal attribution.
/// </summary>
public enum ValueMetricDirection
{
    LowerIsBetter = 1,
    HigherIsBetter = 2
}

public sealed record ValueRealizationWindow(
    string MetricCode,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    decimal Value,
    string Unit)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(MetricCode) && EndUtc > StartUtc;
}

public sealed record ValueRealizationRequest(
    string TrackingCode,
    string? SourceRecommendationId,
    Guid? SourceValueImpactId,
    ValueRealizationWindow BaselineWindow,
    ValueRealizationWindow ActualWindow,
    ValueMetricDirection Direction,
    CostBand ValuePerUnit,
    CostBand PotentialValue,
    decimal InvestmentCost,
    string Currency = "EUR");

public sealed record ValueRealizationResult(
    string TrackingCode,
    string Currency,
    string? SourceRecommendationId,
    Guid? SourceValueImpactId,
    string MetricCode,
    decimal BaselineValue,
    decimal ActualValue,
    decimal ImprovementUnits,
    decimal RealizedLow,
    decimal RealizedMid,
    decimal RealizedHigh,
    decimal PotentialLow,
    decimal PotentialMid,
    decimal PotentialHigh,
    decimal? CaptureRateMid,
    decimal? RoiMid,
    string Status,
    bool IsAbstained,
    string? AbstainReason,
    string AttributionCaveat,
    string EvidenceJson)
{
    public decimal RealizedExpected => RealizedMid;

    public bool IsMonotonic => IsAbstained || (RealizedLow <= RealizedMid && RealizedMid <= RealizedHigh);

    public static ValueRealizationResult Abstained(
        ValueRealizationRequest request,
        string reason)
    {
        var metric = request.BaselineWindow?.MetricCode ?? request.ActualWindow?.MetricCode ?? "unknown";

        return new ValueRealizationResult(
            request.TrackingCode,
            string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.Trim().ToUpperInvariant(),
            request.SourceRecommendationId,
            request.SourceValueImpactId,
            metric,
            request.BaselineWindow?.Value ?? 0m,
            request.ActualWindow?.Value ?? 0m,
            0m,
            0m,
            0m,
            0m,
            request.PotentialValue?.Low ?? 0m,
            request.PotentialValue?.Mid ?? 0m,
            request.PotentialValue?.High ?? 0m,
            null,
            null,
            "Abstained",
            true,
            reason,
            ValueRealizationCaveats.AttributionCaveat,
            "{}");
    }
}

public sealed record ValueRealizationLedgerEntry(
    Guid Id,
    Guid TenantId,
    string TrackingCode,
    string? SourceRecommendationId,
    Guid? SourceValueImpactId,
    string MetricCode,
    string Currency,
    decimal RealizedLow,
    decimal RealizedMid,
    decimal RealizedHigh,
    decimal? RoiMid,
    string Status,
    string AttributionCaveat,
    DateTimeOffset RecordedAtUtc);

public static class ValueRealizationCaveats
{
    public const string AttributionCaveat =
        "Baseline-vs-actual tracked value is not automatic causal attribution. Correlation is not causation; review operating context before claiming savings.";
}
