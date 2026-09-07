using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Relationships;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Infrastructure.Relationships;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Relationships;

/// <summary>
/// Resolution and planning, certified against the canonical model.
///
/// The whole chain, against the real database:
///
///     planner -> resolver -> relationship service -> real store -> PostgreSQL
///
/// Nothing here reaches past the service to read a relationship. The only
/// direct SQL is the cleanup that removes what this certification published,
/// and the preference flip - which is direct on purpose, because the point of
/// that proof is that preference is data rather than code.
///
/// ONE THING WORTH KNOWING BEFORE READING THE PURPOSES USED BELOW.
/// A newly published relationship is 'unproven', and there is as yet no action
/// that promotes it to 'validated' - that belongs to the validate control,
/// which is not built. So every published relationship is unproven, and RL02
/// means only manual exploration may traverse one. That is the contract working
/// as frozen, not a limitation being worked around: the positive proofs below
/// therefore use the exploration purpose, and an automated purpose is used
/// separately to prove the refusal is real rather than theoretical.
/// </summary>
public sealed class RelationshipResolverRuntimeCertificationTests : AuthenticatedApiTestBase
{
    public RelationshipResolverRuntimeCertificationTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static readonly Guid CertificationTenant = Guid.Parse("7e57c0de-0000-4000-8000-000000058001");

    private const string Explore = RelationshipConsumerPurposes.Explore;
    private const string Compiler = RelationshipConsumerPurposes.QueryCompiler;

    private sealed class FixedTenant : ITenantAccessor
    {
        private readonly Guid _tenantId;
        public FixedTenant(Guid tenantId) => _tenantId = tenantId;
        public Guid TenantId => _tenantId;
        public bool TryGetTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed record Vertical(
        RelationshipService Service,
        RelationshipResolver Resolver,
        RelationshipJoinPlanner Planner,
        NpgsqlDataSource DataSource);

    private static async Task<Vertical> BuildAsync()
    {
        Skip.IfNot(IsIntegrationDbReachable(),
            "Integration Postgres not reachable/authenticated on this machine; runs in CI.");

        var dataSource = NpgsqlDataSource.Create(ResolveIntegrationTestConnectionString());

        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            // Named before anything else. A run that cannot say which database
            // it proved something about has not proved anything.
            Assert.Equal("ppiq_app", conn.Database);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT to_regclass('ppiq_meta.plant_relationships') IS NOT NULL";
            Assert.True((bool)(await cmd.ExecuteScalarAsync())!,
                $"The canonical relationship model has not been applied to database '{conn.Database}'.");
        }

        var service = new RelationshipService(new NpgsqlRelationshipStore(dataSource), new FixedTenant(CertificationTenant));
        var resolver = new RelationshipResolver(service);
        return new Vertical(service, resolver, new RelationshipJoinPlanner(resolver, service), dataSource);
    }

    private static string Code(string suffix) => "RES_" + suffix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private static RelationshipDeclaration Declaration(string code, string left, string right, bool preferred = false) => new(
        code, left, right,
        RelationshipJoinTypes.Inner, RelationshipCardinalities.OneToMany,
        "unit", "unit", null, null, preferred,
        new List<RelationshipMemberDto>
        {
            new("site_key", "site_ref", 0),
            new("unit_key", "unit_ref", 1)
        });

    private static async Task CleanAsync(NpgsqlDataSource dataSource, params Guid[] definitionIds)
    {
        // Relationships first, definitions second: the key restricts, and a
        // cleanup that fights its own constraint leaves rows behind.
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "DELETE FROM ppiq_meta.plant_relationships WHERE tenant_id = @tenant AND source_definition_id = ANY(@defs)";
            cmd.Parameters.AddWithValue("tenant", CertificationTenant);
            cmd.Parameters.AddWithValue("defs", definitionIds);
            await cmd.ExecuteNonQueryAsync();
        }

        await RelationshipCertificationDefinitions.RemoveAsync(dataSource, definitionIds);
    }

    private static async Task SetPreferenceAsync(NpgsqlDataSource dataSource, string code, bool preferred)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE ppiq_meta.plant_relationships SET is_preferred_path = @preferred " +
            "WHERE tenant_id = @tenant AND relationship_code = @code AND retired_at_utc IS NULL";
        cmd.Parameters.AddWithValue("preferred", preferred);
        cmd.Parameters.AddWithValue("tenant", CertificationTenant);
        cmd.Parameters.AddWithValue("code", code);
        await cmd.ExecuteNonQueryAsync();
    }

    // ========================================================================
    // The frozen customer behaviour, end to end, on real rows.
    // ========================================================================

    [SkippableFact]
    public async Task Published_then_unpublished_then_restored_against_the_real_store()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = Code("CYCLE");
        var left = "res_cycle_left";
        var right = "res_cycle_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            // PUBLISHED. The consumer resolves and produces an executable plan.
            var published = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1,
                    new List<RelationshipDeclaration> { Declaration(code, left, right) }),
                CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);

            var working = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.True(working.IsSuccess, working.Error?.Message);
            Assert.True(working.Value!.Planned);
            Assert.Single(working.Value!.Steps);
            Assert.Equal(code, working.Value!.Steps[0].RelationshipCode);
            Assert.Equal(2, working.Value!.Steps[0].Predicates.Count);
            Assert.Equal("site_key", working.Value!.Steps[0].Predicates[0].FromColumn);
            Assert.Equal("site_ref", working.Value!.Steps[0].Predicates[0].ToColumn);
            Assert.Null(working.Value!.RefusalCode);

            // UNPUBLISHED. Named refusal, and NOT a partial plan: a half join
            // still executes and still returns numbers that look like an answer.
            var retired = await v.Service.RetireByDefinitionAsync(definitionId, CancellationToken.None);
            Assert.True(retired.IsSuccess);
            Assert.Equal(1, retired.Value);

            var refused = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.True(refused.IsSuccess);
            Assert.False(refused.Value!.Planned);
            Assert.Equal(RelationshipRefusalCodes.NoPath, refused.Value!.RefusalCode);
            Assert.Empty(refused.Value!.Steps);
            Assert.False(string.IsNullOrWhiteSpace(refused.Value!.RefusalMessage));

            // RESTORED. Works again, with no intervention anywhere else.
            var republished = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 2,
                    new List<RelationshipDeclaration> { Declaration(code, left, right) }),
                CancellationToken.None);
            Assert.True(republished.IsSuccess, republished.Error?.Message);

            var again = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.True(again.Value!.Planned);
            Assert.Single(again.Value!.Steps);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task A_multi_hop_path_resolves_and_plans_in_order_on_real_rows()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var first = Code("HOP1");
        var second = Code("HOP2");
        var a = "res_hop_a";
        var b = "res_hop_b";
        var c = "res_hop_c";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            var published = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1, new List<RelationshipDeclaration>
                {
                    Declaration(first, a, b),
                    Declaration(second, b, c)
                }),
                CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);

            var plan = await v.Planner.PlanAsync(a, c, Explore, CancellationToken.None);

            Assert.True(plan.Value!.Planned);
            Assert.Equal(2, plan.Value!.Steps.Count);
            Assert.Equal(a, plan.Value!.Steps[0].FromEntity);
            Assert.Equal(b, plan.Value!.Steps[0].ToEntity);
            Assert.Equal(c, plan.Value!.Steps[1].ToEntity);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Reverse_traversal_swaps_the_member_sides_on_real_rows()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = Code("REV");
        var left = "res_rev_left";
        var right = "res_rev_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1,
                    new List<RelationshipDeclaration> { Declaration(code, left, right) }),
                CancellationToken.None);

            var forward = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            var backward = await v.Planner.PlanAsync(right, left, Explore, CancellationToken.None);

            Assert.True(forward.Value!.Planned);
            Assert.True(backward.Value!.Planned);

            // Keeping the declared order while travelling the other way would
            // compare the left key to the left key: still rows, wrong rows.
            Assert.Equal("site_key", forward.Value!.Steps[0].Predicates[0].FromColumn);
            Assert.Equal("site_ref", forward.Value!.Steps[0].Predicates[0].ToColumn);
            Assert.Equal("site_ref", backward.Value!.Steps[0].Predicates[0].FromColumn);
            Assert.Equal("site_key", backward.Value!.Steps[0].Predicates[0].ToColumn);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    // ========================================================================
    // The refusals, on real rows.
    // ========================================================================

    [SkippableFact]
    public async Task RL03_no_declared_path_is_refused_rather_than_guessed()
    {
        var v = await BuildAsync();
        try
        {
            var plan = await v.Planner.PlanAsync(
                "res_absent_left_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                "res_absent_right_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Explore, CancellationToken.None);

            Assert.False(plan.Value!.Planned);
            Assert.Equal(RelationshipRefusalCodes.NoPath, plan.Value!.RefusalCode);
            Assert.Empty(plan.Value!.Steps);
        }
        finally { await v.DataSource.DisposeAsync(); }
    }

    [SkippableFact]
    public async Task RL01_two_declared_paths_are_refused_and_both_are_named()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var first = Code("AMB1");
        var second = Code("AMB2");
        var left = "res_amb_left";
        var right = "res_amb_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1, new List<RelationshipDeclaration>
                {
                    Declaration(first, left, right),
                    Declaration(second, left, right)
                }),
                CancellationToken.None);

            var plan = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);

            Assert.False(plan.Value!.Planned);
            Assert.Equal(RelationshipRefusalCodes.AmbiguousPath, plan.Value!.RefusalCode);
            Assert.Empty(plan.Value!.Steps);
            Assert.Equal(2, plan.Value!.CandidatePaths.Count);
            Assert.Contains(first, plan.Value!.CandidatePaths);
            Assert.Contains(second, plan.Value!.CandidatePaths);

            // Determinism is the point: the same model must produce the same
            // refusal text, not a text that depends on row order.
            var repeat = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.Equal(plan.Value!.RefusalMessage, repeat.Value!.RefusalMessage);
            Assert.Equal(plan.Value!.CandidatePaths, repeat.Value!.CandidatePaths);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task RL01_is_resolved_by_a_preferred_path_on_real_rows()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var preferred = Code("PREF");
        var other = Code("OTHER");
        var left = "res_pref_left";
        var right = "res_pref_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1, new List<RelationshipDeclaration>
                {
                    Declaration(preferred, left, right, preferred: true),
                    Declaration(other, left, right)
                }),
                CancellationToken.None);

            var plan = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);

            Assert.True(plan.Value!.Planned);
            Assert.Single(plan.Value!.Steps);
            Assert.Equal(preferred, plan.Value!.Steps[0].RelationshipCode);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Preference_is_governed_data_and_flipping_it_changes_the_answer_with_no_code_change()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var a = Code("FLIPA");
        var b = Code("FLIPB");
        var left = "res_flip_left";
        var right = "res_flip_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1, new List<RelationshipDeclaration>
                {
                    Declaration(a, left, right),
                    Declaration(b, left, right)
                }),
                CancellationToken.None);

            // Two lawful paths, neither preferred: refused rather than guessed.
            var ambiguous = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.False(ambiguous.Value!.Planned);
            Assert.Equal(RelationshipRefusalCodes.AmbiguousPath, ambiguous.Value!.RefusalCode);

            // Preference set on A. Nothing is recompiled, redeployed or edited.
            await SetPreferenceAsync(v.DataSource, a, true);

            var choosesA = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.True(choosesA.Value!.Planned);
            Assert.Single(choosesA.Value!.Steps);
            Assert.Equal(a, choosesA.Value!.Steps[0].RelationshipCode);

            // Preference moved to B. Same binary, same query, different answer,
            // because which path is intended is the plant's decision and not the
            // product's. This is the whole reason preference is a column.
            await SetPreferenceAsync(v.DataSource, a, false);
            await SetPreferenceAsync(v.DataSource, b, true);

            var choosesB = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.True(choosesB.Value!.Planned);
            Assert.Single(choosesB.Value!.Steps);
            Assert.Equal(b, choosesB.Value!.Steps[0].RelationshipCode);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task RL02_an_automated_consumer_is_refused_where_exploration_is_allowed()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = Code("RL02");
        var left = "res_rl02_left";
        var right = "res_rl02_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            var published = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1,
                    new List<RelationshipDeclaration> { Declaration(code, left, right) }),
                CancellationToken.None);

            // Real rows, real state: publication does not prove anything, so the
            // stored state is 'unproven'.
            Assert.Equal(RelationshipValidationStates.Unproven, published.Value![0].ValidationState);

            var automated = await v.Planner.PlanAsync(left, right, Compiler, CancellationToken.None);
            Assert.False(automated.Value!.Planned);
            Assert.Equal(RelationshipRefusalCodes.UnprovenRelationship, automated.Value!.RefusalCode);
            Assert.Empty(automated.Value!.Steps);
            Assert.Contains(code, automated.Value!.RefusalMessage!);

            // The same relationship, the same instant, a manual purpose: allowed.
            var manual = await v.Planner.PlanAsync(left, right, Explore, CancellationToken.None);
            Assert.True(manual.Value!.Planned);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task The_certification_tenant_cannot_see_another_tenants_relationships()
    {
        var v = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = Code("TENANT");
        var left = "res_tenant_left";
        var right = "res_tenant_right";

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, CertificationTenant, definitionId);

            await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1,
                    new List<RelationshipDeclaration> { Declaration(code, left, right) }),
                CancellationToken.None);

            var foreignService = new RelationshipService(
                new NpgsqlRelationshipStore(v.DataSource), new FixedTenant(Guid.NewGuid()));
            var foreignPlanner = new RelationshipJoinPlanner(
                new RelationshipResolver(foreignService), foreignService);

            var plan = await foreignPlanner.PlanAsync(left, right, Explore, CancellationToken.None);

            Assert.False(plan.Value!.Planned);
            Assert.Equal(RelationshipRefusalCodes.NoPath, plan.Value!.RefusalCode);
        }
        finally
        {
            await CleanAsync(v.DataSource, definitionId);
            await v.DataSource.DisposeAsync();
        }
    }
}
