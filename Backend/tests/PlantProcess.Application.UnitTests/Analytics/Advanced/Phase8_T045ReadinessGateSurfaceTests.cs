
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES.
/// Certifies Ready / Partial / Blocked readiness gate projection for API and HMI.
/// </summary>
public sealed class Phase8_T045ReadinessGateSurfaceTests
{
    /// <summary>
    /// T-045-R1-A. AdvancedReadinessGateProjector reads Name, State and Reason.
    /// The measurement and its bounds are now part of the DTO, and nothing in
    /// this file asserts them, so they are supplied as explicit NOT-UNDER-TEST
    /// zeros rather than as numbers that would read like real thresholds.
    ///
    /// The record deliberately carries NO defaults for those fields. A default
    /// would let a future mapping forget the evidence and silently reproduce
    /// the three-field discard this task removed.
    /// </summary>
    private static ReadinessDimensionDto Dimension(string name, string state, string reason)
        => new(name, state, reason,
            MeasuredValue: 0d, ReadyThreshold: 0d, PartialThreshold: 0d, HigherIsBetter: true);

    [Fact]
    public void T045_Projects_AllReady_Dimensions_To_Ready_State()
    {
        var readiness = new AnalysisReadinessDto(
            Overall: "Ready",
            CanRun: true,
            Dimensions: new[]
            {
                Dimension("Population", "Ready", "Enough independent heats."),
                Dimension("Freshness", "Ready", "Data is fresh."),
                Dimension("Completeness", "Ready", "Required fields are complete.")
            },
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            IndependentHeats: 200,
            OutcomeEvents: 900);

        var surface = AdvancedReadinessGateProjector.Project(readiness);

        Assert.Equal(AdvancedReadinessGateStates.Ready, surface.State);
        Assert.True(surface.CanRun);
        Assert.Equal(3, surface.ReadyCount);
        Assert.Equal(0, surface.PartialCount);
        Assert.Equal(0, surface.BlockedCount);
        Assert.All(surface.Gates, gate => Assert.Equal(AdvancedReadinessGateStates.Ready, gate.State));
        Assert.Contains("Ready", surface.Message);
    }

    [Fact]
    public void T045_Projects_Warning_Dimension_To_Partial_State()
    {
        var readiness = new AnalysisReadinessDto(
            Overall: "Partial",
            CanRun: true,
            Dimensions: new[]
            {
                Dimension("Population", "Ready", "Enough independent heats."),
                Dimension("Freshness", "Warning", "Data is usable but slightly stale.")
            },
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 180,
            IndependentHeats: 75,
            OutcomeEvents: 300);

        var surface = AdvancedReadinessGateProjector.Project(readiness);

        Assert.Equal(AdvancedReadinessGateStates.Partial, surface.State);
        Assert.True(surface.CanRun);
        Assert.Equal(1, surface.ReadyCount);
        Assert.Equal(1, surface.PartialCount);
        Assert.Equal(0, surface.BlockedCount);
        Assert.Contains(surface.Gates, gate => gate.State == AdvancedReadinessGateStates.Partial);
        Assert.Contains("attention", surface.Message);
    }

    [Fact]
    public void T045_Projects_Blocking_Dimension_To_Blocked_State()
    {
        var readiness = new AnalysisReadinessDto(
            Overall: "Blocked",
            CanRun: false,
            Dimensions: new[]
            {
                Dimension("Population", "Blocked", "Not enough independent heats."),
                Dimension("Completeness", "Ready", "Fields are complete.")
            },
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 30,
            IndependentHeats: 4,
            OutcomeEvents: 12);

        var surface = AdvancedReadinessGateProjector.Project(readiness);

        Assert.Equal(AdvancedReadinessGateStates.Blocked, surface.State);
        Assert.False(surface.CanRun);
        Assert.Equal(1, surface.BlockedCount);

        var blocker = Assert.Single(surface.Gates, gate => gate.IsBlocking);
        Assert.Equal("POPULATION", blocker.GateCode);
        Assert.Contains("Not enough", blocker.Reason);
        Assert.Contains("analysis must abstain", surface.Message);
    }

    [Theory]
    [InlineData("Ready", "Ready")]
    [InlineData("Warning", "Partial")]
    [InlineData("Warn", "Partial")]
    [InlineData("Partial", "Partial")]
    [InlineData("Failed", "Blocked")]
    [InlineData("Blocker", "Blocked")]
    [InlineData("Blocked", "Blocked")]
    public void T045_Normalizes_Legacy_And_Canonical_Readiness_States(string input, string expected)
    {
        Assert.Equal(expected, AdvancedReadinessGateProjector.NormalizeState(input));
    }
}
