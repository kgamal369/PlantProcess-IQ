using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

public sealed class ProductionDevEndpointGuardTests
{
    [Fact]
    public void Program_maps_dev_seed_endpoints_only_inside_development_gate()
    {
        var root = FindRepoRoot();
        var programPath = Path.Combine(root, "Backend", "PlantProcess.Api", "Program.cs");
        Assert.True(File.Exists(programPath), $"Program.cs not found at {programPath}");

        var source = File.ReadAllText(programPath);

        Assert.Contains("MapDevSeedEndpoints", source);
        Assert.Matches(
            new Regex(@"if\s*\(\s*app\.Environment\.IsDevelopment\(\)\s*\)\s*\{[\s\S]*?app\.MapDevSeedEndpoints\(\);[\s\S]*?\}", RegexOptions.Multiline),
            source);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Frontend")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root could not be found.");
    }
}