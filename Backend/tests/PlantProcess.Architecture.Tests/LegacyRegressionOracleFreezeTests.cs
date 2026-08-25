using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// =====================================================================================
// Legacy regression oracle freeze gate.  (backlog reference: T-086)
//
// The legacy discrete-manufacturing dataset is retained as a controlled regression
// oracle with predeclared expected effects and negative controls. It is not canonical
// product vocabulary and no generic contract may derive vocabulary or a universal
// grain from it.
//
// This gate enforces two properties:
//   1. Oracle assets are immutable unless the lock file is deliberately regenerated.
//   2. Generic product surfaces gain no new legacy vocabulary, and the recorded
//      baseline of existing occurrences may shrink but never grow.
//
// Every scanned term is assembled from fragments so this file can never satisfy the
// rules it enforces, and every read strips comments first.
// =====================================================================================

[Trait("Gate", "LegacyRegressionOracleFreeze")]
public sealed class LegacyRegressionOracleFreezeTests
{
    private const string LockRelativePath =
        "Backend/tools/legacy-regression-oracle.lock.json";

    private const string BaselineRelativePath =
        "Backend/tests/PlantProcess.Architecture.Tests/legacy-vocabulary-baseline.txt";

    private static readonly string[] GenericScanRoots =
    {
        "Backend/PlantProcess.Analytics.Core",
        "Backend/PlantProcess.Domain",
        "Backend/PlantProcess.Application",
        "Backend/PlantProcess.ML.Runtime",
        "Backend/PlantProcess.Infrastructure"
    };

    private static readonly string[] LegacyVocabulary =
    {
        "co" + "il",
        "he" + "at",
        "cas" + "ter",
        "tun" + "dish",
        "melt" + "shop",
        "fl" + "eet"
    };

    [Fact]
    public void Every_pinned_oracle_asset_still_matches_its_recorded_hash()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, LockRelativePath)));

        var checkedCount = 0;

        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (!asset.GetProperty("present").GetBoolean())
            {
                continue;
            }

            var relative = asset.GetProperty("path").GetString()!;
            var expected = asset.GetProperty("sha256").GetString()!;
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(
                File.Exists(full),
                "A pinned oracle asset has been deleted: " + relative +
                ". The oracle is frozen; removing an asset requires regenerating the lock in the same commit.");

            Assert.Equal(expected, Sha256Of(full));
            checkedCount++;
        }

        Assert.True(
            checkedCount > 0,
            "The lock pins no present asset. An empty freeze proves nothing.");
    }

    [Fact]
    public void Declared_phenomena_carry_predeclared_expectations()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, LockRelativePath)));

        var phenomena = document.RootElement.GetProperty("declaredPhenomena").EnumerateArray().ToList();

        Assert.True(
            phenomena.Count > 0,
            "An oracle with no predeclared answers cannot falsify anything.");

        foreach (var phenomenon in phenomena)
        {
            var id = phenomenon.GetProperty("id").GetString();

            Assert.False(
                string.IsNullOrWhiteSpace(id),
                "Every declared phenomenon needs an identifier.");

            Assert.False(
                string.IsNullOrWhiteSpace(phenomenon.GetProperty("expectedDirection").GetString()),
                "Phenomenon '" + id + "' declares no expected direction, so no run of it can fail.");
        }
    }

    [Fact]
    public void Generic_surfaces_gain_no_new_legacy_vocabulary()
    {
        var root = FindRepositoryRoot();
        var baseline = LoadBaseline(root);
        var found = ScanGenericSurfaces(root);

        var added = found.Where(entry => !baseline.Contains(entry)).OrderBy(entry => entry).ToList();

        Assert.True(
            added.Count == 0,
            "Generic product surfaces must not derive vocabulary from the legacy regression oracle. " +
            "New occurrences, not present in the recorded baseline:" + Environment.NewLine +
            string.Join(Environment.NewLine, added.Take(25)));

        Assert.True(
            found.Count <= baseline.Count,
            "The legacy-vocabulary baseline is a ratchet. It may shrink, never grow. Baseline holds " +
            baseline.Count + " entries; the scan found " + found.Count + ".");
    }

    private static HashSet<string> LoadBaseline(string root)
    {
        var path = Path.Combine(root, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var entries = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal));

        return new HashSet<string>(entries, StringComparer.Ordinal);
    }

    private static HashSet<string> ScanGenericSurfaces(string root)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scanRoot in GenericScanRoots)
        {
            var full = Path.Combine(root, scanRoot.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                var relative = file.Substring(root.Length + 1).Replace('\\', '/');
                var lines = StripComments(File.ReadAllText(file)).Replace("\r\n", "\n").Split('\n');

                for (var index = 0; index < lines.Length; index++)
                {
                    foreach (var term in LegacyVocabulary)
                    {
                        if (Regex.IsMatch(lines[index], @"\b" + term + @"s?\b", RegexOptions.IgnoreCase))
                        {
                            found.Add(relative + "|" + (index + 1) + "|" + term);
                        }
                    }
                }
            }
        }

        return found;
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
    }

    private static string Sha256Of(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    // Isolation law: every gate file resolves the repository root itself and declares
    // no shared type, so it compiles whether or not any other pack has landed.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}
