using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// PPIQ T-090 static authority gates.
///
/// MIGRATION OWNERSHIP IS EXPLICIT HERE. Script 831 owns the canonical
/// definition-kind CHECK and the version status contract. Script 832 owns the
/// detail tables, their fields and their column types. No assertion in this
/// file says "the schema" - each names the migration that actually owns the
/// fact it checks.
///
/// EVERY SCAN PROVES IT SCANNED. A gate that reports zero without proving it
/// read anything is a broken scan reporting success, which is how a reference
/// scanner once reported a clean codebase across 1189 files it never opened.
/// </summary>
public sealed class DefinitionContractConvergenceTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static string Read(string relativePath)
    {
        var full = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), $"Expected {relativePath} to exist. Without it this gate proves nothing.");
        var text = File.ReadAllText(full);
        Assert.False(string.IsNullOrWhiteSpace(text), $"{relativePath} is empty; the scan would be vacuous.");
        return text;
    }

    private static string DefinitionStoreSql() => Read("Backend/database/scripts/831_definition_store.sql");

    private static string DetailSql() => Read("Backend/database/scripts/832_definition_contract_convergence.sql");

    // ------------------------------------------------------------ KIND_AUTHORITY

    /// <summary>
    /// The enum, the registry and script 831's CHECK are three records of one
    /// fact. Two records of one fact is the defect that cost T-089 two runs;
    /// three records without a gate would be worse.
    /// </summary>
    [Fact]
    [Trait("Gate", "KIND_AUTHORITY")]
    public void The_enum_the_registry_and_script_831_declare_the_same_sixteen_kinds()
    {
        var sql = DefinitionStoreSql();
        var check = Regex.Match(sql, @"ck_definition_store_kind CHECK \(definition_kind IN \((?<body>.*?)\)\)",
            RegexOptions.Singleline);
        Assert.True(check.Success, "Script 831 no longer declares ck_definition_store_kind in a readable form.");

        var declared = Regex.Matches(check.Groups["body"].Value, @"'(?<kind>[a-z_]+)'")
            .Select(m => m.Groups["kind"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(16, declared.Count);

        var registry = DefinitionKindRegistry.Contracts.Select(c => c.StorageKind).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(16, registry.Count);
        Assert.True(declared.SetEquals(registry),
            "Registry and script 831 disagree: " + string.Join(", ", declared.Except(registry).Concat(registry.Except(declared))));

        var enumNames = Enum.GetValues<DefinitionKind>().ToHashSet();
        Assert.Equal(16, enumNames.Count);
        Assert.Equal(16, DefinitionKindRegistry.Contracts.Select(c => c.Kind).Distinct().Count());
    }

    /// <summary>
    /// Numeric compatibility is a separate property from the literal set. An
    /// earlier generated enum kept all sixteen names while renumbering eight of
    /// the eleven historic members, and the set check passed.
    /// </summary>
    [Fact]
    [Trait("Gate", "KIND_NUMERIC_COMPAT")]
    public void Historic_enum_values_did_not_move()
    {
        var frozen = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Transformation"] = 1, ["Page"] = 2, ["Widget"] = 3, ["Analysis"] = 4,
            ["Model"] = 5, ["LogRule"] = 6, ["MasterDimension"] = 7, ["MasterMeasure"] = 8,
            ["Filter"] = 9, ["Hierarchy"] = 10, ["Bookmark"] = 11,
        };

        foreach (var (name, value) in frozen)
        {
            Assert.True(Enum.TryParse<DefinitionKind>(name, out var parsed), $"{name} disappeared from DefinitionKind.");
            Assert.Equal(value, (int)parsed);
        }

        var added = Enum.GetValues<DefinitionKind>()
            .Where(k => !frozen.ContainsKey(k.ToString()))
            .Select(k => (int)k)
            .OrderBy(v => v)
            .ToList();

        Assert.Equal(new[] { 12, 13, 14, 15, 16 }, added);
        Assert.Equal(16, Enum.GetValues<DefinitionKind>().Select(k => (int)k).Distinct().Count());
    }

    [Fact]
    [Trait("Gate", "SURFACE_MAPPING")]
    public void Every_kind_maps_to_exactly_one_declared_surface()
    {
        var counts = DefinitionKindRegistry.Contracts
            .GroupBy(c => c.Surface)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(1, counts["S1"]);
        Assert.Equal(9, counts["S2"]);
        Assert.Equal(4, counts["S3"]);
        Assert.Equal(1, counts["S4"]);
        Assert.Equal(1, counts["S5"]);

        var surfaces = DefinitionStoreSql();
        Assert.Contains("surface IN ('S1','S2','S3','S4','S5')", surfaces, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- DETAIL_SCHEMA_DRIFT

    /// <summary>
    /// Bidirectional. A registry field that script 832 does not create is a
    /// write that will fail at runtime; a column 832 creates that the registry
    /// does not declare would become writable by accident if the physical
    /// catalogue were ever consulted instead.
    /// </summary>
    [Fact]
    [Trait("Gate", "DETAIL_SCHEMA_DRIFT")]
    public void Registry_detail_fields_and_script_832_agree_in_both_directions()
    {
        var tables = ParseDetailTables(DetailSql());
        Assert.True(tables.Count >= 11, $"Expected at least eleven detail tables in script 832, found {tables.Count}.");

        var checkedTables = 0;

        foreach (var contract in DefinitionKindRegistry.Contracts.Where(c => c.DetailTable is not null))
        {
            Assert.True(tables.ContainsKey(contract.DetailTable!),
                $"Registry declares {contract.DetailTable} but script 832 does not create it.");

            var physical = tables[contract.DetailTable!]
                .Where(c => !DefinitionKindRegistry.InfrastructureColumns.Contains(c.Key, StringComparer.Ordinal))
                .Select(c => c.Key)
                .ToHashSet(StringComparer.Ordinal);

            var declared = contract.WritableFields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);

            Assert.True(declared.SetEquals(physical),
                $"{contract.DetailTable}: registry-only [{string.Join(", ", declared.Except(physical))}] " +
                $"physical-only [{string.Join(", ", physical.Except(declared))}]");

            checkedTables++;
        }

        Assert.Equal(10, checkedTables);
    }

    [Fact]
    [Trait("Gate", "PERSISTENCE_TYPE_DRIFT")]
    public void Declared_storage_types_match_script_832_column_types()
    {
        var tables = ParseDetailTables(DetailSql());
        var compared = 0;

        foreach (var contract in DefinitionKindRegistry.Contracts.Where(c => c.DetailTable is not null))
        {
            foreach (var field in contract.WritableFields)
            {
                Assert.True(tables[contract.DetailTable!].TryGetValue(field.Name, out var physical),
                    $"{contract.DetailTable}.{field.Name} is declared by the registry and absent from script 832.");

                Assert.Equal(field.Storage, physical);
                compared++;
            }
        }

        Assert.True(compared >= 50, $"Only {compared} fields compared; the scan is too thin to be meaningful.");
    }

    /// <summary>
    /// The ten SM-06 fields must exist on outcome_details with the composite key
    /// that makes outcome_code unique WITHIN a version rather than across the
    /// store. definition_version_id alone must NOT be unique - several outcomes
    /// legitimately share one semantic contract.
    /// </summary>
    [Fact]
    [Trait("Gate", "SM06_SCHEMA")]
    public void Outcome_details_carries_all_ten_frozen_fields_keyed_within_a_version()
    {
        var sql = DetailSql();
        var tables = ParseDetailTables(sql);

        Assert.True(tables.ContainsKey("outcome_details"), "Script 832 does not create outcome_details.");

        foreach (var field in DefinitionKindRegistry.OutcomeFields)
        {
            Assert.True(tables["outcome_details"].ContainsKey(field),
                $"outcome_details is missing the frozen SM-06 field {field}.");
        }

        Assert.Contains("PRIMARY KEY (definition_version_id, outcome_code)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE (definition_version_id)", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- ARCHITECTURE

    [Fact]
    [Trait("Gate", "CANONICAL_IDENTITY")]
    public void The_writer_never_synthesises_a_tenant_identity()
    {
        var writer = Read("Backend/PlantProcess.Infrastructure/Definitions/CanonicalDefinitionWriter.cs");
        var code = StripComments(writer);

        Assert.DoesNotContain("MD5", code, StringComparison.Ordinal);
        Assert.DoesNotContain("TenantIdOf", code, StringComparison.Ordinal);
        Assert.Contains("write.TenantId == Guid.Empty", code, StringComparison.Ordinal);
        Assert.Contains("write.OwnerId == Guid.Empty", code, StringComparison.Ordinal);
        Assert.Contains("owner_id", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two properties, not one. The writer must own no transaction AND must
    /// refuse to mutate without the caller's. A zero-occurrence scan proves only
    /// the first; the execution gates prove the second.
    /// </summary>
    [Fact]
    [Trait("Gate", "AMBIENT_TX_REQUIRED")]
    public void The_writer_owns_no_transaction_and_demands_the_callers()
    {
        var code = StripComments(Read("Backend/PlantProcess.Infrastructure/Definitions/CanonicalDefinitionWriter.cs"));

        Assert.DoesNotContain("BeginTransaction", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".CommitAsync", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".RollbackAsync", code, StringComparison.Ordinal);
        Assert.Contains("RequireTransaction()", code, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", code, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Gate", "SEMANTIC_AUTHORITY")]
    public void The_writer_does_not_expand_writable_fields_from_the_catalogue()
    {
        var code = StripComments(Read("Backend/PlantProcess.Infrastructure/Definitions/CanonicalDefinitionWriter.cs"));

        Assert.DoesNotContain("information_schema", code, StringComparison.Ordinal);
        Assert.Contains("contract.TryField", code, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Gate", "SM06_SENTINEL_SCOPE")]
    public void The_sentinel_is_compared_exactly_and_never_by_substring()
    {
        var code = StripComments(Read("Backend/PlantProcess.Infrastructure/Definitions/CanonicalDefinitionWriter.cs"));

        Assert.DoesNotContain("Contains(DefinitionKindRegistry.MigratedUnknown", code, StringComparison.Ordinal);
        Assert.Contains("IsUnknownSentinel", code, StringComparison.Ordinal);
        Assert.False(DefinitionKindRegistry.IsUnknownSentinel("legacy_migrated_unknown_mapping_v2"));
        Assert.True(DefinitionKindRegistry.IsUnknownSentinel("  migrated_unknown  "));
    }

    /// <summary>
    /// The public service must not choose behaviour by kind, and must not keep
    /// the widget-only refusal the canonical store made obsolete.
    /// </summary>
    [Fact]
    [Trait("Gate", "GENERIC_SERVICE")]
    public void The_definition_service_has_no_per_kind_persistence_branch()
    {
        var code = StripComments(Read("Backend/PlantProcess.Infrastructure/Definitions/DefinitionService.cs"));

        Assert.DoesNotContain("OnlyWidget", code, StringComparison.Ordinal);
        Assert.DoesNotContain("switch (kind", code, StringComparison.Ordinal);
        Assert.DoesNotContain("case DefinitionKind.", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The compatibility constructor must build the canonical collaborators, not
    /// make them optional. A nullable dependency would leave a shape where
    /// omitting the writer meant skipping the canonical write.
    /// </summary>
    [Fact]
    [Trait("Gate", "LEGACY_CONSTRUCTOR_CANONICAL_BEHAVIOR")]
    public void The_compatibility_constructor_has_no_optional_canonical_dependency()
    {
        var code = StripComments(Read("Backend/PlantProcess.Infrastructure/Definitions/DefinitionService.cs"));

        Assert.DoesNotContain("ICanonicalDefinitionWriter? writer", code, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalIdentityResolver? identity", code, StringComparison.Ordinal);

        var constructors = typeof(DefinitionService).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(2, constructors.Length);
        Assert.Contains(constructors, c => c.GetParameters().Length == 1);
        Assert.Contains(constructors, c => c.GetParameters().Length == 3);
        Assert.DoesNotContain(constructors, c => c.GetParameters().Any(p => p.IsOptional));
    }

    [Fact]
    [Trait("Gate", "SINGLE_VERSION_PERSISTENCE")]
    public void No_source_still_treats_the_retired_version_table_as_a_definition_authority()
    {
        var roots = new[] { "Backend/PlantProcess.Api", "Backend/PlantProcess.Application",
                            "Backend/PlantProcess.Infrastructure", "Backend/PlantProcess.Domain" };
        var offenders = new List<string>();
        var scanned = 0;
        var lines = 0;
        var excluded = 0;
        var skippedGenerated = 0;

        foreach (var root in roots)
        {
            var full = Path.Combine(RepositoryRoot, root.Replace('/', Path.DirectorySeparatorChar));
            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                // EF Migrations are applied history and a generated snapshot.
                // A migration records what already ran against real databases;
                // editing one to remove a table name would falsify that record,
                // and the snapshot regenerates from the model rather than being
                // authored. Neither is a definition authority.
                //
                // Excluded by directory segment, checked against the path the
                // repository actually uses, so a file merely NAMED like a
                // migration elsewhere is still scanned.
                var relative = Path.GetRelativePath(RepositoryRoot, file);
                if (relative.Replace('\\', '/').Contains("/Migrations/", StringComparison.Ordinal))
                {
                    skippedGenerated++;
                    continue;
                }

                var text = File.ReadAllText(file);
                scanned++;
                lines += text.Count(c => c == '\n');

                var code = StripComments(text);
                if (!code.Contains("ppiq_definition_versions", StringComparison.Ordinal))
                {
                    continue;
                }

                // StorageTopologyMap records which SCHEMA a table lives in. That
                // is physical placement, not definition authority, and it stays
                // true for as long as the table exists on an upgrade database.
                // Script 832 retires the table; removing the map entry belongs to
                // T-087, whose file this is. Excluded by exact path - never by a
                // pattern, which would hide a real offender that happened to
                // match it.
                if (Path.GetFileName(file).Equals("StorageTopologyMap.cs", StringComparison.Ordinal))
                {
                    excluded++;
                    continue;
                }

                offenders.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        // Non-vacuity: a zero that cannot prove it read anything is a broken scan.
        Assert.True(scanned > 100, $"Only {scanned} files opened; this scan is not covering the codebase.");
        Assert.True(lines > 10000, $"Only {lines} lines read; this scan is vacuous.");

        // Both exclusions must actually have applied. If either stops being
        // needed, this fails and the exclusion is removed deliberately rather
        // than lingering as dead permission.
        Assert.Equal(1, excluded);
        Assert.True(skippedGenerated > 0,
            "No generated migration file was skipped; the exclusion no longer describes the tree.");
        Assert.Empty(offenders);
    }

    // ---------------------------------------------------------------- internals

    private static Dictionary<string, Dictionary<string, DefinitionKindRegistry.StorageType>> ParseDetailTables(string sql)
    {
        var map = new[]
        {
            ("jsonb", DefinitionKindRegistry.StorageType.Json),
            ("text", DefinitionKindRegistry.StorageType.Text),
            ("uuid", DefinitionKindRegistry.StorageType.Uuid),
            ("integer", DefinitionKindRegistry.StorageType.Integer),
            ("boolean", DefinitionKindRegistry.StorageType.Boolean),
        };

        var tables = new Dictionary<string, Dictionary<string, DefinitionKindRegistry.StorageType>>(StringComparer.Ordinal);

        foreach (Match table in Regex.Matches(sql,
            @"CREATE TABLE IF NOT EXISTS ppiq_meta\.(?<name>\w+_details) \((?<body>.*?)\n\);",
            RegexOptions.Singleline))
        {
            var columns = new Dictionary<string, DefinitionKindRegistry.StorageType>(StringComparer.Ordinal);
            var depth = 0;
            var inConstraint = false;

            foreach (var raw in table.Groups["body"].Value.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0 && !line.StartsWith("--", StringComparison.Ordinal))
                {
                    if (depth == 0 && !inConstraint)
                    {
                        if (line.StartsWith("CONSTRAINT", StringComparison.Ordinal))
                        {
                            inConstraint = true;
                        }
                        else
                        {
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && Regex.IsMatch(parts[0], "^[a-z_]+$"))
                            {
                                var baseType = Regex.Replace(parts[1], @"\(.*", string.Empty);
                                var storage = map.FirstOrDefault(m => m.Item1 == baseType);
                                columns[parts[0]] = storage.Item1 is null
                                    ? DefinitionKindRegistry.StorageType.Text
                                    : storage.Item2;
                            }
                        }
                    }

                    depth += line.Count(c => c == '(') - line.Count(c => c == ')');
                    if (inConstraint && depth <= 0)
                    {
                        inConstraint = false;
                        depth = 0;
                    }
                }
            }

            tables[table.Groups["name"].Value] = columns;
        }

        return tables;
    }

    /// <summary>
    /// Guards must scan comment-free text. A guard that reads its own
    /// explanatory prose finds itself and reports a defect that does not exist.
    /// </summary>
    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join('\n', withoutBlocks
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
