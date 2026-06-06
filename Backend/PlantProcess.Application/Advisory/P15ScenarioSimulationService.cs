namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE
/// Deterministic Phase 15 what-if simulation engine.
///
/// Important honesty boundary:
/// - projection only
/// - no guaranteed saving
/// - no automatic process action
/// - abstain when outside observed envelope
/// - deterministic output for same request/seed
/// </summary>
public sealed class P15ScenarioSimulationService
{
    public P15ScenarioResponse Simulate(P15ScenarioRequest request)
    {
        var policy = P15AdvisoryHonestyPolicy.ValidateScenarioRequest(request);
        var stableSeed = P15AdvisoryHonestyPolicy.BuildStableScenarioSeed(request);

        if (!policy.IsAllowed)
        {
            var status = policy.Violations.Any(item => item.Contains("Out-of-envelope", StringComparison.OrdinalIgnoreCase))
                ? P15SupportStatus.OutOfEnvelope
                : P15SupportStatus.InsufficientSupport;

            return new P15ScenarioResponse
            {
                ScenarioId = BuildScenarioId(request, stableSeed),
                FindingId = request.FindingId,
                SupportStatus = status,
                SupportMessage = policy.Message + " " + string.Join(" ", policy.Violations),
                ProjectionOnlyStatement = P15AdvisoryValueContract.ProjectionOnlyStatement,
                Seed = stableSeed,
                ProjectedValueImpact = null,
                ProjectionPoints = Array.Empty<P15ScenarioProjectionPoint>(),
                Evidence = request.Evidence
            };
        }

        var evidenceDecision = ValidateEvidence(request);
        if (!evidenceDecision.IsAllowed)
        {
            return new P15ScenarioResponse
            {
                ScenarioId = BuildScenarioId(request, stableSeed),
                FindingId = request.FindingId,
                SupportStatus = P15SupportStatus.InsufficientSupport,
                SupportMessage = evidenceDecision.Message + " " + string.Join(" ", evidenceDecision.Violations),
                ProjectionOnlyStatement = P15AdvisoryValueContract.ProjectionOnlyStatement,
                Seed = stableSeed,
                ProjectedValueImpact = null,
                ProjectionPoints = Array.Empty<P15ScenarioProjectionPoint>(),
                Evidence = request.Evidence
            };
        }

        var random = new Random(stableSeed);
        var confidence = Clamp01(request.Evidence.Length == 0 ? 0m : request.Evidence.Max(item => item.Confidence));
        var strengthMultiplier = request.Evidence.Max(item => item.Strength) switch
        {
            P15EvidenceStrength.Strong => 1.0m,
            P15EvidenceStrength.Moderate => 0.72m,
            _ => 0.0m
        };

        var adjustmentEffect = request.Adjustments.Sum(adjustment => CalculateAdjustmentEffect(adjustment, random));
        var expectedEuroImpact = RoundMoney(Math.Abs(adjustmentEffect) * 10000m * confidence * strengthMultiplier);

        var valueImpact = new P15MoneyRange
        {
            CurrencyCode = "EUR",
            MinValue = RoundMoney(expectedEuroImpact * 0.55m),
            ExpectedValue = expectedEuroImpact,
            MaxValue = RoundMoney(expectedEuroImpact * 1.35m)
        };

        var projectionPoints = request.Adjustments
            .Select((adjustment, index) => BuildProjectionPoint(adjustment, index, stableSeed))
            .ToArray();

        return new P15ScenarioResponse
        {
            ScenarioId = BuildScenarioId(request, stableSeed),
            FindingId = request.FindingId,
            SupportStatus = P15SupportStatus.Supported,
            SupportMessage = "Supported deterministic projection generated inside observed data envelope.",
            ProjectionOnlyStatement = P15AdvisoryValueContract.ProjectionOnlyStatement,
            Seed = stableSeed,
            ProjectedValueImpact = valueImpact,
            ProjectionPoints = projectionPoints,
            Evidence = request.Evidence
        };
    }

    public P15ScenarioRequest BuildDemoRequest() =>
        new()
        {
            TenantId = "demo-tenant",
            PlantId = "demo-plant-01",
            FindingId = "finding-temperature-energy-risk",
            ScenarioName = "Reduce furnace temperature target within observed envelope",
            Seed = P15AdvisoryValueContract.DefaultScenarioSeed,
            Adjustments = new[]
            {
                new P15ParameterAdjustment
                {
                    ParameterCode = "furnace_temperature_target",
                    DisplayName = "Furnace temperature target",
                    CurrentValue = 742m,
                    ProposedValue = 735m,
                    MinimumObservedValue = 720m,
                    MaximumObservedValue = 760m,
                    Unit = "degC"
                },
                new P15ParameterAdjustment
                {
                    ParameterCode = "line_speed_target",
                    DisplayName = "Line speed target",
                    CurrentValue = 1.00m,
                    ProposedValue = 1.04m,
                    MinimumObservedValue = 0.88m,
                    MaximumObservedValue = 1.12m,
                    Unit = "m/s"
                }
            },
            Evidence = new[]
            {
                new P15EvidenceReference
                {
                    EvidenceId = "ev-demo-correlation-001",
                    EvidenceType = "association-finding",
                    SourceSystem = "Analytics.Core",
                    Description = "Temperature and line-speed association with energy and quality-risk KPI under comparable operating windows.",
                    Confidence = 0.82m,
                    Strength = P15EvidenceStrength.Moderate,
                    Provenance = new[] { "golden-demo-data", "analytics-core-correlation", "phase15-demo" }
                }
            }
        };

    private static P15PolicyDecision ValidateEvidence(P15ScenarioRequest request)
    {
        if (request.Evidence.Length == 0)
        {
            return P15PolicyDecision.Block(
                "Scenario projection requires evidence before support can be claimed.",
                new[] { "Missing evidence reference." });
        }

        var strongest = request.Evidence.Max(item => item.Strength);
        if (strongest is P15EvidenceStrength.None or P15EvidenceStrength.Weak)
        {
            return P15PolicyDecision.Block(
                "Scenario projection abstains because evidence is weak or missing.",
                new[] { "Weak evidence cannot support what-if projection." });
        }

        return P15PolicyDecision.Allow("Evidence is sufficient for guarded projection.");
    }

    private static decimal CalculateAdjustmentEffect(P15ParameterAdjustment adjustment, Random random)
    {
        var range = Math.Max(1m, adjustment.MaximumObservedValue - adjustment.MinimumObservedValue);
        var normalizedMove = (adjustment.ProposedValue - adjustment.CurrentValue) / range;
        var boundedMove = Math.Max(-1m, Math.Min(1m, normalizedMove));
        var deterministicJitter = ((decimal)random.NextDouble() - 0.5m) * 0.025m;
        return boundedMove + deterministicJitter;
    }

    private static P15ScenarioProjectionPoint BuildProjectionPoint(P15ParameterAdjustment adjustment, int index, int seed)
    {
        var local = new Random(seed + index + 31);
        var range = Math.Max(1m, adjustment.MaximumObservedValue - adjustment.MinimumObservedValue);
        var normalizedMove = (adjustment.ProposedValue - adjustment.CurrentValue) / range;
        var baseline = 100m + (index * 7m);
        var directionalDelta = RoundMetric(normalizedMove * 12m + (((decimal)local.NextDouble() - 0.5m) * 1.5m));
        var projected = RoundMetric(baseline - directionalDelta);

        return new P15ScenarioProjectionPoint
        {
            MetricCode = $"projected_kpi_{index + 1:00}",
            Label = $"Projected KPI impact for {adjustment.DisplayName}",
            BaselineValue = baseline,
            ProjectedValue = projected,
            Delta = RoundMetric(projected - baseline),
            Unit = "index"
        };
    }

    private static string BuildScenarioId(P15ScenarioRequest request, int seed) =>
        $"p15-scenario-{Sanitize(request.FindingId)}-{seed}";

    private static string Sanitize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');

    private static decimal Clamp01(decimal value) => Math.Max(0m, Math.Min(1m, value));
    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundMetric(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
