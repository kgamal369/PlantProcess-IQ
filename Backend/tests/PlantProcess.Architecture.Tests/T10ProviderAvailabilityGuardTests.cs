// PPIQ-T10 - IsAvailableNow literals may exist NOWHERE outside ProviderAvailability.
// Three independent arrays disagreed about provider availability before this gate;
// this test makes a fourth copy impossible.
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T10")]
public sealed class T10ProviderAvailabilityGuardTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "Backend"))) d = d.Parent!;
        return d!.FullName;
    }

    [Fact]
    public void No_IsAvailableNow_literals_outside_the_single_source()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "Backend"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                     && !p.EndsWith("ProviderAvailability.cs"))
            .Where(p => Regex.IsMatch(File.ReadAllText(p), @"IsAvailableNow:\s*(true|false)"))
            .Select(p => p.Substring(RepoRoot().Length))
            .ToList();

        Assert.True(offenders.Count == 0,
            "PPIQ-T10: IsAvailableNow literals found outside ProviderAvailability - " +
            "route them through ProviderAvailability.IsAvailableNow(providerType): "
            + string.Join("; ", offenders));
    }
}