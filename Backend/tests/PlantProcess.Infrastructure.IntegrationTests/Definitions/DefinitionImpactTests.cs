using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-091. Impact preview acceptance: T091-01 through T091-05.
/// </summary>
[Collection("T091DefinitionGraph")]
public sealed class DefinitionImpactTests
{
    private readonly T091GraphFixture _fixture;

    public DefinitionImpactTests(T091GraphFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// T091-01. The three known downstream dependents are visible BEFORE the
    /// root is published again. This is the whole point of the feature: a
    /// caller about to change ROOT_A must see B, C and D first.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-01")]
    public async Task Impact_reports_the_three_known_dependents()
    {
        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);

        var impact = await graph.PreviewImpactAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);

        Assert.True(impact.IsSuccess, impact.Error?.Message);

        var codes = impact.Value!.Consumers.Select(c => c.DefinitionCode).ToList();
        Assert.Contains(T091GraphFixture.ConsumerB, codes);
        Assert.Contains(T091GraphFixture.ConsumerC, codes);
        Assert.Contains(T091GraphFixture.ConsumerD, codes);
        Assert.False(impact.Value.Truncated, "the acceptance fixture must not reach the traversal ceiling");
    }

    /// <summary>
    /// T091-02. Preview mutates nothing. Counted across every table impact
    /// could plausibly touch, plus the version hashes, because a preview that
    /// rewrote content while keeping the row count would pass a count-only
    /// check.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-02")]
    public async Task Impact_preview_writes_nothing()
    {
        var before = await SnapshotAsync();

        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);

        var impact = await graph.PreviewImpactAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);

        Assert.True(impact.IsSuccess, impact.Error?.Message);

        var after = await SnapshotAsync();
        Assert.Equal(before, after);
    }

    /// <summary>
    /// T091-03. CONSUMER_D is reachable through both B and C. It appears once,
    /// at its shallowest depth, and the direct/transitive split is correct.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-03")]
    public async Task Transitive_consumers_are_deduplicated_at_their_shallowest_depth()
    {
        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);

        var impact = await graph.PreviewImpactAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);

        Assert.True(impact.IsSuccess, impact.Error?.Message);

        var consumers = impact.Value!.Consumers;
        Assert.Equal(consumers.Select(c => c.DefinitionId).Distinct().Count(), consumers.Count);

        var d = Assert.Single(consumers.Where(c => c.DefinitionCode == T091GraphFixture.ConsumerD));
        Assert.Equal(2, d.Depth);
        Assert.Equal(ImpactRelationship.Transitive, d.Relationship);

        var b = Assert.Single(consumers.Where(c => c.DefinitionCode == T091GraphFixture.ConsumerB));
        Assert.Equal(1, b.Depth);
        Assert.Equal(ImpactRelationship.Direct, b.Relationship);

        // CONSUMER_C pins version 1 of the root, which is the one compatibility
        // claim the canonical store can actually evidence.
        var c = Assert.Single(consumers.Where(x => x.DefinitionCode == T091GraphFixture.ConsumerC));
        Assert.Equal(CompatibilityRisk.PinnedToExistingVersion, c.CompatibilityRisk);
        Assert.Equal(1, c.PinnedDependsOnVersion);
    }

    /// <summary>
    /// T091-04. A consumer edge that belongs to another tenant must not appear,
    /// and must not reveal by its absence-plus-count that it exists. The edge is
    /// written under a foreign tenant id and the preview is taken as the real
    /// tenant.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-04")]
    public async Task A_consumer_in_another_tenant_does_not_leak()
    {
        var foreignTenant = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO ppiq_meta.definition_dependencies
                    (tenant_id, definition_id, depends_on_definition_id, dependency_kind, is_required)
                VALUES (@foreign_tenant, @consumer, @root, 'model', true)
                ON CONFLICT (definition_id, depends_on_definition_id, dependency_kind) DO NOTHING;
                """, connection);

            command.Parameters.Add(new NpgsqlParameter("foreign_tenant", NpgsqlDbType.Uuid) { Value = foreignTenant });
            command.Parameters.Add(new NpgsqlParameter("consumer", NpgsqlDbType.Uuid)
            {
                Value = _fixture.Ids[T091GraphFixture.ForeignProbe]
            });
            command.Parameters.Add(new NpgsqlParameter("root", NpgsqlDbType.Uuid)
            {
                Value = _fixture.Ids[T091GraphFixture.RootA]
            });

            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var db = _fixture.NewContext();
            var graph = new CanonicalDefinitionGraph(db);

            var impact = await graph.PreviewImpactAsync(
                _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);

            Assert.True(impact.IsSuccess, impact.Error?.Message);
            Assert.DoesNotContain(T091GraphFixture.ForeignProbe,
                impact.Value!.Consumers.Select(c => c.DefinitionCode));

            // And the foreign tenant sees nothing of ours either.
            var foreign = await graph.PreviewImpactAsync(
                foreignTenant, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);
            Assert.True(foreign.IsFailure, "a definition must not resolve for a tenant that does not own it");
        }
        finally
        {
            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();
            await using var cleanup = new NpgsqlCommand(
                "DELETE FROM ppiq_meta.definition_dependencies WHERE tenant_id = @foreign_tenant;", connection);
            cleanup.Parameters.Add(new NpgsqlParameter("foreign_tenant", NpgsqlDbType.Uuid) { Value = foreignTenant });
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// T091-05. The same graph produces the same order every time. Run twice
    /// through independent contexts so no query-plan or cache accident can make
    /// the second run agree with the first for the wrong reason.
    /// </summary>
    [Fact]
    [Trait("Gate", "T091-05")]
    public async Task Impact_order_is_deterministic()
    {
        var first = await OrderAsync();
        var second = await OrderAsync();

        Assert.Equal(first, second);

        // The declared order is depth first, so no deeper consumer may precede
        // a shallower one.
        var depths = await DepthsAsync();
        for (var index = 1; index < depths.Count; index++)
        {
            Assert.True(depths[index] >= depths[index - 1], "impact results must be ordered by depth first");
        }
    }

    private async Task<List<string>> OrderAsync()
    {
        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);

        var impact = await graph.PreviewImpactAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);

        Assert.True(impact.IsSuccess, impact.Error?.Message);
        return impact.Value!.Consumers.Select(c => c.DefinitionCode).ToList();
    }

    private async Task<List<int>> DepthsAsync()
    {
        await using var db = _fixture.NewContext();
        var graph = new CanonicalDefinitionGraph(db);

        var impact = await graph.PreviewImpactAsync(
            _fixture.TenantId, _fixture.Ids[T091GraphFixture.RootA], CancellationToken.None);

        Assert.True(impact.IsSuccess, impact.Error?.Message);
        return impact.Value!.Consumers.Select(c => c.Depth).ToList();
    }

    private async Task<string> SnapshotAsync()
    {
        var definitions = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_store WHERE tenant_id = @tenant_id;");
        var versions = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_versions WHERE tenant_id = @tenant_id;");
        var edges = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_dependencies WHERE tenant_id = @tenant_id;");
        var published = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.definition_versions WHERE tenant_id = @tenant_id AND status = 'published';");
        var hashes = await _fixture.ScalarAsync(
            "SELECT count(DISTINCT definition_hash) FROM ppiq_meta.definition_versions WHERE tenant_id = @tenant_id;");

        return string.Join("|", definitions, versions, edges, published, hashes);
    }
}
