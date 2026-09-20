using System.Reflection;
using PlantProcess.Application.Integration.Services.Mapping;
using PlantProcess.Domain.Entities.Materials;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class MappingFieldBindingAuthorityTests
{
    private static string? Read(string target, IReadOnlyDictionary<string, string> mapping,
        IReadOnlyDictionary<string, string?> source)
    {
        Type? type = typeof(MappingRowProjector).GetNestedType("RowReader", BindingFlags.NonPublic);
        Assert.NotNull(type);
        object instance = Activator.CreateInstance(type!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, args: new object[] { mapping, source }, culture: null)!;
        MethodInfo? method = type!.GetMethod("OptionalString", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        return (string?)method!.Invoke(instance, new object[] { target });
    }

    [Theory]
    [InlineData("raw_alpha", "raw_beta")]
    [InlineData("external_one", "external_two")]
    public void Canonical_members_consume_explicit_foreign_field_bindings(string first, string second)
    {
        var mapping = new Dictionary<string, string>
        {
            [nameof(MaterialUnit.ProductFamily)] = first,
            [nameof(MaterialUnit.GradeOrRecipe)] = second
        };
        var source = new Dictionary<string, string?> { [first] = "A", [second] = "B" };
        Assert.Equal("A", Read(nameof(MaterialUnit.ProductFamily), mapping, source));
        Assert.Equal("B", Read(nameof(MaterialUnit.GradeOrRecipe), mapping, source));
    }

    [Fact]
    public void Conventional_source_names_without_a_declared_mapping_are_not_inferred()
    {
        var source = new Dictionary<string, string?>
        {
            [nameof(MaterialUnit.ProductFamily)] = "must-not-be-inferred",
            [nameof(MaterialUnit.GradeOrRecipe)] = "must-not-be-inferred"
        };
        var mapping = new Dictionary<string, string>();
        Assert.Null(Read(nameof(MaterialUnit.ProductFamily), mapping, source));
        Assert.Null(Read(nameof(MaterialUnit.GradeOrRecipe), mapping, source));
    }

    [Fact]
    public void Declared_foreign_binding_wins_over_a_conventional_but_unmapped_source_key()
    {
        var mapping = new Dictionary<string, string> { [nameof(MaterialUnit.ProductFamily)] = "declared_key" };
        var source = new Dictionary<string, string?>
        {
            ["declared_key"] = "correct",
            [nameof(MaterialUnit.ProductFamily)] = "wrong"
        };
        Assert.Equal("correct", Read(nameof(MaterialUnit.ProductFamily), mapping, source));
    }
}
