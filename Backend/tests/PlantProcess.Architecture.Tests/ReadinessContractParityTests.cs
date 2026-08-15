using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045-R1-A. THE PARITY MUST SURVIVE EVERY HOP.
///
/// The measurement was not missing from the gate. It was dropped between the
/// gate and the transport, and again between the transport and the widget
/// result. A unit test on the gate alone would have passed throughout the
/// defect, so the chain is asserted as source rather than as behaviour.
/// </summary>
public sealed class ReadinessContractParityTests
{
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
            dir = dir.Parent;

        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }

    private static string CodeOf(params string[] segments)
    {
        var path = Path.Combine(RepositoryRoot(), Path.Combine(segments));
        Assert.True(File.Exists(path), "file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*///?.*$", string.Empty);
    }

    private static readonly string[] Fields =
    {
        "MeasuredValue", "ReadyThreshold", "PartialThreshold", "HigherIsBetter"
    };

    [Fact]
    public void The_gate_dimension_carries_the_measurement_and_both_bounds()
    {
        var code = CodeOf("Backend", "PlantProcess.Analytics.Core", "Readiness", "ReadinessGate.cs");

        foreach (var field in Fields)
            Assert.Contains(field, code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_transport_dto_does_not_narrow_the_answer()
    {
        var code = CodeOf("Backend", "PlantProcess.Application", "Analytics", "Advanced", "AnalysisReadiness.cs");

        foreach (var field in Fields)
            Assert.Contains(field, code, StringComparison.Ordinal);

        // The exact three-field projection that WAS the discard.
        Assert.DoesNotContain(
            "new ReadinessDimensionDto(d.Name, d.State.ToString(), d.Reason)",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_widget_result_publishes_the_measurement_as_named_roles()
    {
        var code = CodeOf(
            "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries",
            "WidgetResultSources.cs");

        foreach (var role in new[] { "measuredValue", "readyThreshold", "partialThreshold", "higherIsBetter" })
            Assert.Contains("\"" + role + "\"", code, StringComparison.Ordinal);
    }
}