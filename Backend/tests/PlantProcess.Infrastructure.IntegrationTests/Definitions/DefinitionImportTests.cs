using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-091. Import acceptance: T091-10 through T091-15.
///
/// ONE DISPOSABLE DATABASE, NOT TWO. The clean-room proof needs an empty
/// SEMANTIC target, not a second schema replay. The source graph is built and
/// exported inside a transaction that is then rolled back, which returns the
/// disposable database to its clean semantic baseline; the baseline is asserted
/// rather than assumed, and only then is the artifact imported. Two full
/// canonical replays would cost minutes to prove the same thing.
/// </summary>
[Collection("T091DefinitionGraph")]
public sealed class DefinitionImportTests
{
    private readonly T091GraphFixture _fixture;

    public DefinitionImportTests(T091GraphFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// T091-10 and T091-11. Clean-database round trip and second-import
    /// idempotence, in one disposable database.
    ///
    /// Phase B builds and exports inside a transaction; Phase C rolls it back
    /// and proves the semantic baseline is empty; Phase D imports; Phase E
    /// re-exports and compares canonical semantics. Physical ids are never
    /// compared - the clean database allocates its own and that is correct.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-10")]
    public async Task Export_import_into_a_clean_database_round_trips_semantically()
    {
        await using var probe = await DisposableDatabase.CreateAsync();

        DefinitionArtifact exported;

        // ---- Phase B: build the source graph, export, then undo it ---------
        await using (var db = probe.NewContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var identity = new CanonicalIdentityResolver(db);
            var tenantId = await identity.ResolveTenantAsync(null, CancellationToken.None);
            var ownerId = await identity.ResolveOwnerAsync(null, CancellationToken.None);
            Assert.True(tenantId.HasValue && ownerId.HasValue, "the disposable database must carry provisioned identity");

            var rootId = await BuildSourceGraphAsync(db, tenantId!.Value, ownerId!.Value);

            var graph = new CanonicalDefinitionGraph(db);
            var exporter = new DefinitionExporter(db, graph);

            var result = await exporter.ExportAsync(tenantId.Value, rootId, null, CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            exported = result.Value!;

            await transaction.RollbackAsync();
        }

        // ---- Phase C: the target is semantically clean --------------------
        Assert.Equal(0, await probe.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%';"));
        Assert.Equal(0, await probe.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_versions v " +
            "JOIN ppiq_meta.definition_store s ON s.id = v.definition_id WHERE s.definition_code LIKE 't091_%';"));
        Assert.Equal(0, await probe.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_dependencies d " +
            "JOIN ppiq_meta.definition_store s ON s.id = d.definition_id WHERE s.definition_code LIKE 't091_%';"));

        // ---- Phase D: import ----------------------------------------------
        var imported = await ImportAsync(probe, exported);
        Assert.True(imported.IsSuccess, imported.Error?.Message);
        Assert.True(imported.Value!.DefinitionsWritten > 0);

        // ---- Phase E: re-export and compare semantics ---------------------
        DefinitionArtifact reexported;
        await using (var db = probe.NewContext())
        {
            var graph = new CanonicalDefinitionGraph(db);
            var exporter = new DefinitionExporter(db, graph);
            var identity = new CanonicalIdentityResolver(db);
            var tenantId = (await identity.ResolveTenantAsync(null, CancellationToken.None))!.Value;

            var result = await exporter.ExportAsync(
                tenantId, imported.Value.RootDefinitionId, null, CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            reexported = result.Value!;
        }

        // SELF-DIAGNOSING. The exact provenance-free canonical structures that
        // feed the semantic hash are written to evidence, together with a
        // path-level diff, BEFORE the assertion. A failure that prints two SHA
        // values tells nobody which field of which definition moved.
        var sourceCanonical = DefinitionArtifactCanonicalizer.ToCanonicalJson(exported);
        var importedCanonical = DefinitionArtifactCanonicalizer.ToCanonicalJson(reexported);
        var differences = DefinitionArtifactCanonicalizer.SemanticDiff(exported, reexported);

        var evidenceRoot = Environment.GetEnvironmentVariable("PPIQ_T091_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidenceRoot) && Directory.Exists(evidenceRoot))
        {
            await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "roundtrip_source_semantic.json"), sourceCanonical);
            await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "roundtrip_imported_semantic.json"), importedCanonical);
            await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "roundtrip_semantic_diff.txt"),
                differences.Count == 0 ? "NO SEMANTIC DIFFERENCE" : string.Join(Environment.NewLine, differences));
        }

        Assert.True(
            string.Equals(sourceCanonical, importedCanonical, StringComparison.Ordinal),
            "semantic round trip differs at " + differences.Count + " path(s):" + Environment.NewLine +
            string.Join(Environment.NewLine, differences));

        Assert.Equal(
            DefinitionArtifactCanonicalizer.SemanticHash(exported),
            DefinitionArtifactCanonicalizer.SemanticHash(reexported));

        // Focused structural evidence alongside the hash, so a hash that
        // matched for the wrong reason would still be caught.
        Assert.Equal(exported.Definitions.Count, reexported.Definitions.Count);
        Assert.Equal(exported.Dependencies.Count, reexported.Dependencies.Count);
        Assert.Equal(
            exported.Definitions.Select(d => d.DefinitionCode).OrderBy(c => c, StringComparer.Ordinal),
            reexported.Definitions.Select(d => d.DefinitionCode).OrderBy(c => c, StringComparer.Ordinal));

        // ---- T091-11: the identical artifact imported again ---------------
        var storeBefore = await probe.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_store;");
        var versionsBefore = await probe.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_versions;");
        var edgesBefore = await probe.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_dependencies;");

        var second = await ImportAsync(probe, exported);
        Assert.True(second.IsSuccess, second.Error?.Message);

        Assert.Equal(storeBefore, await probe.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_store;"));
        Assert.Equal(versionsBefore, await probe.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_versions;"));
        Assert.Equal(edgesBefore, await probe.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_dependencies;"));
        Assert.Equal(0, second.Value!.DefinitionsWritten);
    }

    /// <summary>
    /// T091-12. An artifact whose graph references a definition it does not
    /// carry is refused before anything is written. Proven by counting the
    /// store before and after, not by trusting the refusal message.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-12")]
    public async Task A_missing_dependency_refuses_without_partial_installation()
    {
        var artifact = await ExportFixtureAsync();

        var broken = artifact with
        {
            Dependencies = artifact.Dependencies
                .Append(new ArtifactDependency(artifact.RootRef, "d9999", "master_item", true, 1))
                .ToList()
        };

        await AssertRefusedWithoutMutationAsync(broken, "does not carry");
    }

    /// <summary>
    /// T091-13. A definition code that already exists with different semantic
    /// content is a conflict, never an overwrite. The existing definition must
    /// still hold its own content afterwards.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-13")]
    public async Task A_conflicting_existing_definition_is_refused_rather_than_overwritten()
    {
        var artifact = await ExportFixtureAsync();

        // Same codes, different semantic content: what a stale artifact from
        // another environment looks like.
        var conflicting = artifact with
        {
            Definitions = artifact.Definitions
                .Select(d => d with
                {
                    ContentJson = "{\"role\":\"conflicting-content\"}",
                    DefinitionHash = "0000000000000000000000000000000000000000000000000000000000000000",
                })
                .ToList()
        };

        var hashesBefore = await FixtureHashesAsync();

        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);
        var importer = new DefinitionImporter(db, writer, new DefinitionExporter(db, new CanonicalDefinitionGraph(db)));

        var result = await importer.ImportAsync(
            _fixture.TenantId, _fixture.OwnerId, conflicting, CancellationToken.None);

        Assert.True(result.IsFailure, "an established definition must not be silently rewritten by an import");
        Assert.Contains("IMPORT_CONFLICT", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("SemanticContentConflict", result.Error.Message, StringComparison.Ordinal);

        Assert.Equal(hashesBefore, await FixtureHashesAsync());
    }

    /// <summary>
    /// T091-14. A cyclic package and an unknown format version are both refused
    /// before mutation, by the application, with the database trigger left as
    /// the backstop it is meant to be.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-14")]
    public async Task A_cyclic_or_unreadable_package_refuses_before_mutation()
    {
        var artifact = await ExportFixtureAsync();
        var refs = artifact.Definitions.Select(d => d.Ref).OrderBy(r => r, StringComparer.Ordinal).ToList();

        var cyclic = artifact with
        {
            Dependencies = new List<ArtifactDependency>
            {
                new(refs[0], refs[1], "master_item", true, 1),
                new(refs[1], refs[0], "master_item", true, 1),
            }
        };

        await AssertRefusedWithoutMutationAsync(cyclic, "cycle");

        var future = artifact with { FormatVersion = DefinitionArtifact.CurrentFormatVersion + 7 };
        await AssertRefusedWithoutMutationAsync(future, "format version");

        Assert.Null(DefinitionArtifactCanonicalizer.FromTransportJson("{ this is not json"));
    }

    /// <summary>
    /// T091-15. T-091 introduced no second authority. The definitions, versions
    /// and edges an import produces are in the T-089/T-090 tables and nowhere
    /// else, and no portability or impact-cache table exists.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-15")]
    public async Task Imported_semantics_live_only_in_the_canonical_authority()
    {
        // NAMED, NOT SWEPT. An earlier version of this gate matched any table
        // containing 'impact' and caught two long-standing product tables that
        // have nothing to do with T-091. A guard that fails on somebody else's
        // correct work teaches people to ignore it.
        var forbidden = await _fixture.ScalarAsync(
            """
            SELECT count(*) FROM information_schema.tables
             WHERE table_schema = 'ppiq_meta'
               AND table_name IN (
                   'definition_impact_cache', 'definition_impacts', 'definition_portability',
                   'definition_artifacts', 'definition_exports', 'definition_imports',
                   'definition_dependency_cache', 'portable_definitions');
            """);

        Assert.Equal(0, forbidden);

        // The fixture graph was written entirely through the canonical writer,
        // so every one of its definitions is addressable in definition_store
        // and every edge in the T-089 dependency table.
        Assert.Equal(_fixture.Ids.Count, await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_store " +
            "WHERE tenant_id = @tenant_id AND definition_code LIKE 't091_%';"));

        Assert.Equal(6, await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_dependencies d " +
            "JOIN ppiq_meta.definition_store s ON s.id = d.definition_id " +
            "WHERE d.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%';"));
    }

    private async Task AssertRefusedWithoutMutationAsync(DefinitionArtifact artifact, string expected)
    {
        var before = await CountsAsync();

        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);
        var importer = new DefinitionImporter(db, writer, new DefinitionExporter(db, new CanonicalDefinitionGraph(db)));

        var result = await importer.ImportAsync(
            _fixture.TenantId, _fixture.OwnerId, artifact, CancellationToken.None);

        Assert.True(result.IsFailure, "expected a refusal mentioning: " + expected);
        Assert.Contains(expected, result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountsAsync());
    }

    private async Task<string> CountsAsync()
    {
        var store = await _fixture.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_store;");
        var versions = await _fixture.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_versions;");
        var edges = await _fixture.ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_dependencies;");
        return store + "|" + versions + "|" + edges;
    }

    private async Task<string> FixtureHashesAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT string_agg(v.definition_hash, ',' ORDER BY s.definition_code, v.version_number)
              FROM ppiq_meta.definition_versions v
              JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
             WHERE s.definition_code LIKE 't091_%';
            """, connection);

        var value = await command.ExecuteScalarAsync();
        return value is null || value == DBNull.Value ? string.Empty : (string)value;
    }

    private async Task<DefinitionArtifact> ExportFixtureAsync()
    {
        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);
        var exporter = new DefinitionExporter(db, graph);

        var exported = await exporter.ExportAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], null, CancellationToken.None);

        Assert.True(exported.IsSuccess, exported.Error?.Message);
        return exported.Value!;
    }

    private static async Task<ApplicationResultCarrier> ImportAsync(DisposableDatabase probe, DefinitionArtifact artifact)
    {
        await using var db = probe.NewContext();
        var identity = new CanonicalIdentityResolver(db);
        var tenantId = (await identity.ResolveTenantAsync(null, CancellationToken.None))!.Value;
        var ownerId = (await identity.ResolveOwnerAsync(null, CancellationToken.None))!.Value;

        var writer = new CanonicalDefinitionWriter(db);
        var importer = new DefinitionImporter(db, writer, new DefinitionExporter(db, new CanonicalDefinitionGraph(db)));

        var result = await importer.ImportAsync(tenantId, ownerId, artifact, CancellationToken.None);
        return new ApplicationResultCarrier(result.IsSuccess, result.IsFailure, result.Error, result.IsSuccess ? result.Value : null);
    }

    private static async Task<Guid> BuildSourceGraphAsync(PlantProcessDbContext db, Guid tenantId, Guid ownerId)
    {
        var writer = new CanonicalDefinitionWriter(db);

        var sourceX = await WriteAsync(writer, DefinitionKind.Widget, tenantId, ownerId,
            T091GraphFixture.SourceX, "Source X", "{\"role\":\"upstream-widget\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["widget_kind"] = "chart",
                ["chart_type"] = "bar",
                ["dimension_code"] = "dim_neutral",
                ["measure_code"] = "mea_neutral",
            });

        var sourceY = await WriteAsync(writer, DefinitionKind.Analysis, tenantId, ownerId,
            T091GraphFixture.SourceY, "Source Y", "{\"role\":\"upstream-analysis\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome_code"] = "neutral_outcome",
                ["grain_code"] = "neutral_grain",
                ["method_code"] = "neutral_method",
            });

        var root = await WriteAsync(writer, DefinitionKind.Analysis, tenantId, ownerId,
            T091GraphFixture.RootA, "Root A", "{\"role\":\"root\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome_code"] = "root_outcome",
                ["grain_code"] = "neutral_grain",
                ["method_code"] = "neutral_method",
            });

        await EdgeAsync(db, tenantId, ownerId, root, sourceX, "master_item", true, 1);
        await EdgeAsync(db, tenantId, ownerId, root, sourceY, "feature_set", true, null);

        return root;
    }

    private static async Task<Guid> WriteAsync(
        CanonicalDefinitionWriter writer,
        DefinitionKind kind,
        Guid tenantId,
        Guid ownerId,
        string code,
        string name,
        string contentJson,
        IReadOnlyDictionary<string, object?> detail)
    {
        var result = await writer.WriteVersionAsync(new CanonicalDefinitionWrite(
            kind, tenantId, ownerId, code, name, contentJson,
            CanonicalVersionStatus.Published, detail), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!.DefinitionId;
    }

    private static async Task EdgeAsync(
        PlantProcessDbContext db, Guid tenantId, Guid ownerId,
        Guid from, Guid to, string dependencyKind, bool required, int? pinned)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO ppiq_meta.definition_dependencies
                (tenant_id, definition_id, depends_on_definition_id, depends_on_version,
                 dependency_kind, is_required, created_by)
            VALUES (@tenant_id, @definition_id, @depends_on_definition_id, @depends_on_version,
                    @dependency_kind, @is_required, @created_by)
            ON CONFLICT (definition_id, depends_on_definition_id, dependency_kind) DO NOTHING;
            """, connection);

        var transaction = db.Database.CurrentTransaction;
        if (transaction is not null)
        {
            command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        }

        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter("definition_id", NpgsqlDbType.Uuid) { Value = from });
        command.Parameters.Add(new NpgsqlParameter("depends_on_definition_id", NpgsqlDbType.Uuid) { Value = to });
        command.Parameters.Add(new NpgsqlParameter("dependency_kind", NpgsqlDbType.Text) { Value = dependencyKind });
        command.Parameters.Add(new NpgsqlParameter("is_required", NpgsqlDbType.Boolean) { Value = required });
        command.Parameters.Add(new NpgsqlParameter("created_by", NpgsqlDbType.Uuid) { Value = ownerId });
        command.Parameters.Add(new NpgsqlParameter("depends_on_version", NpgsqlDbType.Integer)
        {
            Value = pinned.HasValue ? pinned.Value : (object)DBNull.Value
        });

        await command.ExecuteNonQueryAsync();
    }

    private sealed record ApplicationResultCarrier(
        bool IsSuccess, bool IsFailure, ApplicationError? Error, DefinitionImportResult? Value);
}
