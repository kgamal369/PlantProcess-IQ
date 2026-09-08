using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Relationships;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// A declaration published against a related canonical entity is reached through the
/// relationship authority, and through nothing else.
///
/// This gate used to prove the opposite arrangement. Under T-094 a related declaration
/// was bound by counting the mapped references between two entities - one candidate
/// binds, none refuses (DB06), several refuse (DB07) - because no path authority
/// existed yet to ask. It does now, and counting references was always path selection:
/// a second answer to the question the relationship model exists to answer, and a
/// weaker one, because references cannot be preferred. A plant with two lawful routes
/// was refused permanently instead of publishing which one it meant.
///
/// So the decision is gone rather than moved. What is proved here is that it cannot
/// come back: the projection keeps no link chooser, the refusal codes that only
/// described that choice are retired in favour of the resolver's own, and the governed
/// chain is actually composed - a capability registered nowhere refuses every query it
/// was built to answer, and no contract test would notice.
/// </summary>
[Trait("BacklogTask", "T-096")]
[Trait("Gate", "DeclaredDimensionSubjectLink")]
public sealed class DeclaredDimensionSubjectLinkTests
{
    private sealed class SubjectRow
    {
        public Guid Id { get; set; }
    }

    private sealed class RelatedRow
    {
        public Guid Id { get; set; }
        public Guid SubjectRef { get; set; }
        public string? Bucket { get; set; }
    }

    private static DeclaredDimension Bindable(string code, Type entity, string field) =>
        new(code, code, "string", "unit", entity.Name, field, entity, true, null, null, Guid.NewGuid(), 1);

    private static DeclaredDimension Unbindable(string code) =>
        new(code, code, "string", "unit", "NoSuchEntity", "NoSuchField",
            null, false, DimensionBindingRefusalCodes.Unbindable, "no such entity", Guid.NewGuid(), 2);

    // ------------------------------------------------------------------------
    // Same entity stays local. That half never involved a relationship and still
    // does not: an intrinsic scalar on the population is not a path.
    // ------------------------------------------------------------------------

    [Fact]
    public void A_declaration_on_the_subject_itself_is_recognised_as_directly_bindable()
    {
        Assert.True(DeclaredDimensionProjection.BindsToSubject(
            Bindable("a", typeof(SubjectRow), "Bucket"), typeof(SubjectRow)));
    }

    [Fact]
    public void A_declaration_on_a_related_entity_is_not_directly_bindable()
    {
        Assert.False(DeclaredDimensionProjection.BindsToSubject(
            Bindable("b", typeof(RelatedRow), "Bucket"), typeof(SubjectRow)));
    }

    [Fact]
    public void An_unbindable_declaration_never_claims_to_bind_to_the_subject()
    {
        Assert.False(DeclaredDimensionProjection.BindsToSubject(Unbindable("ghost"), typeof(SubjectRow)));
    }

    [Fact]
    public void A_published_but_unexecutable_declaration_is_refused_before_any_path_is_sought()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.RequireBindableAnywhere(Unbindable("ghost")));

        Assert.Equal(DimensionBindingRefusalCodes.Unbindable, refusal.RefusalCode);
        Assert.Contains("no such entity", refusal.Message);
    }

    // ------------------------------------------------------------------------
    // The interim decision is gone and cannot return.
    // ------------------------------------------------------------------------

    [Fact]
    public void The_projection_carries_no_link_chooser_of_its_own()
    {
        var chooser = typeof(DeclaredDimensionProjection)
            .GetMethods()
            .Where(m => m.IsStatic)
            .Where(m => m.Name.Contains("Link", StringComparison.Ordinal)
                     || m.Name.Contains("SelectSubject", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.True(chooser.Count == 0,
            "the projection still exposes a link chooser: " + string.Join(", ", chooser));
    }

    [Fact]
    public void The_interim_subject_link_resolver_type_no_longer_exists()
    {
        var assemblies = new[] { typeof(DeclaredDimensionProjection).Assembly, typeof(RelationshipResolver).Assembly }
            .Distinct();

        var ghosts = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.Contains("SubjectLinkResolver", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(ghosts.Count == 0, "interim resolver still present: " + string.Join(", ", ghosts));
    }

    [Fact]
    public void The_refusal_codes_that_described_only_that_choice_are_retired()
    {
        // DB06 and DB07 named "no reference" and "several references". Those events
        // are now named by the authority that owns them, and keeping both vocabularies
        // would mean one event with two names.
        var retired = typeof(DimensionBindingRefusalCodes)
            .GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => v.StartsWith("DB06", StringComparison.Ordinal)
                     || v.StartsWith("DB07", StringComparison.Ordinal)
                     || v.StartsWith("DB08", StringComparison.Ordinal))
            .ToList();

        Assert.True(retired.Count == 0, "retired codes still declared: " + string.Join(", ", retired));

        // The dimension catalogue keeps what is genuinely about a declaration.
        Assert.Equal("DB01_dimension_undeclared", DimensionBindingRefusalCodes.Undeclared);
        Assert.Equal("DB02_source_field_unbindable", DimensionBindingRefusalCodes.Unbindable);
        Assert.Equal("DB09_dimension_filter_malformed", DimensionBindingRefusalCodes.FilterMalformed);
    }

    [Fact]
    public void The_events_those_codes_described_are_now_named_by_the_relationship_authority()
    {
        Assert.Equal("RL01", RelationshipRefusalCodes.AmbiguousPath);
        Assert.Equal("RL02", RelationshipRefusalCodes.UnprovenRelationship);
        Assert.Equal("RL03", RelationshipRefusalCodes.NoPath);
    }

    // ------------------------------------------------------------------------
    // What replaced it, and the fact that it is reachable at runtime.
    // ------------------------------------------------------------------------

    [Fact]
    public void The_binder_names_both_endpoints_and_takes_the_purpose_from_its_caller()
    {
        var method = typeof(IRelatedDeclaredDimensionBinder)
            .GetMethod(nameof(IRelatedDeclaredDimensionBinder.SubjectKeysWhereDeclaredEqualsAsync))!;

        var parameters = method.GetParameters().Select(p => p.Name).ToList();
        Assert.Contains("subjectEntityType", parameters);
        Assert.Contains("consumerPurpose", parameters);

        // Both endpoints are named through one catalogue, so the two sides of a
        // cross-entity question are in the same namespace by construction.
        var dependencies = typeof(RelatedDeclaredDimensionBinder)
            .GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(ICanonicalEntityCatalog), dependencies);
        Assert.Contains(typeof(IRelationshipJoinPlanner), dependencies);
        Assert.Contains(typeof(IRelationshipPlanSubjectKeyExecutor), dependencies);
    }

    [Fact]
    public void The_executor_cannot_choose_because_it_cannot_see_a_chooser()
    {
        var dependencies = typeof(IRelationshipPlanSubjectKeyExecutor).Assembly
            .GetTypes()
            .Where(t => typeof(IRelationshipPlanSubjectKeyExecutor).IsAssignableFrom(t) && !t.IsInterface)
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(IRelationshipResolver), dependencies);
        Assert.DoesNotContain(typeof(IRelationshipJoinPlanner), dependencies);
        Assert.DoesNotContain(typeof(IRelationshipService), dependencies);
        Assert.DoesNotContain(typeof(IRelationshipStore), dependencies);
    }

    [Fact]
    public void The_key_projection_reads_the_declared_field_and_the_supplied_member_as_data()
    {
        var rows = new[]
        {
            new RelatedRow { Id = Guid.NewGuid(), SubjectRef = Guid.NewGuid(), Bucket = "b1" },
            new RelatedRow { Id = Guid.NewGuid(), SubjectRef = Guid.NewGuid(), Bucket = "b2" },
        }.AsQueryable();

        // The member is HANDED to the projection by a governed plan now. The projection
        // still spells no entity, no column and no concept.
        var keys = DeclaredDimensionProjection.SubjectKeys(
            rows, Bindable("f", typeof(RelatedRow), "Bucket"), "SubjectRef", false, "b2");

        var text = keys.Expression.ToString();
        Assert.Contains("Bucket", text);
        Assert.Contains("SubjectRef", text);
        Assert.Contains("Distinct", text);
    }

    [Fact]
    public void The_governed_chain_is_composed_so_a_related_declaration_is_reachable_at_runtime()
    {
        var composition = File.ReadAllText(Path.Combine(
            ScopeAwareGenericity.RepositoryRoot(),
            "Backend", "PlantProcess.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("IDeclaredDimensionCatalog", composition, StringComparison.Ordinal);
        Assert.Contains("ICanonicalEntityCatalog", composition, StringComparison.Ordinal);
        Assert.Contains("IRelationshipPlanSubjectKeyExecutor", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("IDeclaredDimensionSubjectLinkResolver", composition, StringComparison.Ordinal);
    }
}
