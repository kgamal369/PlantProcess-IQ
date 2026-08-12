using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045 PACK E. ONE DERIVATION RULE ACROSS THE FOUR AUTHORITATIVE WRITERS.
///
/// Measured 12-Aug: only Rebuild-PresentationDb applied the FDT_C preference.
/// The other three ordered by observation count with an ascending tie-break
/// only - and eleven parameters are tied at 17,010 observations in this
/// dataset, so the four writers resolved to DIFFERENT presentation parameters.
/// The same-UUID convergence invariant could not catch it: the widget ids and
/// codes agreed, and only the bound parameter differed.
///
/// The preference now lives inside the single ORDER BY that all four share.
/// </summary>
public sealed class PresentationParameterConvergenceTests
{
    private static readonly string[] ActiveSeeders =
    {
        "Rebuild-PresentationDb.ps1",
        "Seed-PresentationDashboards.v2.ps1",
        "Insert-Widgets-v4.ps1",
        "Finish-PresentationWorkspace.ps1"
    };

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

    [Fact]
    public void Every_active_seeder_derives_the_presentation_parameter_by_the_same_rule()
    {
        var root = RepositoryRoot();
        var rule = new Regex(
            @"ORDER BY \(pd\.parameter_code = 'FDT_C'\) DESC, COUNT\(\*\) DESC, pd\.parameter_code ASC",
            RegexOptions.None);

        var scanned = 0;
        var offenders = new List<string>();

        foreach (var name in ActiveSeeders)
        {
            var path = Path.Combine(root, "scripts", "demo", name);
            Assert.True(File.Exists(path), "an active seeder is missing: " + path);
            scanned++;

            if (!rule.IsMatch(File.ReadAllText(path)))
                offenders.Add(name);
        }

        Assert.Equal(ActiveSeeders.Length, scanned);
        Assert.True(
            offenders.Count == 0,
            "these seeders do not carry the single presentation-parameter rule: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The invented code must never return. It was produced by a fallback that
    /// ran before observations existed, and it survived in the live database for
    /// weeks after the seeders stopped writing it.
    /// </summary>
    [Fact]
    public void No_seeder_carries_an_invented_parameter_fallback()
    {
        var root = RepositoryRoot();
        var invented = "rolling" + "." + "cooling_rate";
        var scanned = 0;
        var offenders = new List<string>();

        foreach (var name in ActiveSeeders)
        {
            var text = File.ReadAllText(Path.Combine(root, "scripts", "demo", name));
            scanned++;
            if (text.Contains(invented, StringComparison.OrdinalIgnoreCase))
                offenders.Add(name);
        }

        Assert.Equal(ActiveSeeders.Length, scanned);
        Assert.True(offenders.Count == 0, "invented parameter fallback present in: " + string.Join(", ", offenders));
    }
}