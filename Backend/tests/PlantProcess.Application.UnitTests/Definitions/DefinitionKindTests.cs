using PlantProcess.Application.Definitions;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// PPIQ T-039. The frozen validation's second half: the kind enum carries all
/// eleven members. This is not a formality - the task exists because the
/// contract has to be final in M1, and a member added in M2a would break every
/// caller that switched on it.
/// </summary>
public class DefinitionKindTests
{
    private static readonly DefinitionKind[] Required =
    {
        // The five authoring purposes.
        DefinitionKind.Transformation,
        DefinitionKind.Page,
        DefinitionKind.Widget,
        DefinitionKind.Analysis,
        DefinitionKind.Model,
        DefinitionKind.LogRule,
        // The sub-kinds the design also versions.
        DefinitionKind.MasterDimension,
        DefinitionKind.MasterMeasure,
        DefinitionKind.Filter,
        DefinitionKind.Hierarchy,
        DefinitionKind.Bookmark,
    };

    [Fact]
    public void Kind_enum_carries_exactly_the_eleven_declared_members()
    {
        var declared = Enum.GetValues<DefinitionKind>();

        Assert.Equal(11, declared.Length);
        foreach (var required in Required)
        {
            Assert.Contains(required, declared);
        }
    }

    [Fact]
    public void Kind_enum_declares_no_two_members_with_the_same_value()
    {
        // A duplicated value is two names for one row in every store that
        // persists this enum, and it would be invisible until the data was
        // read back under the wrong name.
        var values = Enum.GetValues<DefinitionKind>().Select(k => (int)k).ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void Kind_enum_reserves_zero_so_an_unset_value_cannot_pass_as_a_kind()
    {
        // Default(DefinitionKind) must not be a real kind. A caller that forgot
        // to set it should fail, not silently create a transformation.
        Assert.False(Enum.IsDefined(typeof(DefinitionKind), 0));
    }

    [Fact]
    public void Widget_is_declared_because_M1_stores_it_and_the_validation_names_it()
    {
        Assert.True(Enum.IsDefined(typeof(DefinitionKind), DefinitionKind.Widget));
        Assert.Equal("Widget", DefinitionKind.Widget.ToString());
    }
}