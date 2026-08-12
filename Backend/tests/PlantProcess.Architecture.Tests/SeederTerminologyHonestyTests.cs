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
    /// The positive half. Forbidding the false title proves nothing if the
    /// seeders stopped writing the widget at all.
    /// </summary>
    [Fact]
    public void Every_active_seeder_writes_the_honest_title_and_the_registered_dimension()
    {
        var root = RepositoryRoot();
        var pattern = new Regex(
            @"'MI_SEV'\s+'Defect Mix by Material Type'\s+'donut'\s+'materialUnitType'\s+'defectCount'",
            RegexOptions.IgnoreCase);

        foreach (var name in ActiveSeeders)
        {
            var text = File.ReadAllText(Path.Combine(root, "scripts", "demo", name));
            Assert.True(
                pattern.IsMatch(text),
                name + " does not write the converged MI_SEV definition");
        }
    }
}