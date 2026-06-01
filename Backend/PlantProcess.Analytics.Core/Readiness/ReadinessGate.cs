using System.Collections.Generic;
using System.Linq;
namespace PlantProcess.Analytics.Core.Readiness;
public enum ReadinessState { Ready, Partial, Blocked }
/// <summary>v4 7.3 demo defaults; per-tenant configurable.</summary>
public sealed record ReadinessThresholds(
int HeatsReady = 60, int HeatsPartial = 30,
int EventsReady = 40, int EventsPartial = 15,
double MinorityReady = 0.10, double MinorityPartial = 0.03,
double FreshnessReadyFactor = 1.0, double FreshnessPartialFactor = 2.0,
double CompletenessReady = 0.95, double CompletenessPartial = 0.85);
public sealed record ReadinessInput(
int IndependentHeats,
int OutcomeEvents,
double MinorityClassFraction,
double FreshnessFactor,
double RequiredFieldCompleteness);
public sealed record ReadinessDimension(string Name, ReadinessState State, string Reason);
public sealed record ReadinessReport(ReadinessState Overall, IReadOnlyList<ReadinessDimension> Dimensions)
{
public bool CanRun => Overall != ReadinessState.Blocked;
}
public static class ReadinessGate
{
public static ReadinessReport Evaluate(ReadinessInput input, ReadinessThresholds? thresholds = null)
{
var t = thresholds ?? new ReadinessThresholds();
var dims = new List<ReadinessDimension>
{
HighGood("Independent heats", input.IndependentHeats, t.HeatsReady, t.HeatsPartial),
HighGood("Outcome events", input.OutcomeEvents, t.EventsReady, t.EventsPartial),
HighGoodFraction("Minority-class balance", input.MinorityClassFraction, t.MinorityReady, t.MinorityPartial),
LowGood("Freshness factor (age/cadence)", input.FreshnessFactor, t.FreshnessReadyFactor, t.FreshnessPartialFactor),
HighGoodFraction("Required-field completeness", input.RequiredFieldCompleteness, t.CompletenessReady, t.CompletenessPartial)
};
var overall = dims.Select(d => d.State).Aggregate(ReadinessState.Ready, Worst);
return new ReadinessReport(overall, dims);
}
private static ReadinessState Worst(ReadinessState a, ReadinessState b) => (ReadinessState)Math.Max((int)a, (int)b);

private static ReadinessDimension HighGood(string name, int value, int ready, int partial)
{
    if (value >= ready) return new(name, ReadinessState.Ready, $"{value} >= {ready} (Ready).");
    if (value >= partial) return new(name, ReadinessState.Partial, $"{value} in [{partial},{ready}) (Partial).");
    return new(name, ReadinessState.Blocked, $"{value} < {partial} (Blocked).");
}

private static ReadinessDimension HighGoodFraction(string name, double value, double ready, double partial)
{
    if (value >= ready) return new(name, ReadinessState.Ready, $"{value:P1} >= {ready:P1} (Ready).");
    if (value >= partial) return new(name, ReadinessState.Partial, $"{value:P1} in [{partial:P1},{ready:P1}) (Partial).");
    return new(name, ReadinessState.Blocked, $"{value:P1} < {partial:P1} (Blocked).");
}

private static ReadinessDimension LowGood(string name, double value, double ready, double partial)
{
    if (value <= ready) return new(name, ReadinessState.Ready, $"{value:F2} <= {ready:F2} (Ready).");
    if (value <= partial) return new(name, ReadinessState.Partial, $"{value:F2} in ({ready:F2},{partial:F2}] (Partial).");
    return new(name, ReadinessState.Blocked, $"{value:F2} > {partial:F2} (Blocked).");
}
}