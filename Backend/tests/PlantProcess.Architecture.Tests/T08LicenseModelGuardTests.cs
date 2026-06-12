// PPIQ-T08 - ONE license tier model, forever.
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T08")]
public sealed class T08LicenseModelGuardTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "Backend"))) d = d.Parent!;
        return d!.FullName;
    }

    private static IEnumerable<string> BackendSources() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "Backend"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void Exactly_one_license_tier_enum_exists()
    {
        var declarations = BackendSources()
            .SelectMany(p => Regex.Matches(File.ReadAllText(p), @"public enum (\w*LicenseTier\w*)")
                .Select(m => $"{m.Groups[1].Value} in {p}"))
            .ToList();

        Assert.True(declarations.Count == 1 && declarations[0].StartsWith("LicenseTier "),
            "PPIQ-T08: exactly ONE license tier enum (LicenseTier) may exist. Found: "
            + string.Join("; ", declarations));
    }

    [Fact]
    public void No_runtime_tombstone_files_remain()
    {
        var tombstones = Directory.EnumerateFiles(
                Path.Combine(RepoRoot(), "Backend"), "*.runtime.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.True(tombstones.Count == 0,
            "PPIQ-T08: .runtime.cs tombstones must stay deleted: " + string.Join("; ", tombstones));
    }

    [Fact]
    public void CommercialTier_stays_documented_as_rbac_packaging_not_licensing()
    {
        var rbac = BackendSources().Single(p => p.EndsWith("FormalRoleAccessMatrix.cs"));
        Assert.Contains("PPIQ-T08: NOT a license tier", File.ReadAllText(rbac));
    }
}