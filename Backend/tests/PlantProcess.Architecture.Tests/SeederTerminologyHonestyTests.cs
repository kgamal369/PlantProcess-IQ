using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045 TERMINOLOGY HONESTY GUARD (Section 6.8).
///
/// MI_SEV binds defectCount. A title calling that output "predicted" relabels
/// measured defect data as model output, which is the exact claim Model
/// Insights exists to refuse. The live database already carried the honest
/// title while all four authoritative seeders carried the false one, so a clean
/// rebuild would have reinstated it. That is why this is a build gate and not a
/// review item.
///
/// The forbidden phrase is ASSEMBLED FROM FRAGMENTS so this guard is not itself
/// the match a repository scan reports.
/// </summary>
public sealed class SeederTerminologyHonestyTests
{
    private static readonly string ForbiddenTitle = "Predi" + "cted Sev" + "erity Mix";

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

    /// <summary>
    /// The four active authoritative presentation writers. Named explicitly:
    /// a wildcard over the folder would silently start passing if a writer were
    /// renamed, and this project has already been bitten by a fifth writer
    /// nobody knew existed.
    /// </summary>
    private static readonly string[] ActiveSeeders =
    {
        "Rebuild-PresentationDb.ps1",
        "Seed-PresentationDashboards.v2.ps1",
        "Insert-Widgets-v4.ps1",
        "Finish-PresentationWorkspace.ps1"
    };

    [Fact]
    public void No_active_seeder_writes_a_prediction_title_for_a_measured_defect_widget()
    {
        var root = RepositoryRoot();
        var scanned = 0;
        var offenders = new List<string>();

        foreach (var name in ActiveSeeders)
        {
            var path = Path.Combine(root, "scripts", "demo", name);
            Assert.True(File.Exists(path), "an active seeder is missing: " + path);

            var text = File.ReadAllText(path);
            scanned++;

            if (text.Contains(ForbiddenTitle, StringComparison.OrdinalIgnoreCase))
                offenders.Add(name);
        }

        // A SCAN THAT MATCHES NOTHING MUST FAIL. A guard that silently reads
        // zero files reports a clean tree forever.
        Assert.Equal(ActiveSeeders.Length, scanned);

        Assert.True(
            offenders.Count == 0,
            "these seeders still label measured defect data as a prediction: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// INVERTED BY 817, AND THE HISTORY MATTERS. This half originally asserted
    /// that every seeder WRITES the converged MI_SEV definition, because
    /// forbidding a false title proves nothing if the widget quietly vanishes.
    /// It then did exactly its job: MI_SEV was measured returning ONE row -
    /// defectCount by materialUnitType is a single category, so the donut drew
    /// one slice at 100 percent - and the pack that retired it failed this test
    /// rather than slipping past.
    ///
    /// The widget is now retired, so the rule is the opposite: no writer may
    /// bring it back. The scanned count is still asserted, because a guard that
    /// silently reads zero files reports a clean tree forever.
    /// </summary>
    [Fact]
    public void No_active_seeder_writes_the_retired_widget()
    {
        var root = RepositoryRoot();
        var retired = "MI" + "_SEV";
        var scanned = 0;
        var offenders = new List<string>();

        foreach (var name in ActiveSeeders)
        {
            var text = File.ReadAllText(Path.Combine(root, "scripts", "demo", name));
            scanned++;
            if (text.Contains("'" + retired + "'", StringComparison.Ordinal))
                offenders.Add(name);
        }

        Assert.Equal(ActiveSeeders.Length, scanned);
        Assert.True(
            offenders.Count == 0,
            "a retired widget was reinstated by: " + string.Join(", ", offenders));
    }
}