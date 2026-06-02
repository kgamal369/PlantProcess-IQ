using System;
using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Readiness;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>P4 defaults. The demo tenant is the Phase-2 fallback until ITenantAccessor lands.</summary>
public static class AdvancedDefaults
{
    public static readonly Guid DemoTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

/// <summary>
/// P4-02 single source of truth: maps a loaded dataset to ReadinessInput and runs the
/// unit-tested ReadinessGate. Both the compute engine and the live readiness endpoint
/// call this, so the live verdict can never drift from the tested thresholds.
/// </summary>
public static class AdvancedReadiness
{
    public static ReadinessReport Evaluate(AdvancedDataset ds) =>
        ReadinessGate.Evaluate(new ReadinessInput(
            ds.IndependentHeats,
            ds.Outcomes.Count,
            MinorityFraction(ds),
            ds.FreshnessFactor,
            ds.RequiredFieldCompleteness));

    public static double MinorityFraction(AdvancedDataset ds)
    {
        if (ds.Outcomes.Count == 0) return 0.0;
        if (ds.OutcomeType == VariableType.Binary)
        {
            var g = ds.Outcomes.GroupBy(o => o.Value >= 0.5 ? 1 : 0).Select(x => x.Count()).ToList();
            return g.Count < 2 ? 0.0 : g.Min() / (double)ds.Outcomes.Count;
        }
        if (ds.OutcomeType == VariableType.Categorical)
        {
            var g = ds.Outcomes.GroupBy(o => o.Category ?? "").Select(x => x.Count()).ToList();
            return g.Count < 2 ? 0.0 : g.Min() / (double)ds.Outcomes.Count;
        }
        return 0.5; // class balance not applicable to a continuous outcome
    }
}
