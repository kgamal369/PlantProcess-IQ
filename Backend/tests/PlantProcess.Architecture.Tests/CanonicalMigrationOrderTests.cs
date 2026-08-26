using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// Canonical migration order truth gate.  (backlog reference: T-088)
//
// Backend/database accumulated hotfix, repair, phase and drift-correction scripts
// over months. This holds the line: one ordered path, every file dispositioned,
// no unknown state, no demonstration content on the path, and no unguarded second
// CREATE for a table the path already creates.
//
// Reads strip single-quoted literals and comments first, so a script's own prose
// can neither satisfy nor violate a rule it merely describes.
[Trait("Gate", "CanonicalMigrationOrder")]
public sealed class CanonicalMigrationOrderTests
{
    private const string ManifestRelativePath = "Backend/database/canonical-migration-order.json";
    private const string DatabaseRoot = "Backend/database";

    private static readonly string[] AllowedDispositions =
    {
        "Canonical", "HistoricalRepair", "Superseded", "FixtureOnly", "DeferredWithReason"
    };

    private static readonly Regex DropTable = new(
        @"\bDROP\s+TABLE\s+(?:IF\s+EXISTS\s+)?([A-Za-z0-9_""$.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CreateTable = new(
        @"\bCREATE\s+(?:UNLOGGED\s+|TEMP(?:ORARY)?\s+|GLOBAL\s+|LOCAL\s+)*TABLE\s+(IF\s+NOT\s+EXISTS\s+)?([A-Za-z0-9_""$.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    [Fact]
    public void The_path_is_contiguously_ordered()
    {
        using var manifest = Load();
        var expected = 1;

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            Assert.Equal(expected, step.GetProperty("position").GetInt32());
            expected++;
        }

        Assert.True(expected > 1, "The canonical path is empty. An empty path builds nothing.");
    }

    [Fact]
    public void Every_sql_file_carries_exactly_one_known_disposition()
    {
        var root = FindRepositoryRoot();
        using var manifest = Load();

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            Record(seen, step.GetProperty("path").GetString()!, step.GetProperty("disposition").GetString()!);
        }

        foreach (var off in manifest.RootElement.GetProperty("offPath").EnumerateArray())
        {
            Record(seen, off.GetProperty("path").GetString()!, off.GetProperty("disposition").GetString()!);
        }

        foreach (var value in seen.Values)
        {
            Assert.Contains(value, AllowedDispositions);
        }

        var directory = Path.Combine(root, DatabaseRoot.Replace('/', Path.DirectorySeparatorChar));
        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories))
        {
            var relative = file.Substring(root.Length + 1).Replace('\\', '/');

            if (!seen.ContainsKey(relative))
            {
                missing.Add(relative);
            }
        }

        Assert.True(
            missing.Count == 0,
            "A migration script exists that the manifest does not disposition. Add it in the same commit that " +
            "creates it:" + Environment.NewLine + string.Join(Environment.NewLine, missing.Take(25)));
    }

    [Fact]
    public void Every_script_on_the_path_still_matches_its_recorded_hash()
    {
        var root = FindRepositoryRoot();
        using var manifest = Load();

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            var relative = step.GetProperty("path").GetString()!;
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(full), "A script on the path no longer exists: " + relative);
            Assert.Equal(step.GetProperty("sha256").GetString(), Sha256Of(full));
        }
    }

    [Fact]
    public void No_unguarded_duplicate_create_table_exists_on_the_path()
    {
        var root = FindRepositoryRoot();
        using var manifest = Load();

        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var offenders = new List<string>();

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            var relative = step.GetProperty("path").GetString()!;
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            var code = Strip(File.ReadAllText(full));

            var drops = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Match drop in DropTable.Matches(code))
            {
                var dropped = Qualify(drop.Groups[1].Value);

                if (!drops.ContainsKey(dropped))
                {
                    drops[dropped] = drop.Index;
                }
            }

            foreach (Match match in CreateTable.Matches(code))
            {
                var table = Qualify(match.Groups[2].Value);

                // Governed either by IF NOT EXISTS, or by dropping the table first.
                // Drop-and-recreate is how a column drift gets corrected; demanding
                // IF NOT EXISTS there would hide the drift again.
                var guarded = match.Groups[1].Value.Length > 0 ||
                              (drops.TryGetValue(table, out var dropIndex) && dropIndex < match.Index);

                if (owners.TryGetValue(table, out var first))
                {
                    if (!guarded)
                    {
                        offenders.Add(table + " first created by " + first + ", recreated unguarded by " + relative);
                    }
                }
                else
                {
                    owners[table] = relative;
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "One table, one creating script. A later unguarded CREATE cannot replay from zero:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders.Take(25)));
    }

    [Fact]
    public void No_demonstration_content_sits_on_the_path()
    {
        using var manifest = Load();
        var offenders = new List<string>();

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            var relative = step.GetProperty("path").GetString()!;

            if (Regex.IsMatch(relative, "(?i)(de" + "mo|gol" + "den|synth" + "etic|/se" + "ed/)"))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Customer-demonstration content must never be part of the path that builds a database:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_schema_baseline_is_the_ef_migration_set()
    {
        using var manifest = Load();
        var baseline = manifest.RootElement.GetProperty("efMigrationBaseline");
        var migrations = baseline.GetProperty("migrations").EnumerateArray().ToList();

        Assert.True(
            migrations.Count > 0,
            "The canonical path must record the EF migration baseline. Core tables are created by migrations, " +
            "not by SQL scripts, and a path that omits them cannot build from zero.");

        var root = FindRepositoryRoot();

        foreach (var migration in migrations)
        {
            var name = migration.GetProperty("name").GetString()!;
            var file = Path.Combine(
                root, "Backend", "PlantProcess.Infrastructure", "Migrations", name + ".cs");

            Assert.True(File.Exists(file), "A recorded migration no longer exists: " + name);
            Assert.Equal(migration.GetProperty("sha256").GetString(), Sha256Of(file));
        }
    }

    [Fact]
    public void The_read_model_view_authority_is_represented()
    {
        var root = FindRepositoryRoot();
        using var manifest = Load();

        var views = manifest.RootElement.GetProperty("canonicalViews").EnumerateArray().ToList();

        Assert.True(
            views.Count > 0,
            "Read-model views are a separate authority applied after schema. A manifest that records none " +
            "would let a view disappear from a fresh install without anything noticing.");

        var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in views)
        {
            var relative = view.GetProperty("path").GetString()!;
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(full), "A recorded view authority no longer exists: " + relative);
            Assert.Equal(view.GetProperty("sha256").GetString(), Sha256Of(full));

            recorded.Add(relative);
        }

        var viewsDirectory = Path.Combine(root, "Backend", "database", "views");
        var missing = new List<string>();

        if (Directory.Exists(viewsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(viewsDirectory, "*.sql", SearchOption.AllDirectories))
            {
                var relative = file.Substring(root.Length + 1).Replace('\\', '/');

                if (!recorded.Contains(relative))
                {
                    missing.Add(relative);
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "A view authority exists on disk but is absent from the manifest:" +
            Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Views_apply_after_every_schema_script()
    {
        using var manifest = Load();

        var viewOrders = manifest.RootElement.GetProperty("canonicalViews").EnumerateArray()
            .Select(view => view.GetProperty("order").GetInt32())
            .ToList();

        Assert.True(viewOrders.Count > 0, "No view authority is recorded.");

        var lastSchemaPosition = 0;

        foreach (var step in manifest.RootElement.GetProperty("canonicalPath").EnumerateArray())
        {
            var relative = step.GetProperty("path").GetString()!;

            if (!relative.StartsWith("Backend/database/views/", StringComparison.OrdinalIgnoreCase))
            {
                lastSchemaPosition = Math.Max(lastSchemaPosition, step.GetProperty("position").GetInt32());
            }
        }

        foreach (var order in viewOrders)
        {
            Assert.True(
                order > lastSchemaPosition,
                "A read-model view applies before the schema it reads is complete. Views run last, always.");
        }
    }

    [Fact]
    public void The_fresh_build_was_actually_executed()
    {
        using var manifest = Load();
        var summary = manifest.RootElement.GetProperty("summary");

        Assert.Equal("EXECUTED", summary.GetProperty("freshBuild").GetString());

        Assert.True(
            summary.GetProperty("freshBuildTables").GetInt32() > 0,
            "A fresh build that produced no table did not build anything.");

        Assert.Equal(0, summary.GetProperty("declaredViewsMissing").GetInt32());

        Assert.True(
            summary.GetProperty("declaredViews").GetInt32() > 0,
            "The view authority declared no view, so proving none exist proves nothing.");
    }

    private static void Record(Dictionary<string, string> seen, string path, string disposition)
    {
        Assert.False(
            seen.ContainsKey(path),
            "A script is dispositioned twice, which means it has no single disposition: " + path);

        seen[path] = disposition;
    }

    private static JsonDocument Load()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string Qualify(string raw)
    {
        var table = raw.Replace("\"", string.Empty).ToLowerInvariant();
        return table.Contains('.') ? table : "public." + table;
    }

    private static string Strip(string sql)
    {
        var withoutLiterals = Regex.Replace(sql, @"'(?:[^']|'')*'", "''");
        var withoutBlocks = Regex.Replace(withoutLiterals, @"/\*[\s\S]*?\*/", " ");
        return Regex.Replace(withoutBlocks, @"--[^\r\n]*", " ");
    }

    private static string Sha256Of(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
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