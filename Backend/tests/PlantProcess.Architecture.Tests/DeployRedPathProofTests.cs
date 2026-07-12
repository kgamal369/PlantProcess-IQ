// =====================================================================================
// DeployRedPathProofTests - extends the CI truth gate: proves EVERY test stage index
// precedes EVERY deploy stage index, the deploy uses --remove-orphans, the orphan purge
// targets the legacy plantprocess-* / ppiq-demo stacks, and a rollback path exists.
// Runs inside dotnet test so the pipeline polices its own definition.
//
// TWO DEFECTS IN THE ORIGINAL VERSION OF THIS FILE, FIXED HERE:
//
//   1. Reads ran over the RAW Jenkinsfile, so its header comment satisfied the stage
//      assertions. All reads now strip comments first.
//
//   2. The rollback assertion was Contains("rollback", Jenkinsfile). The word "rollback"
//      appears in the Jenkinsfile exactly once - inside the stage-8 comment. It proved
//      nothing, and after comment stripping it does not exist at all. The rollback lives
//      in deploy/scripts/deploy-canonical.sh, so the assertion moved there and now tests
//      the four behavioural tokens (tag :previous, docker tag restore, the health-gate
//      failure branch, --remove-orphans) rather than the English word.
// =====================================================================================
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Gate", "DeployRedPath")]
public sealed class DeployRedPathProofTests
{
    private const string BackendSuite = "dotnet" + " test";
    private const string FrontendSuite = "npm" + " run test";
    private const string E2eScript = "ci-e2e" + "-stack.sh";
    private const string SeedScript = "migrate-and" + "-seed.sh";
    private const string DeployScript = "deploy-" + "canonical.sh";

    [Fact]
    public void Every_test_stage_precedes_every_deploy_stage()
    {
        var jf = PipelineSourceText.Read("Jenkinsfile");

        int Pos(string needle) => PipelineSourceText.RequiredIndexOf(
            jf, needle, $"'{needle}' is missing from the executable text of the Jenkinsfile.");

        var lastTest = new[] { Pos(BackendSuite), Pos(FrontendSuite), Pos(E2eScript) }.Max();
        var firstShip = new[] { Pos(SeedScript), Pos(DeployScript) }.Min();

        Assert.True(
            lastTest < firstShip,
            "Every test stage must precede every migrate/deploy stage - a deploy must be unreachable while any suite is red.");
    }

    [Fact]
    public void Deploy_uses_remove_orphans_and_rolls_back()
    {
        var deploy = PipelineSourceText.Read("deploy", "scripts", "deploy-canonical.sh");

        Assert.Contains("--remove-orphans", deploy, StringComparison.Ordinal);

        Assert.Contains(":previous", deploy, StringComparison.Ordinal);
        Assert.Contains("docker tag", deploy, StringComparison.Ordinal);
        Assert.Contains("HEALTH GATE FAILED", deploy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Orphan_purge_targets_legacy_stacks()
    {
        var converge = PipelineSourceText.Read("deploy", "server", "converge-canonical-stack.sh");

        Assert.Contains("--remove-orphans", converge, StringComparison.Ordinal);
        Assert.Contains("plantprocess-", converge, StringComparison.Ordinal);
        Assert.Contains("ppiq-demo", converge, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression guard for the defect this file was rewritten to close: if any of the
    /// stage tokens can be found ONLY in comments, the suite above is asserting on prose.
    /// </summary>
    [Fact]
    public void Stage_tokens_exist_outside_comments()
    {
        var raw = PipelineSourceText.ReadRaw("Jenkinsfile");
        var stripped = PipelineSourceText.StripComments(raw);

        foreach (var token in new[] { BackendSuite, FrontendSuite, E2eScript, SeedScript, DeployScript })
        {
            Assert.True(
                stripped.Contains(token, StringComparison.OrdinalIgnoreCase),
                $"'{token}' appears in the Jenkinsfile only inside a comment. A guard satisfied by prose is not a guard.");
        }
    }
}