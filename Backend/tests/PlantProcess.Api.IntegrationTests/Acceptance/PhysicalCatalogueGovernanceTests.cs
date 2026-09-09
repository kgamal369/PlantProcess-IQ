// PPIQ physical catalogue governance.
//
// The catalogue is only worth trusting if it cannot drift from the database it
// claims to describe. So this test does not read the catalogue and believe it:
// it re-queries pg_catalog and cross-checks. A catalogue generated last week
// against a database that has moved since fails here, which is the entire point.
//
// It also reads the SAME authority files the generator reads, so the test and
// the generator cannot disagree about what "classified" means.
//
// Skips unless PPIQ_ACCEPTANCE_EMPTY_CONNECTION is set, following the pattern
// already established by Rule2EmptyStartTests in this project: a bare laptop
// without the acceptance database does not fail the suite, and CI sets it.
using FluentAssertions;
using Npgsql;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Acceptance;

public sealed class PhysicalCatalogueGovernanceTests
{
    private const string ConnectionVariable = "PPIQ_ACCEPTANCE_EMPTY_CONNECTION";
    private const string DatabaseName = "ppiq_acceptance_empty";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend", "database")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
        }

        return dir.FullName;
    }

    private static string CatalogueDir() => Path.Combine(RepoRoot(), "Backend", "database", "catalogue");

    private static IReadOnlyList<string[]> ReadCsv(string path)
    {
        File.Exists(path).Should().BeTrue($"the generated catalogue must exist at {path}. Run tools/db/New-PhysicalDatabaseCatalogue.ps1.");

        var rows = new List<string[]>();
        var lines = File.ReadAllLines(path);
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            rows.Add(SplitCsvLine(lines[i]));
        }

        return rows;
    }

    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else { quoted = false; }
                }
                else { current.Append(ch); }
            }
            else if (ch == '"') { quoted = true; }
            else if (ch == ',') { cells.Add(current.ToString()); current.Clear(); }
            else { current.Append(ch); }
        }

        cells.Add(current.ToString());
        return cells.ToArray();
    }

    private static async Task<HashSet<string>> LiveObjectsAsync(string connectionString)
    {
        const string Sql = @"
SELECT n.nspname || '.' || c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r','p','v','m','S')
  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  AND n.nspname NOT LIKE 'pg_temp%'
  AND n.nspname NOT LIKE 'pg_toast%';";

        var live = new HashSet<string>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(Sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            live.Add(reader.GetString(0));
        }

        return live;
    }

    [SkippableFact]
    public async Task Catalogue_covers_every_live_object_and_nothing_it_invented()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Skip.If(
            string.IsNullOrWhiteSpace(connectionString),
            $"{ConnectionVariable} is not set. Build the acceptance database with " +
            "scripts/db/New-AcceptanceEmptyDb.ps1 -Execute and set the variable to run this locally.");

        var rows = ReadCsv(Path.Combine(CatalogueDir(), "generated", $"{DatabaseName}.tables.csv"));
        rows.Should().NotBeEmpty("an empty catalogue would make this test meaningless");

        var catalogued = new HashSet<string>(rows.Select(r => $"{r[0]}.{r[1]}"), StringComparer.Ordinal);
        var live = await LiveObjectsAsync(connectionString!);

        var missing = live.Except(catalogued, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var invented = catalogued.Except(live, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        missing.Should().BeEmpty(
            "every object in the database must appear in the catalogue. A missing object means the " +
            "catalogue is stale or the generator filtered something it should not have. Regenerate with " +
            "tools/db/New-PhysicalDatabaseCatalogue.ps1.");

        invented.Should().BeEmpty(
            "the catalogue must not name objects the database does not have. This means the catalogue " +
            "was generated against a different database or an object was dropped after generation.");
    }

    [Fact]
    public void Every_catalogued_object_carries_family_lifecycle_and_owner()
    {
        var path = Path.Combine(CatalogueDir(), "generated", $"{DatabaseName}.tables.csv");
        var rows = ReadCsv(path);
        rows.Should().NotBeEmpty();

        var unclassified = rows
            .Where(r => r[3] == "UNCLASSIFIED" || r[4] == "UNCLASSIFIED" || r[5] == "UNCLASSIFIED")
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unclassified.Should().BeEmpty(
            "zero-unknown is the whole contract of the physical catalogue. Each object listed here needs " +
            "one adjudicated row in Backend/database/catalogue/physical-object-classification.tsv, or a " +
            "reviewed pattern in classification-rules.tsv. Do not add a catch-all rule to clear this list.");
    }

    [Fact]
    public void Public_schema_holds_only_allowlisted_platform_objects()
    {
        var path = Path.Combine(CatalogueDir(), "generated", $"{DatabaseName}.tables.csv");
        var offenders = ReadCsv(path)
            .Where(r => r[0] == "public")
            .Where(r => r[15] != "public-allowlist" && r[15] != "explicit")
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "product persistence belongs in ppiq_meta, ppiq_plant or ppiq_staging. A public object is " +
            "either platform infrastructure listed in public-platform-allowlist.tsv, or an explicitly " +
            "adjudicated bounded compatibility object. Anything else is a product object out of place.");
    }

    [Fact]
    public void Every_bounded_compatibility_object_names_a_retirement_owner()
    {
        var path = Path.Combine(CatalogueDir(), "generated", $"{DatabaseName}.tables.csv");
        var unbounded = ReadCsv(path)
            .Where(r => r[4] == "compatibility")
            .Where(r => !r[14].StartsWith("bounded:", StringComparison.Ordinal) || r[14] == "bounded:unstated")
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unbounded.Should().BeEmpty(
            "a compatibility object with no named retirement owner is a permanent object wearing a " +
            "temporary label. Set compatibility_state to bounded:<owner or task> in " +
            "physical-object-classification.tsv.");
    }

    [Fact]
    public void Authority_files_are_present_and_parse()
    {
        foreach (var name in new[]
                 {
                     "physical-object-classification.tsv",
                     "classification-rules.tsv",
                     "public-platform-allowlist.tsv"
                 })
        {
            var path = Path.Combine(CatalogueDir(), name);
            File.Exists(path).Should().BeTrue($"{name} is a catalogue authority file and must exist");
        }

        var rules = File.ReadAllLines(Path.Combine(CatalogueDir(), "classification-rules.tsv"))
            .Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#", StringComparison.Ordinal))
            .ToList();

        rules.Should().NotBeEmpty("an empty rule file would push every object into the manual queue silently");

        // Deliberately not FluentAssertions' OnlyContain/NotContain here. Those take an
        // Expression<Func<T,bool>>, and an expression tree may not contain a call that
        // uses optional arguments - which string.Split(char, StringSplitOptions = None)
        // does. Materialising the offenders keeps the failure message better anyway: it
        // names the bad lines instead of asserting that some line is bad.
        var malformed = rules
            .Where(l => l.Split(new[] { '\t' }, StringSplitOptions.None).Length < 7)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        malformed.Should().BeEmpty(
            "every rule needs schema, pattern, kind, family, lifecycle, owner and retention, " +
            "tab-separated. A short line silently classifies nothing.");

        var catchAll = rules
            .Where(l => l.StartsWith("*\t*\t*\t", StringComparison.Ordinal))
            .ToList();

        catchAll.Should().BeEmpty(
            "a catch-all rule would make the zero-unknown gate green and meaningless");
    }
}