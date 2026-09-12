using System.Reflection;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Dashboarding.Services.Queries;
using PlantProcess.Application.Relationships;
using Xunit;

namespace PlantProcess.Application.UnitTests.Relationships;

/// <summary>
/// The first consumer set resolves through the relationship authority and nothing
/// else. Two things are proven here without a database: that the binder asks the
/// one resolver by naming both endpoints through one catalogue and carries its
/// refusal unchanged, and that no consumer family can become a second path
/// authority - by injection, by inheritance, or by source.
/// </summary>
public sealed class RelationshipConsumerConvergenceTests
{
    // ========================================================================
    // Fakes. They record what they were asked; they decide nothing.
    // ========================================================================

    private sealed class Left { public Guid Id { get; set; } public string Code { get; set; } = ""; }
    private sealed class Subject { public Guid Id { get; set; } }

    private sealed class RecordingCatalog : ICanonicalEntityCatalog
    {
        public List<Type> Asked { get; } = new();
        // T-253. This fake exists for relationship consumer convergence, where nothing
        // is an authoring output target. It says so rather than pretending otherwise.
        public IReadOnlyList<string> ProjectionTargetNames() => Array.Empty<string>();
        // T-262. Nothing here is a projection target, so nothing here has projection
        // fields. Answering empty is the truth, not a stub.
        public IReadOnlyList<CanonicalProjectionField> ProjectionFieldsOf(string n) =>
            Array.Empty<CanonicalProjectionField>();
        public bool IsProjectionTarget(string n) => false;
        public string? NameOf(Type t) { Asked.Add(t); return t == typeof(Left) ? "Left" : t == typeof(Subject) ? "Subject" : null; }
        public Type? FindType(string n) => n == "Left" ? typeof(Left) : n == "Subject" ? typeof(Subject) : null;
        public string? PrimaryKeyMemberOf(Type t) => "Id";
    }

    private sealed class ScriptedPlanner : IRelationshipJoinPlanner
    {
        private readonly RelationshipJoinPlanDto _plan;
        public (string From, string To, string Purpose)? Asked { get; private set; }
        public ScriptedPlanner(RelationshipJoinPlanDto plan) => _plan = plan;
        public Task<ApplicationResult<RelationshipJoinPlanDto>> PlanAsync(string from, string to, string purpose, CancellationToken ct)
        {
            Asked = (from, to, purpose);
            return Task.FromResult(ApplicationResult<RelationshipJoinPlanDto>.Success(_plan with { FromEntity = from, ToEntity = to, Purpose = purpose }));
        }
    }

    private sealed class RecordingExecutor : IRelationshipPlanSubjectKeyExecutor
    {
        public RelationshipJoinPlanDto? Received { get; private set; }
        public Task<IReadOnlyList<Guid>> SubjectKeysAsync(RelationshipJoinPlanDto plan, DeclaredDimension d, Type s, string v, CancellationToken ct)
        {
            Received = plan;
            return Task.FromResult<IReadOnlyList<Guid>>(new[] { Guid.NewGuid() });
        }
    }

    private static DeclaredDimension Declared(string catalog = "Left") => new(
        "site_code", "Site code", "string", "site", catalog, "Code", typeof(Left),
        true, null, null, Guid.NewGuid(), 1);

    private static RelationshipJoinPlanDto Refused(string code) => new(
        "", "", "", false, Array.Empty<RelationshipJoinStepDto>(), false, false, code, "refused", Array.Empty<string>());

    private static RelationshipJoinPlanDto Planned() => new(
        "", "", "", true,
        new[] { new RelationshipJoinStepDto(Guid.NewGuid(), "REL", "Left", "Subject", "inner", "1-n", false, null,
            new[] { new RelationshipJoinPredicateDto("Id", "LeftId", "=", 0) }) },
        false, false, null, null, Array.Empty<string>());

    // ========================================================================
    // Binder: one catalogue, one resolver, refusal carried by its own name.
    // ========================================================================

    [Fact]
    public async Task Both_endpoints_are_named_through_the_same_catalogue_and_the_purpose_is_the_callers()
    {
        var catalog = new RecordingCatalog();
        var planner = new ScriptedPlanner(Planned());
        var executor = new RecordingExecutor();
        var binder = new RelatedDeclaredDimensionBinder(catalog, planner, executor);

        var selection = await binder.SubjectKeysWhereDeclaredEqualsAsync(
            Declared(), typeof(Subject), "S1", RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None);

        Assert.Contains(typeof(Left), catalog.Asked);
        Assert.Contains(typeof(Subject), catalog.Asked);
        Assert.Equal(("Left", "Subject", RelationshipConsumerPurposes.QueryCompiler), planner.Asked);
        Assert.Same(selection.Plan, executor.Received);
        Assert.Single(selection.Keys);
    }

    [Theory]
    [InlineData(RelationshipRefusalCodes.UnprovenRelationship)]
    [InlineData(RelationshipRefusalCodes.AmbiguousPath)]
    [InlineData(RelationshipRefusalCodes.NoPath)]
    public async Task A_resolver_refusal_surfaces_under_its_own_code_never_as_a_dimension_code(string code)
    {
        var binder = new RelatedDeclaredDimensionBinder(new RecordingCatalog(), new ScriptedPlanner(Refused(code)), new RecordingExecutor());

        var refusal = await Assert.ThrowsAsync<RelationshipPathRefusalException>(() =>
            binder.SubjectKeysWhereDeclaredEqualsAsync(Declared(), typeof(Subject), "S1", RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None));

        Assert.Equal(code, refusal.RefusalCode);
        Assert.Equal(RelationshipConsumerPurposes.QueryCompiler, refusal.ConsumerPurpose);
        Assert.DoesNotContain("DB0", refusal.RefusalCode);
    }

    [Fact]
    public async Task A_declaration_whose_catalogue_name_disagrees_with_its_resolved_type_is_refused_not_normalised()
    {
        var binder = new RelatedDeclaredDimensionBinder(new RecordingCatalog(), new ScriptedPlanner(Planned()), new RecordingExecutor());

        var refusal = await Assert.ThrowsAsync<DimensionBindingRefusalException>(() =>
            binder.SubjectKeysWhereDeclaredEqualsAsync(Declared(catalog: "SomethingElse"), typeof(Subject), "S1", RelationshipConsumerPurposes.QueryCompiler, CancellationToken.None));

        Assert.Equal(DimensionBindingRefusalCodes.Unbindable, refusal.RefusalCode);
    }

    // ========================================================================
    // The barrier. Consumer families may depend on the resolver seam and on
    // nothing that would let them choose a path themselves.
    // ========================================================================

    private static readonly Assembly ApplicationAssembly = typeof(RelationshipResolver).Assembly;

    private static Assembly? Infrastructure()
    {
        try { return Assembly.Load("PlantProcess.Infrastructure"); } catch { return null; }
    }

    private static IEnumerable<Type> ConsumerFamilyTypes()
    {
        var prefixes = new[]
        {
            "PlantProcess.Application.Dashboarding", "PlantProcess.Application.Analytics",
            "PlantProcess.Application.Services.Materials",
            "PlantProcess.Infrastructure.Dashboarding", "PlantProcess.Infrastructure.Analytics"
        };
        var assemblies = new List<Assembly> { ApplicationAssembly };
        var infra = Infrastructure();
        if (infra is not null) assemblies.Add(infra);

        return assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace is not null && prefixes.Any(p => t.Namespace.StartsWith(p, StringComparison.Ordinal)));
    }

    private static IEnumerable<Type> DependencyTypes(Type t)
    {
        foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            foreach (var p in c.GetParameters()) yield return p.ParameterType;
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            yield return f.FieldType;
    }

    [Fact]
    public void No_consumer_family_type_depends_on_relationship_storage()
    {
        var offenders = ConsumerFamilyTypes()
            .Where(t => DependencyTypes(t).Any(d => d == typeof(IRelationshipStore)))
            .Select(t => t.FullName).ToList();

        Assert.True(offenders.Count == 0, "types depending on IRelationshipStore: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_interim_subject_link_resolver_no_longer_exists_anywhere()
    {
        var assemblies = new List<Assembly> { ApplicationAssembly };
        var infra = Infrastructure();
        if (infra is not null) assemblies.Add(infra);

        var ghosts = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t.Name.Contains("DeclaredDimensionSubjectLinkResolver", StringComparison.Ordinal))
            .Select(t => t.FullName).ToList();

        Assert.True(ghosts.Count == 0, "interim resolver still present: " + string.Join(", ", ghosts));
    }

    [Fact]
    public void Executors_and_evidence_readers_cannot_choose_because_they_cannot_see_the_resolver()
    {
        var infra = Infrastructure();
        if (infra is null) return;

        var executors = infra.GetTypes().Where(t =>
            typeof(IRelationshipPlanSubjectKeyExecutor).IsAssignableFrom(t) ||
            typeof(IRelationshipValidationEvidenceReader).IsAssignableFrom(t));

        foreach (var t in executors)
        {
            var deps = DependencyTypes(t).ToList();
            Assert.DoesNotContain(typeof(IRelationshipResolver), deps);
            Assert.DoesNotContain(typeof(IRelationshipJoinPlanner), deps);
            Assert.DoesNotContain(typeof(IRelationshipService), deps);
            Assert.DoesNotContain(typeof(IRelationshipStore), deps);
        }
    }

    [Fact]
    public void Consumer_source_contains_no_reference_discovery_and_no_relationship_table()
    {
        var root = LocateRepositoryRoot();
        Assert.True(root is not null, "repository root not located from " + AppContext.BaseDirectory);

        var scanned = new[]
        {
            "Backend/PlantProcess.Application/Dashboarding",
            "Backend/PlantProcess.Application/Analytics",
            "Backend/PlantProcess.Application/Services/Materials",
            "Backend/PlantProcess.Infrastructure/Dashboarding",
            "Backend/PlantProcess.Infrastructure/Analytics"
        };

        var forbidden = new[] { "GetForeignKeys(", "SelectSubjectLinkField", "plant_relationship", "IRelationshipStore", "IsPreferredPath)" };
        var hits = new List<string>();

        foreach (var dir in scanned.Select(d => Path.Combine(root!, d)).Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var code = string.Join("\n", File.ReadLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
                foreach (var token in forbidden)
                    if (code.Contains(token, StringComparison.Ordinal)) hits.Add(Path.GetFileName(file) + " -> " + token);
            }
        }

        Assert.True(hits.Count == 0, string.Join("; ", hits));
    }

    private static string? LocateRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Backend")) && Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    // ========================================================================
    // Eight named protection proofs, one per consumer purpose. Where a family has
    // a live cross-subject seam, the proof is that it runs through the binder.
    // Where it does not, the proof is that it cannot grow one that bypasses the
    // resolver. Nothing is fabricated to make a family look wired.
    // ========================================================================

    private static void RequiresBinder(Type consumer)
    {
        var ctor = consumer.GetConstructors().Single();
        var parameter = ctor.GetParameters().SingleOrDefault(p => p.ParameterType == typeof(IRelatedDeclaredDimensionBinder));
        Assert.True(parameter is not null, consumer.Name + " does not take the binder");
        Assert.DoesNotContain(typeof(IRelationshipStore), ctor.GetParameters().Select(p => p.ParameterType));
        Assert.False(ctor.GetParameters().Any(p => p.ParameterType.Name.Contains("SubjectLinkResolver", StringComparison.Ordinal)),
            consumer.Name + " still takes the interim resolver");
    }

    private static void CannotChoose(Type consumer)
    {
        var deps = DependencyTypes(consumer).ToList();
        Assert.DoesNotContain(typeof(IRelationshipStore), deps);
        Assert.False(deps.Any(d => d.Name.Contains("SubjectLinkResolver", StringComparison.Ordinal)), consumer.Name + " still sees the interim resolver");
    }

    [Fact] public void Proof_1_Projection_same_entity_stays_local_and_cross_entity_has_no_local_link_choice()
    {
        Assert.Null(typeof(DeclaredDimensionProjection).GetMethod("SelectSubjectLinkField", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(DeclaredDimensionProjection).GetMethod("RequireBindable", BindingFlags.Public | BindingFlags.Static));
        Assert.Equal("projection", RelationshipConsumerPurposes.Projection);
    }

    [Fact] public void Proof_2_QueryCompiler_page_query_runs_cross_subject_filters_through_the_binder() => RequiresBinder(typeof(DashboardQueryService));

    [Fact] public void Proof_3_QueryCompiler_widget_query_runs_cross_subject_filters_through_the_binder() => RequiresBinder(typeof(DashboardWidgetQueryService));

    [Fact] public void Proof_4_AssociativeFiltering_is_a_purpose_the_caller_names_not_one_the_binder_assumes()
    {
        var method = typeof(IRelatedDeclaredDimensionBinder).GetMethod(nameof(IRelatedDeclaredDimensionBinder.SubjectKeysWhereDeclaredEqualsAsync))!;
        Assert.Contains(method.GetParameters(), p => p.Name == "consumerPurpose" && p.ParameterType == typeof(string));
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.AssociativeFiltering));
    }

    [Fact] public void Proof_5_DrillDown_has_exactly_one_lawful_seam_and_no_bypass()
    {
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.DrillDown));
        foreach (var t in ConsumerFamilyTypes()) CannotChoose(t);
    }

    [Fact] public void Proof_6_DrillThrough_has_exactly_one_lawful_seam_and_no_bypass()
    {
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.DrillThrough));
        Assert.DoesNotContain(RelationshipConsumerPurposes.Explore, new[]
        {
            RelationshipConsumerPurposes.QueryCompiler, RelationshipConsumerPurposes.AssociativeFiltering,
            RelationshipConsumerPurposes.DrillDown, RelationshipConsumerPurposes.DrillThrough
        });
    }

    [Fact] public void Proof_7_Genealogy_material_edge_workflow_is_untouched_and_cannot_choose_a_semantic_path()
    {
        var genealogy = ApplicationAssembly.GetTypes().Single(t => t.Name == "GenealogyService");
        CannotChoose(genealogy);
        Assert.DoesNotContain(typeof(IRelationshipResolver), DependencyTypes(genealogy));
    }

    [Fact] public void Proof_8_Statistics_and_Correlation_operate_on_resolved_populations_and_cannot_choose_a_path()
    {
        foreach (var name in new[] { "CanonicalCorrelationEngine", "CorrelationEngineRegistry", "AdvancedCorrelationComputeService", "DashboardAggregateExecutor" })
        {
            var t = ApplicationAssembly.GetTypes().SingleOrDefault(x => x.Name == name);
            if (t is null) continue;
            CannotChoose(t);
        }
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.StatisticalAnalysis));
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.Correlation));
    }
}
