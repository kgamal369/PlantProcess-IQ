using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Dashboarding.Services.Queries;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-094 wave A, part 1: the declared-dimension execution binding is generic.
///
/// These tests prove the mechanism without a database: a projection is rewritten to
/// bind the generic slot through EF.Property on the declared field, a mismatched or
/// unbindable declaration is a typed refusal, a well-formed unknown code is admitted
/// to execution where the catalogue decides, and the execution slot is grammar the
/// executor understands. Nothing here names a plant concept.
/// </summary>
[Trait("BacklogTask", "T-094")]
[Trait("Gate", "DeclaredDimensionBinding")]
public sealed class DeclaredDimensionBindingTests
{
    // Neutral fixture types: no customer vocabulary anywhere in this file.
    private sealed class SourceRow
    {
        public Guid Id { get; set; }
        public string? Tag { get; set; }
        public string? Bucket { get; set; }
    }

    private sealed class OtherSourceRow
    {
        public Guid Id { get; set; }
    }

    private sealed class Fact
    {
        public Guid? Id { get; init; }
        public string? DimensionText { get; init; }
        public decimal Value { get; init; }
    }

    private static DeclaredDimension Bindable(string code, Type entity, string field) =>
        new(code, code, "string", "material", entity.Name, field, entity, true, null, null, Guid.NewGuid(), 1);

    // Expression-node assertions use IsAssignableFrom, not IsType: the BCL returns
    // internal specialisations (MethodCallExpression2, TypedConstantExpression) whose
    // exact runtime type is an implementation detail. The node KIND is the contract.
    [Fact]
    public void A_projection_is_rewritten_to_bind_the_slot_through_EF_Property_on_the_declared_field()
    {
        Expression<Func<SourceRow, Fact>> projection = x => new Fact { Id = x.Id, Value = 1m };

        var bound = DeclaredDimensionProjection.WithDeclaredDimension(projection, Bindable("anyCode", typeof(SourceRow), "Bucket"));

        var init = Assert.IsAssignableFrom<MemberInitExpression>(bound.Body);
        var slot = Assert.Single(init.Bindings, b => b.Member.Name == DeclaredDimensionProjection.SlotMember);
        var assignment = Assert.IsAssignableFrom<MemberAssignment>(slot);
        var call = Assert.IsAssignableFrom<MethodCallExpression>(assignment.Expression);

        Assert.Equal("Property", call.Method.Name);
        Assert.Equal("EF", call.Method.DeclaringType!.Name);
        Assert.Equal("Bucket", Assert.IsAssignableFrom<ConstantExpression>(call.Arguments[1]).Value);

        // Every original binding survives.
        Assert.Contains(init.Bindings, b => b.Member.Name == nameof(Fact.Id));
        Assert.Contains(init.Bindings, b => b.Member.Name == nameof(Fact.Value));
        Assert.Same(projection.Parameters[0], bound.Parameters[0]);
    }

    [Fact]
    public void The_declared_field_name_is_data_not_source()
    {
        Expression<Func<SourceRow, Fact>> projection = x => new Fact { Id = x.Id };

        var boundA = DeclaredDimensionProjection.WithDeclaredDimension(projection, Bindable("a", typeof(SourceRow), "Tag"));
        var boundB = DeclaredDimensionProjection.WithDeclaredDimension(projection, Bindable("b", typeof(SourceRow), "Bucket"));

        static string FieldOf(Expression<Func<SourceRow, Fact>> e) =>
            (string)((ConstantExpression)((MethodCallExpression)((MemberAssignment)((MemberInitExpression)e.Body).Bindings
                .Single(b => b.Member.Name == DeclaredDimensionProjection.SlotMember)).Expression).Arguments[1]).Value!;

        Assert.Equal("Tag", FieldOf(boundA));
        Assert.Equal("Bucket", FieldOf(boundB));
    }

    [Fact]
    public void A_declaration_bound_to_another_entity_is_a_typed_refusal_not_a_join()
    {
        Expression<Func<SourceRow, Fact>> projection = x => new Fact { Id = x.Id };

        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.WithDeclaredDimension(projection, Bindable("elsewhere", typeof(OtherSourceRow), "Id")));

        Assert.Equal(DimensionBindingRefusalCodes.SourceMismatch, refusal.RefusalCode);
        Assert.Equal("elsewhere", refusal.DimensionCode);
    }

    [Fact]
    public void An_unbindable_declaration_is_refused_with_its_own_reason()
    {
        Expression<Func<SourceRow, Fact>> projection = x => new Fact { Id = x.Id };
        var unbindable = new DeclaredDimension(
            "ghost", "ghost", "string", "material", "NoSuchEntity", "NoSuchField",
            null, false, DimensionBindingRefusalCodes.Unbindable, "no such entity", Guid.NewGuid(), 3);

        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.WithDeclaredDimension(projection, unbindable));

        Assert.Equal(DimensionBindingRefusalCodes.Unbindable, refusal.RefusalCode);
        Assert.Contains("no such entity", refusal.Message);
    }

    [Fact]
    public void A_declared_filter_becomes_an_equality_on_EF_Property()
    {
        var source = new[] { new SourceRow { Bucket = "b1" }, new SourceRow { Bucket = "b2" } }.AsQueryable();

        var filtered = DeclaredDimensionProjection.WhereDeclaredEquals(source, Bindable("c", typeof(SourceRow), "Bucket"), "b2");

        var where = Assert.IsAssignableFrom<MethodCallExpression>(filtered.Expression);
        Assert.Equal("Where", where.Method.Name);
        var lambda = (LambdaExpression)((UnaryExpression)where.Arguments[1]).Operand;
        var equal = Assert.IsAssignableFrom<BinaryExpression>(lambda.Body);
        Assert.Equal(ExpressionType.Equal, equal.NodeType);
        Assert.Equal("Property", Assert.IsAssignableFrom<MethodCallExpression>(equal.Left).Method.Name);
        Assert.Equal("b2", Assert.IsAssignableFrom<ConstantExpression>(equal.Right).Value);
    }

    [Fact]
    public void The_executor_knows_the_declared_slot_as_grammar()
    {
        Assert.Equal(
            DeclaredDimensionProjection.SlotMember,
            DashboardSourceCapability.RequiredMemberName(DeclaredDimensionProjection.SlotCode));

        Assert.True(DeclaredDimensionProjection.IsSlot(" $declared "));
        Assert.False(DeclaredDimensionProjection.IsSlot("declared"));
    }

    [Fact]
    public void Validation_admits_well_formed_unknown_codes_and_still_rejects_malformed_ones()
    {
        // A well-formed code the compiled registry does not know is not a validation
        // error any more: whether it is DECLARED is the catalogue's question, answered
        // at execution as a typed refusal. Malformed input stays a validation error.
        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedDimension("customerConcept"));
        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedDimension("equipment"));
        Assert.False(DashboardWidgetQuerySafetyRegistry.IsSupportedDimension("9starts-with-digit"));
        Assert.False(DashboardWidgetQuerySafetyRegistry.IsSupportedDimension("has space"));
        Assert.False(DashboardWidgetQuerySafetyRegistry.IsSupportedDimension(DeclaredDimensionProjection.SlotCode));
        Assert.False(DashboardWidgetQuerySafetyRegistry.IsSupportedDimension(null));
    }

    [Fact]
    public void The_binding_contract_sources_carry_no_plant_vocabulary()
    {
        var root = ScopeAwareGenericity.RepositoryRoot();
        var files = new[]
        {
            "Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimension.cs",
            "Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/IDeclaredDimensionCatalog.cs",
            "Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DimensionBindingRefusalException.cs",
            "Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimensionProjection.cs",
            "Backend/PlantProcess.Infrastructure/Dashboarding/Dimensions/DeclaredDimensionCatalog.cs"
        };

        var terms = PlantVocabularyAuthority.TermsFor(PlantVocabularyAuthority.GenericityGateConsumer);

        foreach (var relative in files)
        {
            var text = ScopeAwareGenericity.StripComments(File.ReadAllText(
                Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))));

            foreach (var term in terms)
            {
                Assert.DoesNotContain(term, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}