// =====================================================================================
// CI truth gate. The 06-10 Jun Jenkinsfile shipped zero blocking test stages and a
// catchError that forced a green build around e2e; the suite sat 39-red for days behind
// green deploys. This test makes that regression impossible: it runs INSIDE `dotnet
// test`, so the pipeline that executes it polices its own definition.
//
// TWO DEFECTS IN THE ORIGINAL VERSION OF THIS FILE, FIXED HERE:
//
//   1. Every assertion ran IndexOf/Contains over the RAW Jenkinsfile. The Jenkinsfile's
//      header comment listed every token asserted on. Deleting stages 3, 4 and 5 left
//      this suite green. All reads now go through PipelineSourceText, which strips
//      comments first.
//
//   2. A stage gated by `when { PPIQ_RUN_E2E == "on" }` is not a blocking stage, and no
//      assertion here could see it. E2e_stage_cannot_be_gated_off closes that hole.
//
// Assertion needles are assembled from fragments so that a repository scanner grepping
// this file for forbidden pipeline tokens does not match the guard that forbids them.
// =====================================================================================
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Gate", "CiPipelineTruth")]
public sealed class CiPipelineTruthGateTests
{
    private const string BackendSuite  = "dotnet" + " test";
    private const string FrontendSuite = "npm" + " run test";
    private const string E2eSuiteAlias = "npm" + " run e2e";
    private const string E2eScript     = "ci-e2e" + "-stack.sh";
    private const string EnumerateFlag = "--" + "list";
    private const string SeedScript    = "migrate-and" + "-seed.sh";
    private const string DeployScript  = "deploy-" + "canonical.sh";

    private static readonly Regex SwallowedFailure =
        new("catch" + @"Error\s*\(\s*buildResult\s*:\s*'SUCCESS'", RegexOptions.IgnoreCase);

    private static readonly Regex WhenClause =
        new(@"\bwhen\b", RegexOptions.IgnoreCase);

    private static string Jenkinsfile() => PipelineSourceText.Read("Jenkinsfile");

    [Theory]
    [InlineData(BackendSuite)]
    [InlineData(FrontendSuite)]
    [InlineData(E2eSuiteAlias)]
    public void Pipeline_contains_every_blocking_suite(string gate)
    {
        var jf = Jenkinsfile();

        var present =
            jf.Contains(gate, StringComparison.OrdinalIgnoreCase) ||
            (gate == E2eSuiteAlias && jf.Contains(E2eScript, StringComparison.OrdinalIgnoreCase));

        Assert.True(
            present,
            $"The Jenkinsfile must invoke '{gate}' as a blocking stage, in executable text and not in a " +
            "comment. Removing it recreates the 06-10 Jun green-deploys-red-code regression.");
    }

    [Fact]
    public void Pipeline_never_swallows_failures_with_catchError_success()
    {
        Assert.False(
            SwallowedFailure.IsMatch(Jenkinsfile()),
            "Forcing a SUCCESS build result around a failing suite turns red suites into green builds - forbidden.");
    }

    [Fact]
    public void Pipeline_never_enumerates_instead_of_executing()
    {
        Assert.False(
            Jenkinsfile().Contains(EnumerateFlag, StringComparison.OrdinalIgnoreCase),
            "Test enumeration lists tests without running them - forbidden in the pipeline.");
    }

    [Fact]
    public void Tests_run_before_migrate_seed_and_deploy()
    {
        var jf = Jenkinsfile();

        int Pos(string needle) => PipelineSourceText.RequiredIndexOf(
            jf, needle, $"'{needle}' is missing from the executable text of the Jenkinsfile.");

        var lastTest = Math.Max(Pos(BackendSuite), Math.Max(Pos(FrontendSuite), Pos(E2eScript)));
        var firstShip = Math.Min(Pos(SeedScript), Pos(DeployScript));

        Assert.True(
            lastTest < firstShip,
            "Every test suite must appear BEFORE migrate/seed/deploy - a deploy must be unreachable while any suite is red.");
    }

    /// <summary>
    /// A `when {}` clause on a test stage makes that stage skippable, which makes the
    /// "blocking" assertion above a statement about text rather than behaviour. The e2e
    /// stage previously carried `when { ... PPIQ_RUN_E2E:-off ... }` and therefore never ran.
    /// </summary>
    [Fact]
    public void E2e_stage_cannot_be_gated_off()
    {
        var jf = Jenkinsfile();

        var e2eIndex = PipelineSourceText.RequiredIndexOf(
            jf, E2eScript, $"The Jenkinsfile must invoke '{E2eScript}'.");

        var stageIndex = jf.LastIndexOf("stage(", e2eIndex, StringComparison.Ordinal);
        Assert.True(stageIndex >= 0, "Could not locate the stage declaration that owns the e2e invocation.");

        var declaration = jf.Substring(stageIndex, e2eIndex - stageIndex);

        Assert.False(
            WhenClause.IsMatch(declaration),
            "The e2e stage carries a when{} clause. A conditionally-skipped stage is not a blocking stage: " +
            "the suite can be switched off and the deploy stages below it stay reachable. Remove the when{} " +
            "clause, or the pipeline is claiming a gate it does not enforce.");
    }
}