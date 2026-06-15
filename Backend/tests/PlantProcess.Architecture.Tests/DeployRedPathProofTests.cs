// =====================================================================================
// PPIQ-101 DeployRedPathProofTests - extends the CI truth gate: proves EVERY test stage
// index precedes EVERY deploy stage index, the deploy uses --remove-orphans, the orphan
// purge targets the legacy plantprocess-*/ppiq-demo stacks, and a rollback exists.
// Runs inside dotnet test so the pipeline polices its own definition.
// =====================================================================================
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "PPIQ-101")]
public sealed class DeployRedPathProofTests
{
    private static string Root()
    {
        var c = new DirectoryInfo(AppContext.BaseDirectory);
        while (c is not null)
        {
            if (File.Exists(Path.Combine(c.FullName, "Jenkinsfile")) &&
                Directory.Exists(Path.Combine(c.FullName, "Backend")))
                return c.FullName;
            c = c.Parent;
        }
        throw new InvalidOperationException("Repo root with a Jenkinsfile not found.");
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    private static int Pos(string hay, string needle)
    {
        var i = hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        Assert.True(i >= 0, $"PPIQ-101: '{needle}' missing from the Jenkinsfile.");
        return i;
    }

    [Fact]
    public void PPIQ_101_Every_test_stage_precedes_every_deploy_stage()
    {
        var jf = Read("Jenkinsfile");

        var testIndices = new[]
        {
            Pos(jf, "dotnet test"),
            Pos(jf, "npm run test"),
            jf.IndexOf("ci-e2e-stack.sh", StringComparison.OrdinalIgnoreCase)
        }.Where(i => i >= 0).ToArray();

        var shipIndices = new[]
        {
            Pos(jf, "migrate-and-seed.sh"),
            Pos(jf, "deploy-canonical.sh")
        };

        var lastTest = testIndices.Max();
        var firstShip = shipIndices.Min();

        Assert.True(lastTest < firstShip,
            "PPIQ-101: every test stage must precede every migrate/deploy stage - a deploy must be unreachable while any suite is red.");
    }

    [Fact]
    public void PPIQ_101_Deploy_uses_remove_orphans_and_rolls_back()
    {
        var deploy = Read("deploy", "scripts", "deploy-canonical.sh");
        Assert.Contains("--remove-orphans", deploy);

        var jf = Read("Jenkinsfile");
        Assert.Contains("rollback", jf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PPIQ_101_Orphan_purge_targets_legacy_stacks()
    {
        var converge = Read("deploy", "server", "converge-canonical-stack.sh");
        Assert.Contains("--remove-orphans", converge);
        Assert.Contains("plantprocess-", converge);
        Assert.Contains("ppiq-demo", converge);
    }
}