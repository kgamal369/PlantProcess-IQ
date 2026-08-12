using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

/// <summary>
/// Enforces the refusal-vocabulary structure mechanically rather than by convention.
///
/// One language, not several: TerminalState and ExclusionAttribution are cross-engine.
/// No God-enum: capability shortfalls never enter the statistical reason set, and
/// statistical reasons never enter a capability set.
///
/// These tests exist so that a later engine cannot quietly reintroduce a second
/// refusal language, which is how "unsupported method" gets collapsed back into
/// "insufficient data".
/// </summary>
public sealed class T171_RefusalVocabularyTests
{
    [Fact]
    public void The_kernel_declares_exactly_one_terminal_state_type()
    {
        var assembly = typeof(TerminalState).Assembly;
        var terminalStateTypes = assembly.GetTypes()
            .Where(t => t.IsEnum && t.Name.Contains("TerminalState", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(terminalStateTypes);
        Assert.Equal(typeof(TerminalState), terminalStateTypes[0]);
    }

    [Fact]
    public void The_kernel_declares_exactly_one_attribution_type()
    {
        var assembly = typeof(ExclusionAttribution).Assembly;
        var attributionTypes = assembly.GetTypes()
            .Where(t => t.IsEnum && t.Name.Contains("Attribution", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(attributionTypes);
        Assert.Equal(typeof(ExclusionAttribution), attributionTypes[0]);
    }

    [Fact]
    public void Terminal_state_carries_the_six_states_named_by_the_frozen_contract()
    {
        var names = Enum.GetNames(typeof(TerminalState)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var expected = new[]
        {
            "ContradictedByControl", "Finding", "InsufficientData",
            "ModelNotReady", "NotApplicable", "RefusedByGuard"
        };

        Assert.Equal(expected, names);
    }

    [Fact]
    public void Attribution_separates_data_from_method_from_declaration()
    {
        var names = Enum.GetNames(typeof(ExclusionAttribution));

        Assert.Contains("Data", names);
        Assert.Contains("Method", names);
        Assert.Contains("Declaration", names);

        // Three distinct causes must never share a value.
        Assert.NotEqual(ExclusionAttribution.Data, ExclusionAttribution.Method);
        Assert.NotEqual(ExclusionAttribution.Method, ExclusionAttribution.Declaration);
        Assert.NotEqual(ExclusionAttribution.Data, ExclusionAttribution.Declaration);
    }

    [Fact]
    public void Statistical_reasons_contain_only_statistical_method_concepts()
    {
        var names = Enum.GetNames(typeof(StatisticalExclusionReason));

        // Capability-profiler concepts must never leak into the statistical reason set.
        foreach (var forbidden in new[]
                 {
                     "Genealogy", "Outcome", "Label", "Capability",
                     "History", "Intervention", "Controllable", "Dimension"
                 })
        {
            Assert.DoesNotContain(names, n => n.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Statistical_reasons_stay_small_enough_to_read()
    {
        // A reason set that grows without bound is a God-enum forming. If this fails,
        // the new value probably belongs to a different engine's code set.
        Assert.True(Enum.GetNames(typeof(StatisticalExclusionReason)).Length <= 8,
            "StatisticalExclusionReason is growing beyond statistical-method concerns.");
    }

    [Fact]
    public void Measured_fact_reports_the_number_behind_a_decision()
    {
        var satisfied = MeasuredFact.AtLeast("population", 120, 30, "units");
        Assert.True(satisfied.Satisfied);
        Assert.Equal(120, satisfied.Observed);
        Assert.Equal(30, satisfied.Required);

        var shortfall = MeasuredFact.AtLeast("population", 12, 30, "units");
        Assert.False(shortfall.Satisfied);
        Assert.Equal(12, shortfall.Observed);
        Assert.Equal(30, shortfall.Required);

        // Informational facts carry no requirement and never read as a failure.
        var info = MeasuredFact.Informational("positions", 5, "count");
        Assert.True(info.Satisfied);
        Assert.True(double.IsNaN(info.Required));
    }

    [Fact]
    public void The_statistical_kernel_still_speaks_the_common_language()
    {
        var result = GroupComparisonKernel.Evaluate(new GroupComparisonInput(
            new[] { new NumericGroup("A", new[] { 1.0, 2.0, 3.0, 4.0 }) }));

        // The refusal uses the shared TerminalState and the shared attribution,
        // with a statistics-specific reason code.
        Assert.Equal(TerminalState.InsufficientData, result.TerminalState);
        Assert.Equal(ExclusionAttribution.Data, result.Attribution);
        Assert.Equal(StatisticalExclusionReason.InsufficientGroups, result.ExclusionReason);
    }
}
