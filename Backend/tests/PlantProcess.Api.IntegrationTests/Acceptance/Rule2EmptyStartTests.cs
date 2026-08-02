// PPIQ-T13 Rule 2: the plant schema starts empty, provable in one query.
//
// This test reads the SAME allowlist file the proof query is generated from, so
// the test and the query cannot drift apart. Every table not on the allowlist is
// counted; the total must be zero. A table added later is plant data by default
// and this test goes red until somebody classifies it on purpose.
//
// It skips unless PPIQ_ACCEPTANCE_EMPTY_CONNECTION is set, following the same
// pattern as the other database-dependent integration tests in this project:
// a bare laptop without the acceptance database does not fail the suite, and
// CI sets the variable.
using System.Text;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Acceptance;

public sealed class Rule2EmptyStartTests
{
    private const string ConnectionVariable = "PPIQ_ACCEPTANCE_EMPTY_CONNECTION";

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

    private static IReadOnlyList<string> ReadAllowlist()
    {
        var path = Path.Combine(RepoRoot(), "Backend", "database", "acceptance", "rule2_prefill_allowlist.txt");
        File.Exists(path).Should().BeTrue($"the prefill allowlist must exist at {path}");

        var allowed = new List<string>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            allowed.Add(line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0]);
        }

        // A positive check beside the forbidding check: if the parse breaks, fail
        // loudly rather than allowlisting nothing and passing by accident.
        allowed.Should().NotBeEmpty("an empty allowlist would make this test meaningless");
        return allowed;
    }

    private static string BuildProofSql(IReadOnlyList<string> allowed)
    {
        var list = new StringBuilder();
        for (var i = 0; i < allowed.Count; i++)
        {
            if (i > 0)
            {
                list.Append(", ");
            }

            list.Append('\'').Append(allowed[i].Replace("'", "''")).Append('\'');
        }

        return $@"
WITH candidate AS (
    SELECT n.nspname, c.relname
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE c.relkind = 'r'
      AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
      AND c.relname NOT IN ({list})
),
counted AS (
    SELECT (xpath('/row/c/text()',
            query_to_xml(format('SELECT count(*) AS c FROM %I.%I', nspname, relname), false, true, '')
           ))[1]::text::bigint AS row_count
    FROM candidate
)
SELECT coalesce(sum(row_count), 0) FROM counted;";
    }

    [SkippableFact]
    public async Task Plant_schema_starts_empty_in_one_query()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        Skip.If(
            string.IsNullOrWhiteSpace(connectionString),
            $"{ConnectionVariable} is not set. Build the acceptance database with " +
            "scripts/db/New-AcceptanceEmptyDb.ps1 -Execute and set the variable to run this locally.");

        var allowed = ReadAllowlist();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(BuildProofSql(allowed), connection);
        var scalar = await command.ExecuteScalarAsync();
        var rows = Convert.ToInt64(scalar);

        rows.Should().Be(
            0,
            "Rule 2 requires the plant schema to start empty. A non-zero count means a migration " +
            "wrote customer-shaped data into a fresh database, or a new table needs classifying in " +
            "Backend/database/acceptance/rule2_prefill_allowlist.txt.");
    }
}