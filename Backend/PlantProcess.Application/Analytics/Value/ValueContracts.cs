
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_CONTRACTS.
/// A low/expected/high assumption band. Complete only when Low <= Mid <= High.
/// Mid is intentionally retained as the historical API name; Expected is exposed as an alias.
/// </summary>
public sealed record CostBand(decimal Low, decimal Mid, decimal High)
{
    public decimal Expected => Mid;

    public bool IsComplete => Low <= Mid && Mid <= High;

    public string RangeText => $"{Low:N2} <= {Mid:N2} <= {High:N2}";
}

/// <summary>
/// Versioned, tenant-scoped set of cost assumptions. Nulls mean "missing basis", not zero.
/// </summary>
public sealed record CostAssumptionSet(
    int Version,
    string Currency,
    CostBand? CostPerTon,
    CostBand? DowngradeDeltaPerTon,
    CostBand? ScrapCostPerTon,
    CostBand? DowntimeCostPerMin,
    CostBand? GradePremiumPerTon,
    CostBand? EnergyPricePerMwh)
{
    public DateTimeOffset EffectiveFromUtc { get; init; } = DateTimeOffset.MinValue;

    public string? CreatedBy { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }
}

/// <summary>
/// Measured inputs for one finding. ProductionStopMinutes must be attributable production-stop time,
/// not raw equipment-stop time.
/// </summary>
public sealed record ValueImpactInputs(
    string FindingRef,
    string? CoilId,
    string? DefectCode,
    decimal DefectRateDelta,
    decimal MonthlyVolumeTons,
    decimal ProductionStopMinutes,
    decimal YieldLossTons,
    bool UseScrapCost = false)
{
    public decimal DefectAffectedTons => DefectRateDelta * MonthlyVolumeTons;
}

public sealed record ValueImpactTerm(
    string Name,
    string InputsJson,
    decimal Low,
    decimal Mid,
    decimal High,
    ProvenanceHandle Handle)
{
    public decimal Expected => Mid;

    public bool IsMonotonic => Low <= Mid && Mid <= High;

    public decimal RangeWidth => High - Low;
}

public sealed record ValueImpactResult(
    string Currency,
    decimal Low,
    decimal Mid,
    decimal High,
    IReadOnlyList<ValueImpactTerm> Terms,
    int AssumptionVersion,
    bool IsAbstained,
    string? AbstainReason)
{
    public decimal Expected => Mid;

    public bool IsMonotonic => IsAbstained || (Low <= Mid && Mid <= High && Terms.All(x => x.IsMonotonic));

    public decimal RangeWidth => High - Low;

    public string SupportStatus => IsAbstained ? "Abstained" : "BoundedRange";

    public string HonestyCaveat =>
        IsAbstained
            ? "No value claim emitted because the required assumption basis is incomplete."
            : "Projected value range only; not a guaranteed saving. Every figure is tied to assumptions, inputs, and provenance.";

    public static ValueImpactResult Abstained(string currency, int version, string reason)
        => new(currency, 0m, 0m, 0m, Array.Empty<ValueImpactTerm>(), version, true, reason);
}
