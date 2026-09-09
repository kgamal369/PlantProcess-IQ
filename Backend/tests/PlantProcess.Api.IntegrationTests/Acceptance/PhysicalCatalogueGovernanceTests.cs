// PPIQ physical catalogue governance.
//
// The catalogue is only worth trusting if it cannot drift from the database it
// claims to describe, so these tests do not read the catalogue and believe it:
// they re-query pg_catalog and cross-check. A catalogue generated against a
// database that has moved since fails here, which is the point.
//
// PPIQ_CATALOGUE_DATABASE selects which generated catalogue to check, so the
// same tests run against a candidate before promotion and against the promoted
// database afterwards. PPIQ_ACCEPTANCE_EMPTY_CONNECTION supplies the
// connection for the live cross-check.
using FluentAssertions;
using Npgsql;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Acceptance;

public sealed class PhysicalCatalogueGovernanceTests
{
    private const string ConnectionVariable = "PPIQ_ACCEPTANCE_EMPTY_CONNECTION";
    private const string DatabaseVariable = "PPIQ_CATALOGUE_DATABASE";

    private static string CatalogueName() =>
        Environment.GetEnvironmentVariable(DatabaseVariable) is { Length: > 0 } name
            ? name
            : "ppiq_acceptance_empty";

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

    private static string TablesCsv() =>
        Path.Combine(CatalogueDir(), "generated", CatalogueName() + ".tables.csv");

    private static IReadOnlyList<string[]> ReadCsv(string path)
    {
        File.Exists(path).Should().BeTrue($"the generated catalogue must exist at {path}");

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

    [SkippableFact]
    public async Task Catalogue_matches_the_live_database_exactly()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Skip.If(
            string.IsNullOrWhiteSpace(connectionString),
            $"{ConnectionVariable} is not set. The catalogue runner sets it; a bare laptop without the database does not fail the suite.");

        const string Sql = @"
SELECT n.nspname || '.' || c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r','p','v','m','S')
  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  AND n.nspname NOT LIKE 'pg_temp%'
  AND n.nspname NOT LIKE 'pg_toast%';";

        var live = new HashSet<string>(StringComparer.Ordinal);
        await using (var connection = new NpgsqlConnection(connectionString!))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(Sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { live.Add(reader.GetString(0)); }
        }

        var rows = ReadCsv(TablesCsv());
        rows.Should().NotBeEmpty("an empty catalogue would make this test meaningless");
        var catalogued = new HashSet<string>(rows.Select(r => $"{r[0]}.{r[1]}"), StringComparer.Ordinal);

        var missing = live.Except(catalogued, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var invented = catalogued.Except(live, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        missing.Should().BeEmpty("every object in the database must appear in the catalogue; a missing one means the catalogue is stale");
        invented.Should().BeEmpty("the catalogue must not name objects the database does not have");
    }

    [Fact]
    public void Every_object_carries_family_lifecycle_owner_and_a_resolved_creator()
    {
        var rows = ReadCsv(TablesCsv());
        rows.Should().NotBeEmpty();

        var unclassified = rows
            .Where(r => r[3] == "UNCLASSIFIED" || r[4] == "UNCLASSIFIED" || r[5] == "UNCLASSIFIED")
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unclassified.Should().BeEmpty(
            "zero-unknown is the contract of the physical catalogue. Classification derives from the governed " +
            "schema and the measured creator; an object here means neither resolved it.");

        var unresolved = rows
            .Where(r => r[7] == "UNRESOLVED")
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unresolved.Should().BeEmpty(
            "an object whose creating script cannot be identified is ungoverned by definition. Trace it to a " +
            "migration, a canonical script or an offPath script before this can be green.");
    }

    [Fact]
    public void Public_holds_only_platform_or_explicitly_bounded_compatibility()
    {
        var offenders = ReadCsv(TablesCsv())
            .Where(r => r[0] == "public")
            .Where(r => r[16] != "platform-allowlist" && r[16] != "explicit" && r[16] != "creator-authority")
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a public object is either platform infrastructure on the allowlist, or bounded compatibility with a " +
            "measured creator and a retirement owner. Nothing may sit there unaccounted for.");
    }

    [Fact]
    public void Every_compatibility_object_names_a_retirement_owner()
    {
        var unbounded = ReadCsv(TablesCsv())
            .Where(r => r[4] == "compatibility")
            .Where(r => !r[15].StartsWith("bounded:", StringComparison.Ordinal))
            .Select(r => $"{r[0]}.{r[1]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unbounded.Should().BeEmpty(
            "a compatibility object with no named retirement owner is a permanent object wearing a temporary label");
    }

    [Fact]
    public void Classification_rules_are_schema_scoped_and_carry_no_catch_all()
    {
        foreach (var name in new[] { "physical-object-classification.tsv", "classification-rules.tsv", "public-platform-allowlist.tsv" })
        {
            File.Exists(Path.Combine(CatalogueDir(), name)).Should().BeTrue($"{name} is a catalogue authority file");
        }

        var rules = File.ReadAllLines(Path.Combine(CatalogueDir(), "classification-rules.tsv"))
            .Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#", StringComparison.Ordinal))
            .ToList();

        rules.Should().NotBeEmpty("an empty rule file would push every governed object into the manual queue");

        var malformed = rules
            .Where(l => l.Split(new[] { '\t' }, StringSplitOptions.None).Length < 5)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        malformed.Should().BeEmpty("every rule needs schema, family, lifecycle, owner and retention, tab-separated");

        var wildcard = rules
            .Where(l => l.StartsWith("*", StringComparison.Ordinal))
            .ToList();

        wildcard.Should().BeEmpty("a catch-all rule would make the zero-unknown gate green and meaningless");
    }
}