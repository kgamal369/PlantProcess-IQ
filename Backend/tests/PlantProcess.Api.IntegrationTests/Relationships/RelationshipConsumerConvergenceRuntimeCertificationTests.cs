using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Relationships;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Dashboarding.Dimensions;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Relationships;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Relationships;

/// <summary>
/// The three authorities proven together on real rows: validation, path preference,
/// and a real cross-subject consumer executing through them.
///
/// The consumer is the binder both dashboard query services call for a declared
/// dimension published against a related entity - here a site-level dimension
/// filtering material units. The same request, with no code change between calls,
/// must refuse an unproven path, refuse an ungoverned choice, follow the preferred
/// path, follow the OTHER path when only the preference flips, and refuse once the
/// authority is retired.
///
/// Everything mutating happens on a disposable clone named by the pack. ppiq_app is
/// never written; the pack fingerprints it before and after and requires equality.
/// </summary>
public sealed class RelationshipConsumerConvergenceRuntimeCertificationTests : AuthenticatedApiTestBase
{
    public RelationshipConsumerConvergenceRuntimeCertificationTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private const string ProbeVariable = "PPIQ_RELATIONSHIP_PROBE_DB";

    // One tenant per test. The probe database is shared by the whole class, and a
    // relationship published by one test is a lawful second path as far as another
    // test's resolver is concerned - which is how a single-path RL02 proof quietly
    // became an RL01. Isolation by tenant is the model's own boundary, so the tests
    // are separated by the same rule the product enforces.
    private readonly Guid _tenant = Guid.NewGuid();

    private sealed class FixedTenant : ITenantAccessor
    {
        private readonly Guid _tenantId;
        public FixedTenant(Guid tenantId) => _tenantId = tenantId;
        public Guid TenantId => _tenantId;
        public bool TryGetTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed record Vertical(
        Guid Tenant,
        PlantProcessDbContext Db,
        NpgsqlDataSource DataSource,
        RelationshipService Service,
        RelatedDeclaredDimensionBinder Binder);

    private static string ProbeConnectionString()
    {
        var probe = Environment.GetEnvironmentVariable(ProbeVariable);
        Skip.If(string.IsNullOrWhiteSpace(probe),
            "No disposable relationship probe database named in " + ProbeVariable + "; the pack creates one from ppiq_app.");

        Skip.IfNot(IsIntegrationDbReachable(),
            "Integration Postgres not reachable/authenticated on this machine; runs in CI.");

        var builder = new NpgsqlConnectionStringBuilder(ResolveIntegrationTestConnectionString()) { Database = probe };
        return builder.ConnectionString;
    }

    private Vertical Build()
    {
        var connectionString = ProbeConnectionString();

        // The naming convention is part of the model, not a detail of hosting. Without
        // it EF asks for "Id" and the canonical schema has "id" - the certification
        // would fail on its own scaffolding while saying nothing about relationships.
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        var db = new PlantProcessDbContext(options);
        var dataSource = NpgsqlDataSource.Create(connectionString);

        var tenant = new FixedTenant(_tenant);
        var catalog = new CanonicalEntityCatalog(db);
        var service = new RelationshipService(
            new NpgsqlRelationshipStore(dataSource), tenant, new RelationshipValidationEvidenceReader(db, catalog));
        var resolver = new RelationshipResolver(service);
        var planner = new RelationshipJoinPlanner(resolver, service);
        var executor = new RelationshipPlanSubjectKeyExecutor(db, catalog);

        return new Vertical(_tenant, db, dataSource, service, new RelatedDeclaredDimensionBinder(catalog, planner, executor));
    }

    private static string Code(string suffix) => "CONV_" + suffix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>A site-level declared dimension: lives on Site, filters MaterialUnit through the relationship.</summary>
    private static DeclaredDimension SiteCodeDimension(Guid definitionId) => new(
        "site_code", "Site code", "string", "site",
        nameof(Site), nameof(Site.SiteCode), typeof(Site),
        true, null, null, definitionId, 1);

    private static RelationshipDeclaration SiteToMaterial(string code, bool preferred = false) => new(
        code, nameof(Site), nameof(MaterialUnit),
        RelationshipJoinTypes.Inner, RelationshipCardinalities.OneToMany,
        "site", "site", null, null, preferred,
        new List<RelationshipMemberDto> { new(nameof(Site.Id), nameof(MaterialUnit.SiteId), 0) });

    private static async Task SetPreferenceAsync(Vertical v, string code, bool preferred)
    {
        var dataSource = v.DataSource;
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE ppiq_meta.plant_relationships SET is_preferred_path = @p " +
            "WHERE tenant_id = @t AND relationship_code = @c AND retired_at_utc IS NULL";
        cmd.Parameters.AddWithValue("p", preferred);
        cmd.Parameters.AddWithValue("t", v.Tenant);
        cmd.Parameters.AddWithValue("c", code);
        await cmd.ExecuteNonQueryAsync();
    }

    [SkippableFact]
    public async Task A_real_cross_subject_consumer_follows_validation_then_preference_then_refusal_with_no_code_change()
    {
        var v = Build();
        var definitionA = Guid.NewGuid();
        var definitionB = Guid.NewGuid();
        var a = Code("A");
        var b = Code("B");
        var siteCode = "CERT-" + Guid.NewGuid().ToString("N").Substring(0, 6);

        try
        {
            // Real canonical rows on the clone: one site, one material unit at it.
            var site = new Site(siteCode, "Convergence certification site", isSynthetic: true);
            var material = new MaterialUnit("MC-" + siteCode, "Coil", site.Id, null, null, isSynthetic: true);
            v.Db.Add(site);
            v.Db.Add(material);
            await v.Db.SaveChangesAsync();

            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, v.Tenant, definitionA, definitionB);

            var declared = SiteCodeDimension(definitionA);

            // 1. ONE path, unproven. Page/widget execution is automated, so it refuses.
            //
            // The single path matters. Ambiguity is decided before proof is, so two
            // unproven paths would refuse RL01 and this proof would never reach RL02.
            var firstPublish = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionA, 1, new List<RelationshipDeclaration> { SiteToMaterial(a) }),
                CancellationToken.None);
            Assert.True(firstPublish.IsSuccess, firstPublish.Error?.Message);
            Assert.Equal(RelationshipValidationStates.Unproven, firstPublish.Value![0].ValidationState);

            var unproven = await Assert.ThrowsAsync<RelationshipPathRefusalException>(() =>
                v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode,
                    RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None));
            Assert.Equal(RelationshipRefusalCodes.UnprovenRelationship, unproven.RefusalCode);
            Assert.Equal(RelationshipConsumerPurposes.QueryCompiler, unproven.ConsumerPurpose);

            // 2. VALIDATE against the real rows, then the same request succeeds.
            var validatedA = await v.Service.ValidateAsync(firstPublish.Value![0].Id, CancellationToken.None);
            Assert.True(validatedA.IsSuccess, validatedA.Error?.Message);
            Assert.Equal(RelationshipValidationStates.Validated, validatedA.Value!.ValidationState);
            Assert.True(validatedA.Value!.Evidence.LeftMatched >= 1, "the seeded site did not match");
            Assert.True(validatedA.Value!.Evidence.RightMatched >= 1, "the seeded material did not match");
            Assert.False(validatedA.Value!.Evidence.CardinalityContradicted);

            var provenA = await v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode,
                RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None);
            Assert.Equal(a, provenA.Plan.Steps[0].RelationshipCode);
            Assert.Contains(material.Id, provenA.Keys);

            // 3. A SECOND lawful path, from its own definition so the first keeps its
            //    proof. Validated, unpreferred: refused, and both are named.
            var secondPublish = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionB, 1, new List<RelationshipDeclaration> { SiteToMaterial(b) }),
                CancellationToken.None);
            Assert.True(secondPublish.IsSuccess, secondPublish.Error?.Message);

            var validatedB = await v.Service.ValidateAsync(secondPublish.Value![0].Id, CancellationToken.None);
            Assert.True(validatedB.IsSuccess, validatedB.Error?.Message);
            Assert.Equal(RelationshipValidationStates.Validated, validatedB.Value!.ValidationState);

            var ambiguous = await Assert.ThrowsAsync<RelationshipPathRefusalException>(() =>
                v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode,
                    RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None));
            Assert.Equal(RelationshipRefusalCodes.AmbiguousPath, ambiguous.RefusalCode);
            Assert.Contains(a, ambiguous.CandidatePaths);
            Assert.Contains(b, ambiguous.CandidatePaths);

            // 4. PREFER A. Same request; the consumer follows A.
            await SetPreferenceAsync(v, a, true);

            var followsA = await v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode,
                RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None);
            Assert.Equal(a, followsA.Plan.Steps[0].RelationshipCode);
            Assert.Contains(material.Id, followsA.Keys);

            // 5. FLIP TO B. Nothing else changed - not the request, not the consumer,
            //    not a build. Which path is intended is the plant's decision.
            await SetPreferenceAsync(v, a, false);
            await SetPreferenceAsync(v, b, true);

            var followsB = await v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode,
                RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None);
            Assert.Equal(b, followsB.Plan.Steps[0].RelationshipCode);
            Assert.Contains(material.Id, followsB.Keys);

            // A value no site carries selects no material, through the same path.
            var none = await v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode + "-absent",
                RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None);
            Assert.Empty(none.Keys);

            // 6. RETIRE both authorities. Same request: refused, nothing inferred.
            Assert.Equal(1, (await v.Service.RetireByDefinitionAsync(definitionA, CancellationToken.None)).Value);
            Assert.Equal(1, (await v.Service.RetireByDefinitionAsync(definitionB, CancellationToken.None)).Value);

            var gone = await Assert.ThrowsAsync<RelationshipPathRefusalException>(() =>
                v.Binder.SubjectKeysWhereDeclaredEqualsAsync(declared, typeof(MaterialUnit), siteCode,
                    RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None));
            Assert.Equal(RelationshipRefusalCodes.NoPath, gone.RefusalCode);
        }
        finally
        {
            await v.Db.DisposeAsync();
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task A_relationship_whose_declared_members_match_nothing_fails_validation_rather_than_passing_vacuously()
    {
        var v = Build();
        var definitionId = Guid.NewGuid();
        var code = Code("NOMATCH");

        try
        {
            var site = new Site("VAL-" + Guid.NewGuid().ToString("N").Substring(0, 6), "Validation site", isSynthetic: true);
            var material = new MaterialUnit("MU-" + Guid.NewGuid().ToString("N").Substring(0, 6), "Coil", site.Id, null, null, isSynthetic: true);
            v.Db.Add(site);
            v.Db.Add(material);
            await v.Db.SaveChangesAsync();

            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, v.Tenant, definitionId);

            // A lawful-looking declaration that compares a site's key to a material's own key.
            // Both sides are populated; nothing matches; that is a finding, not a pass.
            var declaration = new RelationshipDeclaration(
                code, nameof(Site), nameof(MaterialUnit),
                RelationshipJoinTypes.Inner, RelationshipCardinalities.OneToMany,
                "site", "site", null, null, false,
                new List<RelationshipMemberDto> { new(nameof(Site.Id), nameof(MaterialUnit.Id), 0) });

            var published = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1, new List<RelationshipDeclaration> { declaration }),
                CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);

            var validated = await v.Service.ValidateAsync(published.Value![0].Id, CancellationToken.None);
            Assert.True(validated.IsSuccess, validated.Error?.Message);
            Assert.Equal(RelationshipValidationStates.Failed, validated.Value!.ValidationState);
            Assert.Equal(0, validated.Value!.Evidence.LeftMatched);
        }
        finally
        {
            await v.Db.DisposeAsync();
            await v.DataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Publication_refuses_a_comparison_the_canonical_executor_cannot_run()
    {
        var v = Build();
        var definitionId = Guid.NewGuid();

        try
        {
            await RelationshipCertificationDefinitions.SeedAsync(v.DataSource, v.Tenant, definitionId);

            var declaration = new RelationshipDeclaration(
                Code("NEQ"), nameof(Site), nameof(MaterialUnit),
                RelationshipJoinTypes.Inner, RelationshipCardinalities.OneToMany,
                "site", "site", null, null, false,
                new List<RelationshipMemberDto> { new(nameof(Site.Id), nameof(MaterialUnit.SiteId), 0, "<>") });

            var published = await v.Service.PublishAsync(
                new RelationshipPublicationRequest(definitionId, 1, new List<RelationshipDeclaration> { declaration }),
                CancellationToken.None);

            Assert.True(published.IsFailure);
            Assert.Contains(RelationshipPublicationCodes.UnknownVocabulary, published.Error!.Message);
        }
        finally
        {
            await v.Db.DisposeAsync();
            await v.DataSource.DisposeAsync();
        }
    }
}
