// =====================================================================================
// PPIQ-101 - Deploy red-path proof gate (extends the CI truth gate).
//
// CiPipelineTruthGateTests already proves the LAST test stage precedes the FIRST ship
// stage. Rule A1.9 asks for the stricter guarantee plus the orphan-zero invariant:
//
//   (1) EVERY test stage in the Jenkinsfile precedes EVERY deploy (migrate/seed/build/
//       recreate) stage - so a single red suite makes the whole ship path unreachable,
//       not merely "mostly" unreachable.
//   (2) The pipeline can only ever deploy the ONE canonical compose project
//       (plantprocessiq). It must never reference a parallel stack such as the legacy
//       ppiq-demo project, which is exactly how orphaned stacks accrete on the server.
//
// This runs INSIDE `dotnet test`, so the pipeline that executes it polices its own
// definition. The server-side proof (docker compose ls == one project, a deliberately
// red branch skipping stages 6-7, then a green deploy with /health=200 through Caddy) is
// captured in deploy/runbooks/ppiq-101-deploy-red-path-proof.md - it cannot run in-process.
// =====================================================================================
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T05")]
[Trait("Task", "PPIQ-101")]
public sealed class DeployRedPathProofTests
{
    // A stage is a TEST stage if its body invokes one of these suites.
    private static readonly string[] TestStageMarkers =
    {
        "dotnet test",
        "npm run test",
        "ci-e2e-stack.sh",
    };

    // A stage is a DEPLOY stage if its body invokes migrate/seed or build/recreate.
    private static readonly string[] DeployStageMarkers =
    {
        "migrate-and-seed.sh",
        "deploy-canonical.sh",
    };

    private const string CanonicalProject = "plantprocessiq";
    private const string CanonicalComposeFile = "Infrastructure/deploy/docker-compose.demo.yml";

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

    // Split the Jenkinsfile into ordered stage blocks: (declared name, body text, order).
    // The body runs from this stage('...') declaration to the next one (or EOF), which is
    // enough to classify a stage by the command it invokes without a full Groovy parse.
    private static IReadOnlyList<(string Name, string Body, int Order)> ParseStages(string jf)
    {
        var matches = Regex.Matches(jf, @"stage\(\s*'([^']+)'\s*\)");
        var stages = new List<(string, string, int)>();
        for (var k = 0; k < matches.Count; k++)
        {
            var start = matches[k].Index;
            var end = k + 1 < matches.Count ? matches[k + 1].Index : jf.Length;
            stages.Add((matches[k].Groups[1].Value, jf.Substring(start, end - start), k));
        }
        return stages;
    }

    private static bool HasMarker(string body, string[] markers) =>
        markers.Any(m => body.Contains(m, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Every_test_stage_precedes_every_deploy_stage()
    {
        var stages = ParseStages(Jenkinsfile());

        var testStages = stages.Where(s => HasMarker(s.Body, TestStageMarkers)).ToList();
        var deployStages = stages.Where(s => HasMarker(s.Body, DeployStageMarkers)).ToList();

        Assert.True(testStages.Count > 0,
            "PPIQ-101: the Jenkinsfile declares no test stage - a deploy with no preceding suite is never safe.");
        Assert.True(deployStages.Count > 0,
            "PPIQ-101: the Jenkinsfile declares no deploy stage - nothing for the red path to gate.");

        // Full cartesian product: for all t in tests, for all d in deploys, order(t) < order(d).
        foreach (var t in testStages)
        foreach (var d in deployStages)
            Assert.True(t.Order < d.Order,
                $"PPIQ-101: test stage '{t.Name}' (#{t.Order}) must precede deploy stage '{d.Name}' (#{d.Order}). " +
                "Any test stage at or after a deploy stage means a red suite can ship.");
    }

    [Fact]
    public void No_stage_is_both_test_and_deploy()
    {
        // A stage that both tests and ships would let the ship half run while the test half is red.
        foreach (var s in ParseStages(Jenkinsfile()))
            Assert.False(
                HasMarker(s.Body, TestStageMarkers) && HasMarker(s.Body, DeployStageMarkers),
                $"PPIQ-101: stage '{s.Name}' mixes a test suite and a deploy command in one block - split them.");
    }

    [Fact]
    public void Pipeline_pins_the_single_canonical_compose_project()
    {
        var jf = Jenkinsfile();

        Assert.Matches(
            new Regex(@"COMPOSE_PROJECT\s*=\s*'" + Regex.Escape(CanonicalProject) + "'"),
            jf);
        Assert.Contains(CanonicalComposeFile, jf, StringComparison.OrdinalIgnoreCase);

        // The pipeline must never reference the legacy parallel demo stack. That project
        // (ppiq-demo, services plantprocess-*) is the documented orphan-stack risk in
        // deploy/compose/docker-compose.demo.yml; the canonical stack lives elsewhere.
        Assert.DoesNotContain("ppiq-demo", jf, StringComparison.OrdinalIgnoreCase);
    }
}
