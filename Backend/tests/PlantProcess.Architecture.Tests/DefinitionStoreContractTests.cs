using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// The definition store is the single authority for every no-code and SQL artifact.
// Two of its properties are load-bearing and belong in the database rather than in
// the application: a published version cannot be edited, and a dependency cycle is
// refused. This gate holds the shape of that authority in source.
[Trait("Gate", "DefinitionStore")]
public sealed class DefinitionStoreContractTests
{
    private static readonly string[] Surfaces = { "S1", "S2", "S3", "S4", "S5" };

    private static readonly string[] Statuses =
    {
        "draft", "validated", "published", "paused_by_drift", "rolled_back", "superseded"
    };

    private static readonly string[] DependencyKinds =
    {
        "source", "master_item", "relationship", "feature_set", "model", "page"
    };

    [Fact]
    public void The_three_tables_live_in_the_metadata_schema()
    {
        var sql = Source();

        foreach (var table in new[] { "definition_store", "definition_versions", "definition_dependencies" })
        {
            Assert.True(
                Regex.IsMatch(sql, @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+ppiq_meta\." + table, RegexOptions.IgnoreCase),
                "The definition store belongs in the metadata schema; " + table + " is not created there.");
        }
    }

    [Fact]
    public void Every_declared_surface_and_status_and_dependency_kind_is_constrained()
    {
        var sql = Source();

        foreach (var value in Surfaces.Concat(Statuses).Concat(DependencyKinds))
        {
            Assert.Contains("'" + value + "'", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_published_version_cannot_be_edited()
    {
        var sql = Source();

        Assert.True(
            Regex.IsMatch(sql, @"CREATE\s+TRIGGER\s+trg_definition_versions_immutable", RegexOptions.IgnoreCase),
            "Immutability must be enforced by the database. An authority that relies on every caller " +
            "behaving is not an authority.");

        Assert.True(
            Regex.IsMatch(sql, @"BEFORE\s+UPDATE\s+ON\s+ppiq_meta\.definition_versions", RegexOptions.IgnoreCase),
            "The immutability trigger must fire before the update, not after it.");
    }

    [Fact]
    public void A_dependency_cycle_is_refused()
    {
        var sql = Source();

        Assert.True(
            Regex.IsMatch(sql, @"CREATE\s+TRIGGER\s+trg_definition_dependencies_no_cycle", RegexOptions.IgnoreCase),
            "A cycle makes resolution order undefined; it must be refused at insert time.");

        Assert.True(
            Regex.IsMatch(sql, @"WITH\s+RECURSIVE", RegexOptions.IgnoreCase),
            "Refusing a cycle requires walking the graph, not checking the immediate edge only.");

        Assert.True(
            Regex.IsMatch(sql, @"definition_id\s*<>\s*depends_on_definition_id", RegexOptions.IgnoreCase),
            "The one-step cycle is cheap to refuse with a check constraint and must be.");
    }

    [Fact]
    public void The_store_is_on_the_canonical_path_before_the_views()
    {
        var root = FindRepositoryRoot();
        var manifestPath = Path.Combine(root, "Backend", "database", "canonical-migration-order.json");

        Assert.True(File.Exists(manifestPath), "The canonical migration authority manifest is missing.");

        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));

        var storePosition = 0;
        var firstViewPosition = int.MaxValue;

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            var path = step.GetProperty("path").GetString()!;
            var position = step.GetProperty("position").GetInt32();

            if (path.EndsWith("_definition_store.sql", StringComparison.OrdinalIgnoreCase))
            {
                storePosition = position;
            }
            else if (path.StartsWith("Backend/database/views/", StringComparison.OrdinalIgnoreCase))
            {
                firstViewPosition = Math.Min(firstViewPosition, position);
            }
        }

        Assert.True(storePosition > 0, "The definition store is not on the canonical path.");
        Assert.True(storePosition < firstViewPosition, "Views read the schema and must follow it.");

        var terminal = manifest.RootElement.GetProperty("storageTopology").GetProperty("terminalFile").GetString()!;
        var terminalPosition = 0;

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            if (step.GetProperty("path").GetString() == terminal)
            {
                terminalPosition = step.GetProperty("position").GetInt32();
            }
        }

        Assert.True(terminalPosition > 0, "The terminal storage convergence is not on the canonical path.");

        Assert.True(
            storePosition < terminalPosition,
            "The definition store creates its tables directly in the metadata schema, so it must run before the " +
            "file that relocates everything else. A table already in place is not something the convergence relocates.");
    }

    private static string Source()
    {
        var root = FindRepositoryRoot();
        var scripts = Path.Combine(root, "Backend", "database", "scripts");
        var file = Directory.EnumerateFiles(scripts, "*_definition_store.sql").SingleOrDefault();

        Assert.False(string.IsNullOrEmpty(file), "Exactly one definition store script must exist.");

        var text = File.ReadAllText(file!);
        var withoutBlocks = Regex.Replace(text, @"/\*[\s\S]*?\*/", " ");
        return Regex.Replace(withoutBlocks, @"--[^\r\n]*", " ");
    }

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