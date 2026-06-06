using Xunit;

namespace PlantProcess.Api.IntegrationTests.Demo;

public sealed class Phase2NoFacadeMetricSourceTests
{
    [Fact]
    public void Demo_pages_must_not_hardcode_realism_scale_metric_cards()
    {
        var root = FindRepoRoot();
        var frontend = Path.Combine(root, "Frontend", "PlantProcess.Web", "src");

        if (!Directory.Exists(frontend))
            return;

        var forbidden = new[]
        {
            "5,600 coils",
            "5600 coils",
            "~5,600 coils",
            "630 heats",
            "~630 heats",
            "C-0044170 resolves to H-3361"
        };

        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(frontend, "*.tsx", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}"))
                continue;

            var text = File.ReadAllText(file);
            foreach (var phrase in forbidden)
            {
                if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{Path.GetRelativePath(root, file)} -> {phrase}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Demo metrics must come from backend/source data, not hardcoded frontend facade literals: " +
            string.Join("; ", offenders));
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