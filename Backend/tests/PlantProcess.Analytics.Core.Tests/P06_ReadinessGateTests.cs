using System.Linq;
using PlantProcess.Analytics.Core.Readiness;
using Xunit;
// T-032: boundary states, Blocked prevents the run, per-tenant overrides, reasons logged.
namespace PlantProcess.Analytics.Core.Tests;
public sealed class P06_ReadinessGateTests
{
private static ReadinessInput Ready() => new(60, 40, 0.10, 1.0, 0.95);
[Fact] public void All_at_ready_thresholds_is_ready()
{
    var r = ReadinessGate.Evaluate(Ready());
    Assert.Equal(ReadinessState.Ready, r.Overall);
    Assert.True(r.CanRun);
}

[Fact] public void Heats_just_below_ready_is_partial()
    => Assert.Equal(ReadinessState.Partial, ReadinessGate.Evaluate(Ready() with { IndependentHeats = 59 }).Overall);

[Fact] public void Heats_below_partial_is_blocked_and_cannot_run()
{
    var r = ReadinessGate.Evaluate(Ready() with { IndependentHeats = 29 });
    Assert.Equal(ReadinessState.Blocked, r.Overall);
    Assert.False(r.CanRun);
}

[Theory]
[InlineData(0, 14, 0.10, 1.0, 0.95)]   // events blocked
[InlineData(60, 40, 0.02, 1.0, 0.95)]  // minority blocked
[InlineData(60, 40, 0.10, 2.5, 0.95)]  // freshness blocked
[InlineData(60, 40, 0.10, 1.0, 0.84)]  // completeness blocked
public void Any_blocked_dimension_blocks_overall(int heats, int events, double minority, double fresh, double complete)
    => Assert.Equal(ReadinessState.Blocked, ReadinessGate.Evaluate(new ReadinessInput(heats, events, minority, fresh, complete)).Overall);

[Fact] public void Thresholds_are_overridable_per_tenant()
{
    var loose = new ReadinessThresholds(HeatsReady: 10, HeatsPartial: 5);
    Assert.Equal(ReadinessState.Ready, ReadinessGate.Evaluate(Ready() with { IndependentHeats = 12 }, loose).Overall);
}

[Fact] public void Every_dimension_has_a_reason()
{
    var r = ReadinessGate.Evaluate(Ready() with { IndependentHeats = 29 });
    Assert.All(r.Dimensions, d => Assert.False(string.IsNullOrWhiteSpace(d.Reason)));
    Assert.Contains(r.Dimensions, d => d.State == ReadinessState.Blocked);
}
}