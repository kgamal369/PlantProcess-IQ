using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>A low/mid/high assumption band. Complete only when Low &lt;= Mid &lt;= High.</summary>
public sealed record CostBand(decimal Low, decimal Mid, decimal High)
{
    public bool IsComplete => Low <= Mid && Mid <= High;
}

/// <summary>A versioned, tenant-scoped set of cost assumptions (each a band). Nulls mean "missing basis".</summary>
public sealed record CostAssumptionSet(
    int Version,
    string Currency,
    CostBand? CostPerTon,
    CostBand? DowngradeDeltaPerTon,
    CostBand? ScrapCostPerTon,
    CostBand? DowntimeCostPerMin,
    CostBand? GradePremiumPerTon,
    CostBand? EnergyPricePerMwh);

/// <summary>
/// Measured inputs for ONE finding. ProductionStopMinutes is the §5.2 production-stop figure (T-023),
/// never raw equipment-stop minutes.
/// </summary>
public sealed record ValueImpactInputs(
    string FindingRef,
    string? CoilId,
    string? DefectCode,
    decimal DefectRateDelta,
    decimal MonthlyVolumeTons,
    decimal ProductionStopMinutes,
    decimal YieldLossTons,
    bool UseScrapCost = false);

public sealed record ValueImpactTerm(
    string Name,
    string InputsJson,
    decimal Low,
    decimal Mid,
    decimal High,
    ProvenanceHandle Handle);

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
    public static ValueImpactResult Abstained(string currency, int version, string reason)
        => new(currency, 0m, 0m, 0m, Array.Empty<ValueImpactTerm>(), version, true, reason);
}