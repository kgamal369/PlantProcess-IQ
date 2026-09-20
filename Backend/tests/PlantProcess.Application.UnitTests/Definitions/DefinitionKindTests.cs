using PlantProcess.Application.Definitions;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// PPIQ T-039, corrected as the kind authority grew.
///
/// WHAT THIS PROTECTS, AND WHAT IT NEVER PROTECTED. The original assertion was a
/// COUNT of eleven, written when eleven was all there was. A count is not the
/// invariant: T-090 added 12..16 and the Industrial Integration configuration
/// authority adds 17, and each addition made the count assertion fail without
/// telling anyone anything about the property that actually matters.
///
/// The property that matters is that a PERSISTED numeric value never changes
/// meaning. Every historic member keeps its number, zero stays unused, and any
/// new member is additive and named here deliberately.
/// </summary>
public class DefinitionKindTests
{
    /// <summary>The eleven M1 members and their frozen numbers.</summary>
    private static readonly Dictionary<string, int> FrozenM1 = new(StringComparer.Ordinal)
    {
        ["Transformation"] = 1, ["Page"] = 2, ["Widget"] = 3, ["Analysis"] = 4,
        ["Model"] = 5, ["LogRule"] = 6, ["MasterDimension"] = 7, ["MasterMeasure"] = 8,
        ["Filter"] = 9, ["Hierarchy"] = 10, ["Bookmark"] = 11,
    };

    /// <summary>Everything added since, with the numbers already persisted for it.</summary>
    private static readonly Dictionary<string, int> AdditiveSinceM1 = new(StringComparer.Ordinal)
    {
        ["SavedQuery"] = 12, ["FeatureSet"] = 13, ["Practice"] = 14, ["Report"] = 15,
        ["Scenario"] = 16, ["AcquisitionConfiguration"] = 17,
    };

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
    public void Kind_enum_still_carries_every_M1_member()
    {
        var declared = Enum.GetValues<DefinitionKind>();

        foreach (var required in Required)
        {
            Assert.Contains(required, declared);
        }
    }

    /// <summary>
    /// Numbers, not a count. A renumbered member reinterprets every row that
    /// persisted it, which is invisible until the data is read back.
    /// </summary>
    [Fact]
    public void Historic_kind_numbers_never_move()
    {
        foreach (var (name, value) in FrozenM1)
        {
            Assert.True(Enum.TryParse<DefinitionKind>(name, out var parsed), name + " disappeared from DefinitionKind.");
            Assert.Equal(value, (int)parsed);
        }
    }

    /// <summary>
    /// Every member beyond M1 is additive and declared here on purpose.
    /// AcquisitionConfiguration = 17 is the Industrial Integration addition; a
    /// member that appears without being named here fails this test.
    /// </summary>
    [Fact]
    public void Every_member_beyond_M1_is_a_declared_additive_member()
    {
        foreach (var (name, value) in AdditiveSinceM1)
        {
            Assert.True(Enum.TryParse<DefinitionKind>(name, out var parsed), name + " disappeared from DefinitionKind.");
            Assert.Equal(value, (int)parsed);
        }

        var undeclared = Enum.GetNames<DefinitionKind>()
            .Where(name => !FrozenM1.ContainsKey(name) && !AdditiveSinceM1.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(undeclared.Length == 0, "Undeclared definition kind(s): " + string.Join(", ", undeclared));
        Assert.Equal(17, (int)DefinitionKind.AcquisitionConfiguration);
        Assert.Equal(FrozenM1.Count + AdditiveSinceM1.Count, Enum.GetValues<DefinitionKind>().Length);
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