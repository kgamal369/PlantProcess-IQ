// =====================================================================================
// PPIQ-T05 - CI truth gate. The 06-10 Jenkinsfile shipped zero blocking test stages and
// catchError(SUCCESS) around e2e; the suite sat 39-red for days behind green deploys.
// This test makes that regression impossible: it runs INSIDE `dotnet test`, so the
// pipeline that executes it polices its own definition.
// =====================================================================================
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T05")]
public sealed class CiPipelineTruthGateTests
{
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Jenkinsfile")) &&
                Directory.Exists(Path.Combine(current.FullName, "Backend")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Repo root with a Jenkinsfile could not be found.");
    }

    private static string Jenkinsfile() =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "Jenkinsfile"));

    [Theory]
    [InlineData("dotnet test")]
    [InlineData("npm run test")]
    [InlineData("npm run e2e")]
    public void Pipeline_contains_every_blocking_suite(string gate)
    {
        var jf = Jenkinsfile();
        Assert.True(
            jf.Contains(gate, StringComparison.OrdinalIgnoreCase)
            // e2e may run via the dedicated ci script - accept either form:
            || (gate == "npm run e2e" && jf.Contains("ci-e2e-stack.sh", StringComparison.OrdinalIgnoreCase)),
            $"PPIQ-T05: the Jenkinsfile must invoke '{gate}' as a blocking stage. " +
            "Removing it recreates the 06-10 Jun green-deploys-red-code regression.");
    }

    [Fact]
    public void Pipeline_never_swallows_failures_with_catchError_success()
    {
        var jf = Jenkinsfile();
        Assert.False(
            Regex.IsMatch(jf, @"catchError\s*\(\s*buildResult\s*:\s*'SUCCESS'", RegexOptions.IgnoreCase),
            "PPIQ-T05: catchError(buildResult:'SUCCESS') turns failing suites into green builds - forbidden.");
    }

    [Fact]
    public void Pipeline_never_enumerates_instead_of_executing()
    {
        var jf = Jenkinsfile();
        Assert.False(
            jf.Contains("--list", StringComparison.OrdinalIgnoreCase),
            "PPIQ-T05: '--list' enumerates tests without running them - forbidden in the pipeline.");
    }

    [Fact]
    public void Tests_run_before_migrate_seed_and_deploy()
    {
        var jf = Jenkinsfile();
        int Pos(string s)
        {
            var i = jf.IndexOf(s, StringComparison.OrdinalIgnoreCase);
            Assert.True(i >= 0, $"PPIQ-T05: '{s}' missing from the Jenkinsfile.");
            return i;
        }

        var lastTest   = Math.Max(Pos("dotnet test"), Math.Max(Pos("npm run test"), jf.IndexOf("ci-e2e-stack.sh", StringComparison.OrdinalIgnoreCase)));
        var firstShip  = Math.Min(Pos("migrate-and-seed.sh"), Pos("deploy-canonical.sh"));

        Assert.True(lastTest < firstShip,
            "PPIQ-T05: every test suite must appear BEFORE migrate/seed/deploy - a deploy must be unreachable while any suite is red.");
    }
}