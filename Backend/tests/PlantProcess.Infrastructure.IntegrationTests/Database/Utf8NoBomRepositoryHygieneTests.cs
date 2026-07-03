using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Database;

public sealed class Utf8NoBomRepositoryHygieneTests
{
    [Fact]
    public void Active_json_and_sql_files_must_be_utf8_without_bom()
    {
        var root = FindRepoRoot();

        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(p =>
                !ShouldSkipPath(root, p) &&
                (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var offenders = new List<string>();

        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);

            if (bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF)
            {
                offenders.Add(Path.GetRelativePath(root, file));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Active .json/.sql files with UTF-8 BOM: " + string.Join(", ", offenders));
    }

    private static bool ShouldSkipPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('/', '\\');

        if (relative.StartsWith(".git\\", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("node_modules\\", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("obj\\", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("dist\\", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("deploy\\.ppiq-backups\\", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith(".phase1_phase2_backup\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\.git\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\node_modules\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\dist\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\.ppiq-backups\\", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("\\.phase1_phase2_backup\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Historical generated audit outputs are not active product/runtime files.
        if (relative.StartsWith("Documentation\\UltimateAudit_", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("Documentation\\hygiene\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (relative.StartsWith("Documentation\\", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(relative).StartsWith("manifest_", StringComparison.OrdinalIgnoreCase) &&
            relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Frontend")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root could not be found.");
    }
}