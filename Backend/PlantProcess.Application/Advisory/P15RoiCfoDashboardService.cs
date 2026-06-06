namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD
/// Buyer/CFO-facing Phase 15 value dashboard.
///
/// Guardrails:
/// - potential value and realized value are separate
/// - ROI and payback reconcile with value ledger
/// - export evidence pack carries provenance and caveats
/// - no causal claim
/// - no fake realized value without ledger
/// </summary>
public sealed class P15RoiCfoDashboardService
{
    public P15RoiCfoDashboardResponse BuildDemoDashboard()
    {
        var recommendationService = new P15RecommendationService();
        var recommendationRequest = recommendationService.BuildDemoRequest();
        var recommendationResponse = recommendationService.Generate(recommendationRequest);
        var recommendation = recommendationResponse.Recommendations.FirstOrDefault();

        var valueService = new P15ValueRealizationService();
        var valueA = valueService.Calculate(valueService.BuildDemoRequest(actualValue: 91.5m));
        var valueB = valueService.Calculate(valueService.BuildDemoRequest(actualValue: 94.0m));
        var ledgerEntries = new[] { valueA.LedgerEntry, valueB.LedgerEntry }
            .Where(item => item is not null)
            .Cast<P15ValueRealizationLedgerEntry>()
            .ToArray();

        var potential = recommendation?.ExpectedImpact ?? new P15MoneyRange
        {
            CurrencyCode = "EUR",
            MinValue = 0m,
            ExpectedValue = 0m,
            MaxValue = 0m
        };

        var realizedExpected = ledgerEntries.Sum(item => item.RealizedValue.ExpectedValue);
        var realized = new P15MoneyRange
        {
            CurrencyCode = potential.CurrencyCode,
            MinValue = Math.Round(ledgerEntries.Sum(item => item.RealizedValue.MinValue), 2, MidpointRounding.AwayFromZero),
            ExpectedValue = Math.Round(realizedExpected, 2, MidpointRounding.AwayFromZero),
            MaxValue = Math.Round(ledgerEntries.Sum(item => item.RealizedValue.MaxValue), 2, MidpointRounding.AwayFromZero)
        };

        var subscriptionCost = 18000m;
        var paybackMonths = realized.ExpectedValue <= 0m
            ? 0m
            : Math.Round(subscriptionCost / realized.ExpectedValue, 2, MidpointRounding.AwayFromZero);

        var summary = new P15RoiSummary
        {
            TenantId = "demo-tenant",
            PlantId = "demo-plant-01",
            PotentialValue = potential,
            RealizedValue = realized,
            PaybackPeriodMonths = paybackMonths,
            RecommendationCount = recommendationResponse.Recommendations.Length,
            ApprovedRecommendationCount = 1,
            RealizedLedgerEntryCount = ledgerEntries.Length,
            EvidencePackReference = "p15-cfo-evidence-pack-demo-001"
        };

        var buckets = BuildBuckets(ledgerEntries, potential);
        var evidencePack = BuildEvidencePack(summary, recommendation, ledgerEntries);

        return new P15RoiCfoDashboardResponse
        {
            Status = "Ready",
            Message = "ROI/CFO dashboard generated from potential recommendation impact and realized value ledger entries.",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Summary = summary,
            Buckets = buckets,
            LedgerEntries = ledgerEntries,
            EvidencePack = evidencePack,
            Caveats = new[]
            {
                P15AdvisoryValueContract.ProjectionOnlyStatement,
                P15AdvisoryValueContract.AttributionCaveat,
                "Potential value is not realized value.",
                "Realized value is calculated only from ledger entries."
            }
        };
    }

    public P15CfoEvidencePack BuildEvidencePack(P15RoiSummary summary, P15RecommendationCandidate? recommendation, P15ValueRealizationLedgerEntry[] ledgerEntries)
    {
        return new P15CfoEvidencePack
        {
            EvidencePackId = summary.EvidencePackReference,
            CurrencyCode = summary.RealizedValue.CurrencyCode,
            PotentialExpectedValue = summary.PotentialValue.ExpectedValue,
            RealizedExpectedValue = summary.RealizedValue.ExpectedValue,
            PaybackPeriodMonths = summary.PaybackPeriodMonths,
            RecommendationIds = recommendation is null ? Array.Empty<string>() : new[] { recommendation.RecommendationId },
            LedgerEntryIds = ledgerEntries.Select(item => item.LedgerEntryId).ToArray(),
            Provenance = ledgerEntries
                .SelectMany(item => item.Provenance)
                .Append("phase15-roi-cfo-dashboard")
                .Append("cfo-evidence-pack")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Caveats = new[]
            {
                P15AdvisoryValueContract.ProjectionOnlyStatement,
                P15AdvisoryValueContract.AttributionCaveat,
                "Export reconciles with the value-realization ledger available at dashboard generation time."
            }
        };
    }

    private static P15RoiValueBucket[] BuildBuckets(P15ValueRealizationLedgerEntry[] ledgerEntries, P15MoneyRange potential)
    {
        var realizedEnergy = ledgerEntries.Sum(item => item.RealizedValue.ExpectedValue);
        var unrealizedPotential = Math.Max(0m, potential.ExpectedValue - realizedEnergy);

        return new[]
        {
            new P15RoiValueBucket
            {
                BucketCode = "potential-total",
                Label = "Potential value",
                ValueKind = P15ValueKind.Potential,
                CurrencyCode = potential.CurrencyCode,
                ExpectedValue = potential.ExpectedValue,
                Source = "Recommendation expected impact range"
            },
            new P15RoiValueBucket
            {
                BucketCode = "realized-ledger",
                Label = "Realized value",
                ValueKind = P15ValueKind.Realized,
                CurrencyCode = potential.CurrencyCode,
                ExpectedValue = realizedEnergy,
                Source = "Value-realization ledger"
            },
            new P15RoiValueBucket
            {
                BucketCode = "unrealized-pipeline",
                Label = "Remaining potential",
                ValueKind = P15ValueKind.Potential,
                CurrencyCode = potential.CurrencyCode,
                ExpectedValue = unrealizedPotential,
                Source = "Potential minus realized"
            }
        };
    }
}

public sealed record P15RoiCfoDashboardResponse
{
    public required string Status { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public required P15RoiSummary Summary { get; init; }
    public P15RoiValueBucket[] Buckets { get; init; } = Array.Empty<P15RoiValueBucket>();
    public P15ValueRealizationLedgerEntry[] LedgerEntries { get; init; } = Array.Empty<P15ValueRealizationLedgerEntry>();
    public required P15CfoEvidencePack EvidencePack { get; init; }
    public string[] Caveats { get; init; } = Array.Empty<string>();
}

public sealed record P15RoiValueBucket
{
    public required string BucketCode { get; init; }
    public required string Label { get; init; }
    public P15ValueKind ValueKind { get; init; }
    public required string CurrencyCode { get; init; }
    public decimal ExpectedValue { get; init; }
    public required string Source { get; init; }
}

public sealed record P15CfoEvidencePack
{
    public required string EvidencePackId { get; init; }
    public required string CurrencyCode { get; init; }
    public decimal PotentialExpectedValue { get; init; }
    public decimal RealizedExpectedValue { get; init; }
    public decimal PaybackPeriodMonths { get; init; }
    public string[] RecommendationIds { get; init; } = Array.Empty<string>();
    public string[] LedgerEntryIds { get; init; } = Array.Empty<string>();
    public string[] Provenance { get; init; } = Array.Empty<string>();
    public string[] Caveats { get; init; } = Array.Empty<string>();
}
