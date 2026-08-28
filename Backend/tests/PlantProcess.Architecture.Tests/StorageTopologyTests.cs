using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Persistence.Topology;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// Storage topology truth gate.
//
// Product storage is governed by three schemas. This holds the line in both
// halves at once: the relational model must place every mapped entity in a
// governed schema, and the physical convergence must be the only thing that
// moves a table, exactly once, without creating anything.
//
// SQL is read with literals and comments stripped first, so a file's own prose
// can neither satisfy nor violate a rule it merely describes.
[Trait("Gate", "StorageTopology")]
public sealed class StorageTopologyTests
{
    private const string ConvergenceRelativePath =
        "Backend/database/topology/000_storage_topology_convergence.sql";

    private const string ViewAuthorityRelativePath =
        "Backend/database/views/006_dashboard_dataset_views.sql";

    private static readonly string[] Governed = { "ppiq_meta", "ppiq_plant", "ppiq_staging" };

    [Fact]
    public void Every_mapped_entity_lives_in_a_governed_schema()
    {
        using var context = BuildContext();

        var stranded = new List<string>();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();

            if (string.IsNullOrWhiteSpace(table))
            {
                continue;
            }

            var schema = entityType.GetSchema();

            if (schema is null || !Governed.Contains(schema))
            {
                stranded.Add(entityType.ClrType.Name + " -> " + (schema ?? "public") + "." + table);
            }
        }

        Assert.True(
            stranded.Count == 0,
            "An entity is mapped outside the governed topology. Either the frozen assignment does not name its " +
            "table, or the entity has no explicit table name for the assignment to match:" +
            Environment.NewLine + string.Join(Environment.NewLine, stranded.Take(25)));
    }

    [Fact]
    public void The_topology_map_places_tables_only_in_governed_schemas()
    {
        Assert.True(StorageTopologyMap.Count > 0, "The topology map is empty, so it governs nothing.");

        foreach (var placement in StorageTopologyMap.Placements_)
        {
            Assert.Contains(placement.Value, Governed);
        }
    }

    [Fact]
    public void Convergence_relocates_and_never_creates()
    {
        var raw = ReadRepositoryFile(ConvergenceRelativePath);

        // The relocation is intentionally dynamic PL/pgSQL. Its ALTER TABLE text is
        // executable SQL inside EXECUTE format(...), so removing string literals before
        // proving relocation would erase the very operation this gate must certify.
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", " ");
        var executableShape = Regex.Replace(withoutBlocks, @"--[^\r\n]*", " ");
        var directCode = Strip(raw);

        Assert.Matches(
            new Regex(
                @"EXECUTE\s+format\s*\(\s*'ALTER\s+TABLE\s+%I\.%I\s+SET\s+SCHEMA\s+%I'",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            executableShape);

        // Direct DDL/data-copy operations remain forbidden. Strip() is correct for
        // these assertions because comments and dynamic SQL literals must not satisfy
        // or accidentally trip a direct-statement rule.
        Assert.DoesNotMatch(new Regex(@"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase), directCode);
        Assert.DoesNotMatch(new Regex(@"\bINSERT\s+INTO\b", RegexOptions.IgnoreCase), directCode);
        Assert.DoesNotMatch(new Regex(@"\bSELECT\s+INTO\b", RegexOptions.IgnoreCase), directCode);
        Assert.DoesNotMatch(new Regex(@"\bDROP\s+TABLE\b", RegexOptions.IgnoreCase), directCode);

        // Nor may the convergence hide table-creation/copy/drop DDL inside EXECUTE format.
        Assert.DoesNotMatch(
            new Regex(
                @"EXECUTE\s+format\s*\(\s*'(?:CREATE\s+TABLE|INSERT\s+INTO|DROP\s+TABLE)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            executableShape);
    }
    [Fact]
    public void Convergence_is_existence_driven()
    {
        var code = Strip(ReadRepositoryFile(ConvergenceRelativePath));

        Assert.Contains("to_regclass", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_read_model_views_leave_no_public_compatibility_alias()
    {
        var code = Strip(ReadRepositoryFile(ViewAuthorityRelativePath));

        Assert.DoesNotMatch(
            new Regex(@"CREATE\s+(OR\s+REPLACE\s+)?VIEW\s+public\.", RegexOptions.IgnoreCase),
            code);

        Assert.Matches(
            new Regex(@"CREATE\s+OR\s+REPLACE\s+VIEW\s+ppiq_plant\.", RegexOptions.IgnoreCase),
            code);
    }

    [Fact]
    public void The_canvas_staging_fallback_is_the_governed_staging_schema()
    {
        var code = ReadRepositoryFile("Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs");

        Assert.DoesNotContain("\"dump_store\"", code, StringComparison.Ordinal);
        Assert.Contains("\"ppiq_staging\"", code, StringComparison.Ordinal);
    }

    private static PlantProcessDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=model_only;Username=model_only;Password=model_only")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    private static string ReadRepositoryFile(string relative)
    {
        var full = Path.Combine(FindRepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(full), "A governed artefact no longer exists: " + relative);

        return File.ReadAllText(full);
    }

    private static string Strip(string sql)
    {
        var withoutLiterals = Regex.Replace(sql, @"'(?:[^']|'')*'", "''");
        var withoutBlocks = Regex.Replace(withoutLiterals, @"/\*[\s\S]*?\*/", " ");
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
