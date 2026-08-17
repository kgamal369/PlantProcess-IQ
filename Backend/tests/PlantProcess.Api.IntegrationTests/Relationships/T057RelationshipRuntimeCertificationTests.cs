using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Relationships;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Infrastructure.Relationships;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Relationships;

/// <summary>
/// T-057 runtime certification.
///
/// This publishes through the PUBLICATION SEAM and reads back through the
/// SERVICE. It never inserts a relationship row with SQL, because a row put
/// there by hand proves the table exists and proves nothing about the contract.
///
/// The seam is complete and invokable. What does not exist yet is a CALLER:
/// relationships are emitted by publishing a transformation definition, and that
/// publication path is C1/DF4, which is not this task's and is not in M1's
/// Worker-1 queue. Inventing a caller - by hooking an unrelated publish endpoint
/// and guessing where declarations live in its payload - would be building the
/// very temporary contract the product model forbids. So the seam is certified
/// directly, exactly as its real caller will call it.
///
/// The store is constructed against the integration database on purpose: the
/// real adapter, the real SQL, the real CHECK constraints and the real partial
/// unique index all take part. Only the tenant is substituted, because tenant
/// resolution is HTTP-bound and this is not an HTTP call.
/// </summary>
public sealed class T057RelationshipRuntimeCertificationTests : AuthenticatedApiTestBase
{
    // Derived from the integration base purely to reach its connection-string
    // and reachability helpers, which are protected. Nothing here goes through
    // HTTP: the seam under certification is not an HTTP surface.
    public T057RelationshipRuntimeCertificationTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static readonly Guid CertificationTenant = Guid.Parse("7e57c0de-0000-4000-8000-000000057001");

    private sealed class FixedTenant : ITenantAccessor
    {
        private readonly Guid _tenantId;
        public FixedTenant(Guid tenantId) => _tenantId = tenantId;
        public Guid TenantId => _tenantId;
        public bool TryGetTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private static string ConnectionString => ResolveIntegrationTestConnectionString();

    private static async Task<(RelationshipService Service, NpgsqlDataSource DataSource)> BuildAsync()
    {
        Skip.IfNot(IsIntegrationDbReachable(),
            "Integration Postgres not reachable/authenticated on this machine; runs in CI.");

        var dataSource = NpgsqlDataSource.Create(ConnectionString);

        // The database is asserted BY NAME before anything else. The integration
        // test base defaults to ppiq_app when no connection string is supplied.
        // A run that cannot say which database it proved anything about has not
        // proved anything.
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            Assert.Equal("ppiq_presentation", conn.Database);

            cmd.CommandText = "SELECT to_regclass('public.ppiq_plant_relationships') IS NOT NULL";
            var present = (bool)(await cmd.ExecuteScalarAsync())!;
            Assert.True(present,
                $"Script 827 has not been applied to database '{conn.Database}'. " +
                "T-057 cannot be certified against a schema that does not carry it.");
        }

        return (new RelationshipService(new NpgsqlRelationshipStore(dataSource), new FixedTenant(CertificationTenant)), dataSource);
    }

    private static RelationshipDeclaration Declaration(string code, string left, string right) => new(
        code, left, right,
        RelationshipJoinTypes.Inner,
        RelationshipCardinalities.OneToMany,
        "unit", "unit",
        null, null, false,
        new List<RelationshipMemberDto>
        {
            new("site_key", "site_ref", 0),
            new("unit_key", "unit_ref", 1)
        });

    private static async Task CleanAsync(NpgsqlDataSource dataSource, Guid definitionId)
    {
        // Certification leaves nothing behind. This removes only what THIS test
        // published, addressed by its own definition identity.
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "DELETE FROM public.ppiq_plant_relationships WHERE tenant_id = @tenant AND source_definition_id = @def";
        cmd.Parameters.AddWithValue("tenant", CertificationTenant);
        cmd.Parameters.AddWithValue("def", definitionId);
        await cmd.ExecuteNonQueryAsync();
    }

    [SkippableFact]
    public async Task Publishing_through_the_seam_preserves_the_whole_contract_and_reads_back()
    {
        var (service, dataSource) = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = "T057_CERT_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        try
        {
            var published = await service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 3,
                    new List<RelationshipDeclaration> { Declaration(code, "cert_left", "cert_right") }),
                CancellationToken.None);

            Assert.True(published.IsSuccess, published.Error?.Message);
            Assert.Single(published.Value!);

            var emitted = published.Value![0];

            // Source definition identity and version survive publication, so a
            // result computed under this relationship stays explainable after
            // the model moves on.
            Assert.Equal(definitionId, emitted.SourceDefinitionId);
            Assert.Equal(3, emitted.SourceDefinitionVersion);

            Assert.Equal(RelationshipCardinalities.OneToMany, emitted.Cardinality);
            Assert.Equal(RelationshipJoinTypes.Inner, emitted.JoinType);
            Assert.Equal(RelationshipValidationStates.Unproven, emitted.ValidationState);
            Assert.Equal(RelationshipAmbiguityStates.Unambiguous, emitted.AmbiguityState);
            Assert.False(emitted.IsGrainConverting);

            // Ordered composite members survive the round trip in order.
            Assert.Equal(2, emitted.Members.Count);
            Assert.Equal("site_key", emitted.Members[0].LeftColumn);
            Assert.Equal(0, emitted.Members[0].MemberOrder);
            Assert.Equal("unit_key", emitted.Members[1].LeftColumn);
            Assert.Equal(1, emitted.Members[1].MemberOrder);

            // Read back through the service, not through the publication result.
            var byId = await service.GetByIdAsync(emitted.Id, CancellationToken.None);
            Assert.True(byId.IsSuccess, byId.Error?.Message);
            Assert.Equal(code, byId.Value!.RelationshipCode);
            Assert.Equal(2, byId.Value!.Members.Count);

            var byEntity = await service.GetPublishedAsync("cert_right", CancellationToken.None);
            Assert.True(byEntity.IsSuccess);
            Assert.Contains(byEntity.Value!, r => r.RelationshipCode == code);
        }
        finally
        {
            await CleanAsync(dataSource, definitionId);
            await dataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task A_retired_relationship_is_excluded_from_consumer_reads()
    {
        var (service, dataSource) = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = "T057_CERT_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        try
        {
            var published = await service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1,
                    new List<RelationshipDeclaration> { Declaration(code, "cert_left", "cert_right") }),
                CancellationToken.None);

            Assert.True(published.IsSuccess, published.Error?.Message);
            var id = published.Value![0].Id;

            var retired = await service.RetireByDefinitionAsync(definitionId, CancellationToken.None);
            Assert.True(retired.IsSuccess);
            Assert.Equal(1, retired.Value);

            // Excluded from both consumer reads. It is deactivated, not deleted:
            // the row is still there, which is what keeps a historical result
            // explainable - but no consumer may traverse it.
            Assert.True((await service.GetByIdAsync(id, CancellationToken.None)).IsFailure);

            var live = await service.GetPublishedAsync(null, CancellationToken.None);
            Assert.True(live.IsSuccess);
            Assert.DoesNotContain(live.Value!, r => r.RelationshipCode == code);

            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT retired_at_utc IS NOT NULL FROM public.ppiq_plant_relationships WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            Assert.True((bool)(await cmd.ExecuteScalarAsync())!, "the row must survive retirement, deactivated rather than deleted");
        }
        finally
        {
            await CleanAsync(dataSource, definitionId);
            await dataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Republication_supersedes_the_previous_emission_against_the_real_unique_index()
    {
        var (service, dataSource) = await BuildAsync();
        var definitionId = Guid.NewGuid();
        var code = "T057_CERT_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        try
        {
            var first = await service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1,
                    new List<RelationshipDeclaration> { Declaration(code, "cert_left", "cert_right") }),
                CancellationToken.None);
            Assert.True(first.IsSuccess, first.Error?.Message);

            // The same code twice would violate the live partial unique index if
            // republication did not retire the previous emission first. This is
            // the assertion the unit estate cannot make.
            var second = await service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 2,
                    new List<RelationshipDeclaration> { Declaration(code, "cert_left", "cert_right") }),
                CancellationToken.None);
            Assert.True(second.IsSuccess, second.Error?.Message);

            var live = await service.GetPublishedAsync(null, CancellationToken.None);
            var matching = live.Value!.Where(r => r.RelationshipCode == code).ToList();
            Assert.Single(matching);
            Assert.Equal(2, matching[0].SourceDefinitionVersion);
        }
        finally
        {
            await CleanAsync(dataSource, definitionId);
            await dataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task The_database_refuses_grain_conversion_without_attribution_even_if_the_service_is_bypassed()
    {
        var (_, dataSource) = await BuildAsync();

        try
        {
            // Two guards, one rule. The service refuses this at publish; if a
            // future caller ever reaches the adapter another way, the CHECK
            // constraint refuses it too. One of the two will eventually be
            // bypassed, which is why there are two.
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO public.ppiq_plant_relationships " +
                "(tenant_id, relationship_code, left_entity, right_entity, join_type, cardinality, " +
                " grain_left, grain_right, source_definition_id, source_definition_version, effective_from_utc) " +
                "VALUES (@t, @c, 'a', 'b', 'inner', '1-n', 'heat', 'coil', @d, 1, now())";
            cmd.Parameters.AddWithValue("t", CertificationTenant);
            cmd.Parameters.AddWithValue("c", "T057_CERT_GRAIN_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            cmd.Parameters.AddWithValue("d", Guid.NewGuid());

            await Assert.ThrowsAsync<PostgresException>(async () => await cmd.ExecuteNonQueryAsync());
        }
        finally
        {
            await dataSource.DisposeAsync();
        }
    }
}