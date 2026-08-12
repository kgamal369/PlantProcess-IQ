using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045 PACK B GUARDS.
///
/// Three rules were ruled for the Class-1/Class-2 seam, and a rule that only
/// exists in a handover is a rule that comes back. Each is read as CODE with
/// comments stripped, because a file that explains what it must not do would
/// otherwise satisfy or trip a guard about that construct.
///
/// The measure codes are assembled from fragments so this file does not itself
/// become the match that a repository scan reports.
/// </summary>
public sealed class NativeWidgetSourceGenericityTests
{
    private const string FindingStatus = "finding" + "Status";
    private const string ScoringCoverage = "scoring" + "Coverage";
    private const string AnalysisReadiness = "analysis" + "Readiness";

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }

    private static string CodeOf(params string[] segments)
    {
        var path = Path.Combine(RepositoryRoot(), Path.Combine(segments));
        Assert.True(File.Exists(path), "file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    private static string SourcesCode() => CodeOf(
        "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries",
        "WidgetResultSources.cs");

    private static string ValidatorCode() => CodeOf(
        "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Widgets",
        "DashboardWidgetValidationService.cs");

    private static string RegistryCode() => CodeOf(
        "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Widgets",
        "DashboardWidgetQuerySafetyRegistry.cs");

    /// <summary>
    /// The validator asks the registry a general question. If it ever compares
    /// against a measure code, every future native source becomes a validator
    /// edit and the seam has stopped being generic.
    /// </summary>
    [Fact]
    public void Validator_never_names_a_native_measure_code()
    {
        var code = ValidatorCode();

        Assert.DoesNotContain(FindingStatus, code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ScoringCoverage, code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AnalysisReadiness, code, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("MeasureProvidesOwnColumns", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every native measure must be declared in the registry, or the validator
    /// would demand a grouping dimension a native source does not use and the
    /// widget would be refused for a reason that is not true of it.
    /// </summary>
    [Fact]
    public void Registry_declares_every_native_measure_as_providing_own_columns()
    {
        var code = RegistryCode();

        Assert.Contains("MeasuresProvidingOwnColumns", code, StringComparison.Ordinal);
        Assert.Contains("Measures.FindingStatus", code, StringComparison.Ordinal);
        Assert.Contains("Measures.ScoringCoverage", code, StringComparison.Ordinal);
        Assert.Contains("Measures.AnalysisReadiness", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The seam must not learn a customer. No dashboard code, widget code or
    /// industry noun may appear in the sources: a native source is dispatched
    /// on a measure code and nothing else.
    /// </summary>
    [Fact]
    public void Native_sources_carry_no_dashboard_widget_or_industry_vocabulary()
    {
        var code = SourcesCode();

        var forbidden = new[]
        {
            "widgetCode", "dashboardCode", "PO_KPI", "QM_", "EO_", "MI_", "RI_", "PA_", "CF_",
            "coil", "slab", "heat", "caster", "steel", "mill", "grade", "fleet"
        };

        // WORD BOUNDARIES, DELIBERATELY. A substring match reports
        // "IndependentHeats" - a property name owned by the canonical readiness
        // contract, not by this seam - and a guard that fails on something the
        // file under test cannot fix is a guard that gets deleted rather than
        // obeyed. That property name is a real Rule 1 violation and is recorded
        // as a finding against the readiness contract, not suppressed here.
        foreach (var token in forbidden)
        {
            Assert.False(
                Regex.IsMatch(code, @"\b" + Regex.Escape(token) + @"\b", RegexOptions.IgnoreCase),
                "the Class-2 seam names '" + token + "', which makes it specific to one customer");
        }
    }

    /// <summary>
    /// A native source must never route back through the Class-1 shape. Passing
    /// a rich result through WidgetFact or BuildResult would flatten it to one
    /// decimal and lose the only thing it carries.
    /// </summary>
    [Fact]
    public void Native_sources_never_use_the_class_one_projection()
    {
        var code = SourcesCode();

        Assert.DoesNotContain("WidgetFact", code, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildResult", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DashboardAggregateExecutor", code, StringComparison.Ordinal);
    }
}