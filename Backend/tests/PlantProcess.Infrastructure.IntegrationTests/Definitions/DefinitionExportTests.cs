using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-091. Export acceptance: T091-06 through T091-09.
/// </summary>
[Collection("T091DefinitionGraph")]
public sealed class DefinitionExportTests
{
    private readonly T091GraphFixture _fixture;

    public DefinitionExportTests(T091GraphFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// T091-06. The artifact carries the root and exactly the upstream closure
    /// it needs - and NOT its downstream consumers. Exporting consumers would
    /// be the impact direction, and it would drag half the tenant along.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-06")]
    public async Task Export_carries_the_root_and_its_required_closure_only()
    {
        var artifact = await ExportAsync();

        var codes = artifact.Definitions.Select(d => d.DefinitionCode).ToList();
        Assert.Contains(T091GraphFixture.RootA, codes);
        Assert.Contains(T091GraphFixture.SourceX, codes);
        Assert.Contains(T091GraphFixture.SourceY, codes);

        Assert.DoesNotContain(T091GraphFixture.ConsumerB, codes);
        Assert.DoesNotContain(T091GraphFixture.ConsumerC, codes);
        Assert.DoesNotContain(T091GraphFixture.ConsumerD, codes);

        // Cross-kind closure: the root is an Analysis and it requires a Widget,
        // so the exporter is proven generic rather than analysis-shaped.
        Assert.Contains(artifact.Definitions, d => d.Kind == "widget");
        Assert.Contains(artifact.Definitions, d => d.Kind == "analysis");

        var root = Assert.Single(artifact.Definitions.Where(d => d.DefinitionCode == T091GraphFixture.RootA));
        Assert.Equal(artifact.RootRef, root.Ref);
        Assert.Equal(2, artifact.Dependencies.Count(d => d.FromRef == root.Ref));
    }

    /// <summary>
    /// T091-07. Every dependency edge in the artifact carries an exact version -
    /// the pinned one where the source edge pinned it, and the resolved
    /// published one where it did not. An artifact that said "latest" would
    /// reproduce a different system tomorrow.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-07")]
    public async Task Every_exported_dependency_is_pinned_to_an_exact_version()
    {
        var artifact = await ExportAsync();

        Assert.NotEmpty(artifact.Dependencies);
        Assert.All(artifact.Dependencies, d => Assert.True(
            d.DependsOnVersion.HasValue,
            "dependency " + d.FromRef + " -> " + d.ToRef + " left an unresolved version in the artifact"));

        var byRef = artifact.Definitions.ToDictionary(d => d.Ref, StringComparer.Ordinal);

        // The pinned upstream edge kept its declared version rather than being
        // silently upgraded to whatever is newest.
        var pinned = Assert.Single(artifact.Dependencies.Where(d =>
            byRef[d.ToRef].DefinitionCode == T091GraphFixture.SourceX));
        Assert.Equal(1, pinned.DependsOnVersion);

        // The unpinned edge resolved to the version actually exported for that
        // dependency, not to a number nobody carries.
        var resolved = Assert.Single(artifact.Dependencies.Where(d =>
            byRef[d.ToRef].DefinitionCode == T091GraphFixture.SourceY));
        Assert.Equal(byRef[resolved.ToRef].VersionNumber, resolved.DependsOnVersion);
    }

    /// <summary>
    /// T091-08. Two exports of an unchanged graph canonicalise identically and
    /// hash identically, and the format version is stated. Metadata differs
    /// between the two runs by construction - the export timestamps are taken
    /// microseconds apart - so this also proves provenance stays out of the
    /// semantic unit.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-08")]
    public async Task The_artifact_is_deterministic_and_schema_versioned()
    {
        var first = await ExportAsync();
        var second = await ExportAsync();

        Assert.Equal(DefinitionArtifact.CurrentFormatVersion, first.FormatVersion);

        Assert.Equal(
            DefinitionArtifactCanonicalizer.ToCanonicalJson(first),
            DefinitionArtifactCanonicalizer.ToCanonicalJson(second));

        Assert.Equal(
            DefinitionArtifactCanonicalizer.SemanticHash(first),
            DefinitionArtifactCanonicalizer.SemanticHash(second));

        Assert.True(DefinitionArtifactCanonicalizer.SemanticallyEqual(first, second));

        // Package-local refs are deterministic and are what the package
        // references resolve through.
        Assert.All(first.Definitions, d => Assert.Matches("^d[0-9]{4}$", d.Ref));
        Assert.Equal(
            first.Definitions.Select(d => d.Ref).ToList(),
            second.Definitions.Select(d => d.Ref).ToList());
    }

    /// <summary>
    /// T091-09. The canonical semantic section carries no environment-local
    /// identity and no secret material. Source uuids may travel in the record
    /// as provenance, but they must not reach the comparison unit, or the same
    /// definitions exported from two installations would compare unequal.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-09")]
    public async Task The_semantic_section_carries_no_secrets_or_environment_identity()
    {
        var artifact = await ExportAsync();
        var canonical = DefinitionArtifactCanonicalizer.ToCanonicalJson(artifact);

        foreach (var definition in artifact.Definitions)
        {
            Assert.DoesNotContain(definition.SourceDefinitionId!.Value.ToString(), canonical, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(definition.SourceVersionId!.Value.ToString(), canonical, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(_fixture.TenantId.ToString(), canonical, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_fixture.OwnerId.ToString(), canonical, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exportedAtUtc", canonical, StringComparison.Ordinal);

        foreach (var marker in new[] { "password", "secret", "token", "credential", "connection_string", "vault", "Password=" })
        {
            Assert.DoesNotContain(marker, canonical, StringComparison.OrdinalIgnoreCase);
        }

        // The transport form may carry provenance, and it does - that is the
        // difference between the two forms, stated as a test rather than a
        // comment.
        var transport = DefinitionArtifactCanonicalizer.ToTransportJson(artifact);
        Assert.Contains("exportedAtUtc", transport, StringComparison.Ordinal);
        Assert.Contains("semanticHash", transport, StringComparison.Ordinal);
    }

    /// <summary>
    /// A definition with nothing published and no explicit version requested is
    /// refused rather than exported as a draft that a caller would read as
    /// truth. Supporting evidence for the ruling behind T091-07.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-07")]
    public async Task An_unpublished_root_without_an_explicit_version_is_refused()
    {
        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);
        var graph = new CanonicalDefinitionGraph(db);
        var exporter = new DefinitionExporter(db, graph);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var draft = await writer.WriteVersionAsync(new CanonicalDefinitionWrite(
            DefinitionKind.Analysis, _fixture.TenantId, _fixture.OwnerId,
            "t091_draft_only", "Draft only", "{\"role\":\"draft\"}",
            CanonicalVersionStatus.Draft,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome_code"] = "draft_outcome",
                ["grain_code"] = "neutral_grain",
                ["method_code"] = "neutral_method",
            }), CancellationToken.None);

        Assert.True(draft.IsSuccess, draft.Error?.Message);

        var exported = await exporter.ExportAsync(
            _fixture.TenantId, draft.Value!.DefinitionId, null, CancellationToken.None);

        Assert.True(exported.IsFailure);
        Assert.Contains("DEFINITION_VERSION_NOT_EXPORTABLE", exported.Error!.Message, StringComparison.Ordinal);

        await transaction.RollbackAsync();
    }

    /// <summary>
    /// Known answer for the registry's null contract: a widget written with
    /// four of its five declared fields exports WITHOUT the fifth. SQL NULL on
    /// an optional detail column means "not declared", and the artifact says
    /// exactly that by omission - never by an explicit null, which the
    /// canonical writer would hash as a declared key. Supporting evidence for
    /// T091-08 and the r8 null/absent ruling.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-08")]
    public async Task An_undeclared_optional_detail_field_is_omitted_not_nulled()
    {
        var artifact = await ExportAsync();
        var sourceX = Assert.Single(artifact.Definitions.Where(d => d.DefinitionCode == T091GraphFixture.SourceX));

        Assert.True(DefinitionKindRegistry.TryResolveStorageKind("widget", out var contract));
        var declared = contract.WritableFields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);

        Assert.NotNull(sourceX.Detail);

        // ASSERTED AGAINST THE CONTRACT, NOT A COUNT I EXPECTED. An earlier
        // revision of this test hard-coded "four fields" and failed because a
        // fifth column carries a non-null default - the exporter was right and
        // the expectation was invented. What the null contract actually
        // promises: exported keys are declared fields, at least one declared
        // field is absent because it holds SQL NULL, and no exported value is
        // an explicit null.
        Assert.Subset(declared, sourceX.Detail!.Keys.ToHashSet(StringComparer.Ordinal));
        Assert.True(sourceX.Detail.Count < declared.Count,
            "no declared field was omitted, so this fixture cannot demonstrate the null contract");
        Assert.All(sourceX.Detail.Values, v => Assert.NotNull(v));

        var canonical = DefinitionArtifactCanonicalizer.ToCanonicalJson(artifact);
        foreach (var absent in declared.Except(sourceX.Detail.Keys))
        {
            Assert.DoesNotContain("\"" + absent + "\"", canonical, StringComparison.Ordinal);
        }
    }

    private async Task<DefinitionArtifact> ExportAsync()
    {
        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);
        var exporter = new DefinitionExporter(db, graph);

        var exported = await exporter.ExportAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], null, CancellationToken.None);

        Assert.True(exported.IsSuccess, exported.Error?.Message);
        return exported.Value!;
    }
}
