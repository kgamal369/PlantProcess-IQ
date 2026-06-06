namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING
/// Tracks realized value by comparing a baseline KPI window against an actual KPI window.
///
/// Guardrails:
/// - baseline vs actual must be reproducible
/// - realized value must link to source recommendation
/// - attribution caveat must be explicit
/// - no causal claim
/// - changing actual value changes realized value
/// </summary>
public sealed class P15ValueRealizationService
{
    public P15ValueRealizationResponse Calculate(P15ValueRealizationRequest request)
    {
        var violations = Validate(request);
        if (violations.Length > 0)
        {
            return new P15ValueRealizationResponse
            {
                Status = "Blocked",
                Message = "Value-realization calculation blocked: " + string.Join(" ", violations),
                AttributionCaveat = P15AdvisoryValueContract.AttributionCaveat,
                LedgerEntry = null,
                BaselineVsActualDelta = 0m,
                Violations = violations
            };
        }

        var delta = Math.Round(request.BaselineWindow.Value - request.ActualWindow.Value, 4, MidpointRounding.AwayFromZero);
        var expected = Math.Round(delta * request.EuroPerUnitImprovement, 2, MidpointRounding.AwayFromZero);

        var value = new P15MoneyRange
        {
            CurrencyCode = request.CurrencyCode,
            MinValue = Math.Round(expected * 0.80m, 2, MidpointRounding.AwayFromZero),
            ExpectedValue = expected,
            MaxValue = Math.Round(expected * 1.20m, 2, MidpointRounding.AwayFromZero)
        };

        var ledger = new P15ValueRealizationLedgerEntry
        {
            LedgerEntryId = BuildLedgerEntryId(request),
            TenantId = request.TenantId,
            PlantId = request.PlantId,
            RecommendationId = request.RecommendationId,
            FindingId = request.FindingId,
            BaselineWindow = request.BaselineWindow,
            ActualWindow = request.ActualWindow,
            RealizedValue = value,
            AttributionCaveat = P15AdvisoryValueContract.AttributionCaveat,
            Provenance = request.Provenance
                .Append("phase15-value-realization")
                .Append("baseline-vs-actual")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        return new P15ValueRealizationResponse
        {
            Status = "Calculated",
            Message = "Baseline-vs-actual realized value calculated with explicit attribution caveat.",
            AttributionCaveat = P15AdvisoryValueContract.AttributionCaveat,
            LedgerEntry = ledger,
            BaselineVsActualDelta = delta,
            Violations = Array.Empty<string>()
        };
    }

    public P15ValueRealizationRequest BuildDemoRequest(decimal actualValue = 91.5m)
    {
        return new P15ValueRealizationRequest
        {
            TenantId = "demo-tenant",
            PlantId = "demo-plant-01",
            RecommendationId = "p15-rec-finding-temperature-energy-risk-15096",
            FindingId = "finding-temperature-energy-risk",
            CurrencyCode = "EUR",
            EuroPerUnitImprovement = 1750m,
            BaselineWindow = new P15ValueWindow
            {
                WindowId = "baseline-window-demo-001",
                MetricCode = "energy_intensity_index",
                Label = "Baseline energy intensity index",
                FromUtc = DateTimeOffset.UtcNow.AddDays(-28),
                ToUtc = DateTimeOffset.UtcNow.AddDays(-14),
                Value = 100m,
                Unit = "index"
            },
            ActualWindow = new P15ValueWindow
            {
                WindowId = "actual-window-demo-001",
                MetricCode = "energy_intensity_index",
                Label = "Actual energy intensity index after recommendation review",
                FromUtc = DateTimeOffset.UtcNow.AddDays(-14),
                ToUtc = DateTimeOffset.UtcNow,
                Value = actualValue,
                Unit = "index"
            },
            Provenance = new[]
            {
                "phase15-recommendation-generator",
                "approved-for-review",
                "demo-kpi-window"
            }
        };
    }

    private static string[] Validate(P15ValueRealizationRequest request)
    {
        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TenantId)) violations.Add("TenantId is required.");
        if (string.IsNullOrWhiteSpace(request.PlantId)) violations.Add("PlantId is required.");
        if (string.IsNullOrWhiteSpace(request.RecommendationId)) violations.Add("RecommendationId is required.");
        if (string.IsNullOrWhiteSpace(request.FindingId)) violations.Add("FindingId is required.");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode)) violations.Add("CurrencyCode is required.");
        if (request.EuroPerUnitImprovement <= 0m) violations.Add("EuroPerUnitImprovement must be greater than zero.");

        if (request.BaselineWindow is null) violations.Add("BaselineWindow is required.");
        if (request.ActualWindow is null) violations.Add("ActualWindow is required.");

        if (request.BaselineWindow is not null && request.ActualWindow is not null)
        {
            if (!string.Equals(request.BaselineWindow.MetricCode, request.ActualWindow.MetricCode, StringComparison.OrdinalIgnoreCase))
                violations.Add("Baseline and actual windows must use the same metric code.");

            if (request.BaselineWindow.ToUtc > request.ActualWindow.FromUtc)
                violations.Add("Baseline window must end before actual window starts.");

            if (request.BaselineWindow.FromUtc >= request.BaselineWindow.ToUtc)
                violations.Add("Baseline window range is invalid.");

            if (request.ActualWindow.FromUtc >= request.ActualWindow.ToUtc)
                violations.Add("Actual window range is invalid.");
        }

        return violations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string BuildLedgerEntryId(P15ValueRealizationRequest request)
    {
        var raw = $"{request.TenantId}-{request.PlantId}-{request.RecommendationId}-{request.BaselineWindow.WindowId}-{request.ActualWindow.WindowId}";
        var safe = new string(raw.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        return $"p15-value-ledger-{safe}";
    }
}

public sealed record P15ValueRealizationRequest
{
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public required string RecommendationId { get; init; }
    public required string FindingId { get; init; }
    public required string CurrencyCode { get; init; }
    public decimal EuroPerUnitImprovement { get; init; }
    public required P15ValueWindow BaselineWindow { get; init; }
    public required P15ValueWindow ActualWindow { get; init; }
    public string[] Provenance { get; init; } = Array.Empty<string>();
}

public sealed record P15ValueRealizationResponse
{
    public required string Status { get; init; }
    public required string Message { get; init; }
    public required string AttributionCaveat { get; init; }
    public P15ValueRealizationLedgerEntry? LedgerEntry { get; init; }
    public decimal BaselineVsActualDelta { get; init; }
    public string[] Violations { get; init; } = Array.Empty<string>();
}
