namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT
/// Shared Phase 15 contract spine for advisory, what-if, recommendation, value-realization, ROI and benchmarking features.
///
/// Scope guard:
/// - projection-only simulation
/// - no causal claim unless proven by a future certified causal engine
/// - no automatic write-back
/// - confidence/evidence/provenance required
/// - explicit human approval required before downstream action
/// </summary>
public static class P15AdvisoryValueContract
{
    public const string Marker = "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT";
    public const string Phase = "P15";
    public const string Mode = "Prescriptive-Advisory-And-Value-Realization";
    public const string ProjectionOnlyStatement = "Projection only. Not a guaranteed saving and not an automatic process action.";
    public const string AttributionCaveat = "Correlation is not causation. Realized value may be influenced by confounders and operational context.";
    public const int DefaultMinimumBenchmarkCohortSize = 5;
    public const int DefaultScenarioSeed = 15096;
}

public enum P15SupportStatus
{
    Supported = 1,
    InsufficientSupport = 2,
    OutOfEnvelope = 3,
    BlockedByHonestyGuard = 4
}

public enum P15EvidenceStrength
{
    None = 0,
    Weak = 1,
    Moderate = 2,
    Strong = 3
}

public enum P15RecommendationStatus
{
    Draft = 1,
    ApprovalRequired = 2,
    Approved = 3,
    Dismissed = 4,
    Blocked = 5
}

public enum P15ApprovalDecision
{
    None = 0,
    Approve = 1,
    Dismiss = 2
}

public enum P15ValueKind
{
    Potential = 1,
    Realized = 2
}

public enum P15BenchmarkVisibility
{
    Visible = 1,
    SuppressedMinimumCohort = 2,
    SuppressedTenantIsolation = 3
}

public sealed record P15EvidenceReference
{
    public required string EvidenceId { get; init; }
    public required string EvidenceType { get; init; }
    public required string SourceSystem { get; init; }
    public required string Description { get; init; }
    public decimal Confidence { get; init; }
    public P15EvidenceStrength Strength { get; init; }
    public string[] Provenance { get; init; } = Array.Empty<string>();
}

public sealed record P15MoneyRange
{
    public required string CurrencyCode { get; init; }
    public decimal MinValue { get; init; }
    public decimal ExpectedValue { get; init; }
    public decimal MaxValue { get; init; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(CurrencyCode) &&
        MinValue <= ExpectedValue &&
        ExpectedValue <= MaxValue;
}

public sealed record P15ParameterAdjustment
{
    public required string ParameterCode { get; init; }
    public required string DisplayName { get; init; }
    public decimal CurrentValue { get; init; }
    public decimal ProposedValue { get; init; }
    public decimal MinimumObservedValue { get; init; }
    public decimal MaximumObservedValue { get; init; }
    public string? Unit { get; init; }

    public bool IsInsideObservedEnvelope =>
        ProposedValue >= MinimumObservedValue && ProposedValue <= MaximumObservedValue;
}

public sealed record P15ScenarioRequest
{
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public required string FindingId { get; init; }
    public required string ScenarioName { get; init; }
    public int Seed { get; init; } = P15AdvisoryValueContract.DefaultScenarioSeed;
    public P15ParameterAdjustment[] Adjustments { get; init; } = Array.Empty<P15ParameterAdjustment>();
    public P15EvidenceReference[] Evidence { get; init; } = Array.Empty<P15EvidenceReference>();
}

public sealed record P15ScenarioProjectionPoint
{
    public required string MetricCode { get; init; }
    public required string Label { get; init; }
    public decimal BaselineValue { get; init; }
    public decimal ProjectedValue { get; init; }
    public decimal Delta { get; init; }
    public string? Unit { get; init; }
}

public sealed record P15ScenarioResponse
{
    public required string ScenarioId { get; init; }
    public required string FindingId { get; init; }
    public P15SupportStatus SupportStatus { get; init; }
    public required string SupportMessage { get; init; }
    public required string ProjectionOnlyStatement { get; init; }
    public int Seed { get; init; }
    public P15MoneyRange? ProjectedValueImpact { get; init; }
    public P15ScenarioProjectionPoint[] ProjectionPoints { get; init; } = Array.Empty<P15ScenarioProjectionPoint>();
    public P15EvidenceReference[] Evidence { get; init; } = Array.Empty<P15EvidenceReference>();

    public bool IsActionableProjection => SupportStatus == P15SupportStatus.Supported && ProjectedValueImpact?.IsValid == true;
}

public sealed record P15RecommendationParameterWindow
{
    public required string ParameterCode { get; init; }
    public required string DisplayName { get; init; }
    public decimal RecommendedMinimum { get; init; }
    public decimal RecommendedMaximum { get; init; }
    public string? Unit { get; init; }
    public required string Basis { get; init; }
}

public sealed record P15RecommendationCandidate
{
    public required string RecommendationId { get; init; }
    public required string FindingId { get; init; }
    public required string Title { get; init; }
    public required string AdvisoryText { get; init; }
    public P15RecommendationStatus Status { get; init; } = P15RecommendationStatus.ApprovalRequired;
    public P15EvidenceStrength EvidenceStrength { get; init; }
    public decimal Confidence { get; init; }
    public P15MoneyRange? ExpectedImpact { get; init; }
    public P15RecommendationParameterWindow[] ParameterWindows { get; init; } = Array.Empty<P15RecommendationParameterWindow>();
    public P15EvidenceReference[] Evidence { get; init; } = Array.Empty<P15EvidenceReference>();
    public string[] Provenance { get; init; } = Array.Empty<string>();
    public required string HonestyCaveat { get; init; }
    public bool RequiresHumanApproval { get; init; } = true;
    public bool HasWriteBackPath { get; init; } = false;
}

public sealed record P15ApprovalCommand
{
    public required string RecommendationId { get; init; }
    public required string ApproverUserId { get; init; }
    public P15ApprovalDecision Decision { get; init; }
    public required string Comment { get; init; }
    public DateTimeOffset DecidedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record P15ApprovalResult
{
    public required string RecommendationId { get; init; }
    public P15RecommendationStatus Status { get; init; }
    public required string Message { get; init; }
    public required string ApprovalRecordId { get; init; }
    public DateTimeOffset DecidedAtUtc { get; init; }
}

public sealed record P15ValueWindow
{
    public required string WindowId { get; init; }
    public required string MetricCode { get; init; }
    public required string Label { get; init; }
    public DateTimeOffset FromUtc { get; init; }
    public DateTimeOffset ToUtc { get; init; }
    public decimal Value { get; init; }
    public string? Unit { get; init; }
}

public sealed record P15ValueRealizationLedgerEntry
{
    public required string LedgerEntryId { get; init; }
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public required string RecommendationId { get; init; }
    public required string FindingId { get; init; }
    public P15ValueWindow BaselineWindow { get; init; } = default!;
    public P15ValueWindow ActualWindow { get; init; } = default!;
    public P15MoneyRange RealizedValue { get; init; } = default!;
    public required string AttributionCaveat { get; init; }
    public string[] Provenance { get; init; } = Array.Empty<string>();
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record P15RoiSummary
{
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public P15MoneyRange PotentialValue { get; init; } = default!;
    public P15MoneyRange RealizedValue { get; init; } = default!;
    public decimal PaybackPeriodMonths { get; init; }
    public int RecommendationCount { get; init; }
    public int ApprovedRecommendationCount { get; init; }
    public int RealizedLedgerEntryCount { get; init; }
    public required string EvidencePackReference { get; init; }
}

public sealed record P15BenchmarkRequest
{
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public required string MetricCode { get; init; }
    public required string IndustryCode { get; init; }
    public int MinimumCohortSize { get; init; } = P15AdvisoryValueContract.DefaultMinimumBenchmarkCohortSize;
}

public sealed record P15BenchmarkBand
{
    public required string BandCode { get; init; }
    public decimal P10 { get; init; }
    public decimal P25 { get; init; }
    public decimal P50 { get; init; }
    public decimal P75 { get; init; }
    public decimal P90 { get; init; }
    public int CohortSize { get; init; }
    public P15BenchmarkVisibility Visibility { get; init; }
}

public sealed record P15BenchmarkResponse
{
    public required string MetricCode { get; init; }
    public required string IndustryCode { get; init; }
    public P15BenchmarkVisibility Visibility { get; init; }
    public required string Message { get; init; }
    public P15BenchmarkBand? Band { get; init; }
    public string[] PrivacyGuards { get; init; } = Array.Empty<string>();
}
