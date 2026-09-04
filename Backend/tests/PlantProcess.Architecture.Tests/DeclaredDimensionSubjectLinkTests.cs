using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-094 part 2b, stage 1: a declaration published against a related canonical entity
/// becomes executable through the single mapped reference that entity carries to the
/// subject of the population.
///
/// The decision proved here is arithmetic over what the model declares - one candidate
/// binds, none refuses, several refuse. No name is compared anywhere, so a customer
/// concept published against any related entity resolves by the same rule. Reading the
/// model and translating the query is the resolver's work and is proved against a real
/// database by the execution falsification, not here.
/// </summary>
[Trait("BacklogTask", "T-094")]
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
    public void Exactly_one_mapped_reference_is_the_link()
    {
        Assert.Equal(
            "SubjectRef",
            DeclaredDimensionProjection.SelectSubjectLinkField("c", new List<string> { "SubjectRef" }));
    }

    [Fact]
    public void No_reference_to_the_subject_is_a_typed_refusal_rather_than_an_empty_result()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.SelectSubjectLinkField("d", Array.Empty<string>()));

        Assert.Equal(DimensionBindingRefusalCodes.SubjectLinkAbsent, refusal.RefusalCode);
        Assert.Equal("d", refusal.DimensionCode);
    }

    [Fact]
    public void More_than_one_reference_is_refused_rather_than_chosen()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.SelectSubjectLinkField("e", new List<string> { "FirstRef", "SecondRef" }));

        Assert.Equal(DimensionBindingRefusalCodes.SubjectLinkAmbiguous, refusal.RefusalCode);
        Assert.Contains("2", refusal.Message);
    }

    [Fact]
    public void A_published_but_unexecutable_declaration_is_refused_before_any_link_is_sought()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.RequireBindableAnywhere(Unbindable("ghost")));

        Assert.Equal(DimensionBindingRefusalCodes.Unbindable, refusal.RefusalCode);
        Assert.Contains("no such entity", refusal.Message);
    }

    [Fact]
    public void The_key_projection_reads_the_declared_field_and_the_link_field_as_data()
    {
        var rows = new[]
        {
            new RelatedRow { Id = Guid.NewGuid(), SubjectRef = Guid.NewGuid(), Bucket = "b1" },
            new RelatedRow { Id = Guid.NewGuid(), SubjectRef = Guid.NewGuid(), Bucket = "b2" },
        }.AsQueryable();

        var keys = DeclaredDimensionProjection.SubjectKeys(
            rows, Bindable("f", typeof(RelatedRow), "Bucket"), "SubjectRef", false, "b2");

        var text = keys.Expression.ToString();
        Assert.Contains("Bucket", text);
        Assert.Contains("SubjectRef", text);
        Assert.Contains("Distinct", text);
    }

    [Fact]
    public void The_structural_refusal_codes_are_distinct_and_stated()
    {
        Assert.Equal("DB06_subject_link_absent", DimensionBindingRefusalCodes.SubjectLinkAbsent);
        Assert.Equal("DB07_subject_link_ambiguous", DimensionBindingRefusalCodes.SubjectLinkAmbiguous);
        Assert.Equal("DB08_subject_link_unavailable", DimensionBindingRefusalCodes.SubjectLinkUnavailable);
        Assert.NotEqual(DimensionBindingRefusalCodes.SubjectLinkAbsent, DimensionBindingRefusalCodes.SourceMismatch);
    }

    [Fact]
    public void The_resolver_is_composed_so_a_related_declaration_is_reachable_at_runtime()
    {
        // A capability that is registered nowhere refuses every query it was built to
        // answer, and no unit test of the contract would notice.
        var composition = File.ReadAllText(Path.Combine(
            ScopeAwareGenericity.RepositoryRoot(),
            "Backend", "PlantProcess.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("IDeclaredDimensionCatalog", composition, StringComparison.Ordinal);
        Assert.Contains("IDeclaredDimensionSubjectLinkResolver", composition, StringComparison.Ordinal);
    }
}