using System;
using System.Text.Json.Nodes;
using PlantProcess.Application.Definitions.Canvas;
using Xunit;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// PPIQ T-253. THE GOVERNED OUTPUT TARGET AS CANONICAL CONTENT.
///
/// These are the content-level gates: what the store KEEPS, which is the part T-243
/// round-trips and T-245 binds. They deliberately reach no database - the question here
/// is what ContentJson says, not what a writer did with it afterwards.
/// </summary>
public sealed class CanvasOutputTargetContentTests
{
    private const string Target = "QualityEvent";
    private const string OtherTarget = "ProcessEvent";

    private const string GraphWithTarget =
        "{\"name\":\"a\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],\"joins\":[]}";

    private const string GraphWithoutTarget =
        "{\"name\":\"a\",\"targetEntity\":\"\",\"tables\":[\"t0\"],\"joins\":[]}";

    private static JsonObject Root(string content) => (JsonObject)JsonNode.Parse(content)!;

    [Fact]
    [Trait("Gate", "T253_GRAPH_TARGET_IS_CONTENT")]
    public void A_graph_writes_the_governed_target_into_the_hashed_root()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWithTarget, Target);

        Assert.Equal(Target, Root(content)[CanvasDefinitionContent.RootOutputTarget]!.GetValue<string>());
        Assert.Equal(Target, CanvasDefinitionContent.Read(content).OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T253_GRAPH_TARGET_REQUIRED")]
    public void A_graph_without_a_target_is_refused_and_never_defaulted()
    {
        Assert.Throws<ArgumentException>(() => CanvasDefinitionContent.ForGraph(GraphWithTarget, "   "));
    }

    [Fact]
    [Trait("Gate", "T253_GRAPH_TARGET_MISMATCH")]
    public void A_graph_that_names_one_entity_cannot_be_saved_against_another()
    {
        var error = Assert.Throws<ArgumentException>(
            () => CanvasDefinitionContent.ForGraph(GraphWithTarget, OtherTarget));

        Assert.Contains(Target, error.Message, StringComparison.Ordinal);
        Assert.Contains(OtherTarget, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Gate", "T253_GRAPH_TARGET_COMPATIBILITY")]
    public void A_graph_that_declares_no_target_of_its_own_still_takes_the_governed_one()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWithoutTarget, Target);

        Assert.Equal(Target, CanvasDefinitionContent.Read(content).OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T253_SQL_TARGET_IS_CONTENT")]
    public void Sql_writes_the_governed_target_into_the_hashed_root()
    {
        var content = CanvasDefinitionContent.ForSql("select 1", null, Target);

        Assert.Equal(Target, Root(content)[CanvasDefinitionContent.RootOutputTarget]!.GetValue<string>());
        Assert.Equal(Target, CanvasDefinitionContent.Read(content).OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T253_SQL_TARGET_REQUIRED")]
    public void Sql_without_a_target_is_refused_and_never_defaulted()
    {
        Assert.Throws<ArgumentException>(() => CanvasDefinitionContent.ForSql("select 1", null, null!));
    }

    [Fact]
    [Trait("Gate", "T253_LEGACY_GRAPH")]
    public void Legacy_graph_content_recovers_the_target_the_author_stated_in_the_graph()
    {
        var legacy = "{\"graph\":" + GraphWithTarget + ",\"representation\":\"graph\"}";

        Assert.Equal(Target, CanvasDefinitionContent.Read(legacy).OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T253_LEGACY_SQL_NULL")]
    public void Legacy_sql_content_reopens_with_no_target_rather_than_an_invented_one()
    {
        var legacy = "{\"representation\":\"sql\",\"sql\":\"select 1\"}";

        Assert.Null(CanvasDefinitionContent.Read(legacy).OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T253_EMPTY_IS_ABSENT")]
    public void An_empty_target_string_is_the_absence_of_a_target_not_a_target_named_empty()
    {
        var legacy = "{\"graph\":" + GraphWithoutTarget + ",\"representation\":\"graph\"}";

        Assert.Null(CanvasDefinitionContent.Read(legacy).OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T253_TARGET_CHANGES_IDENTITY")]
    public void Two_definitions_that_differ_only_in_target_are_not_the_same_content()
    {
        var a = CanvasDefinitionContent.ForSql("select 1", null, Target);
        var b = CanvasDefinitionContent.ForSql("select 1", null, OtherTarget);

        Assert.NotEqual(a, b);
    }

    [Fact]
    [Trait("Gate", "T253_DETERMINISM_HELD")]
    public void The_same_semantics_still_serialise_byte_identically()
    {
        var a = CanvasDefinitionContent.ForGraph(GraphWithTarget, Target);
        var b = CanvasDefinitionContent.ForGraph(GraphWithTarget, "  " + Target + "  ");

        Assert.Equal(a, b);
    }

    [Fact]
    [Trait("Gate", "T253_GRAPH_HELPER")]
    public void The_graph_target_helper_reads_a_stated_name_and_null_for_anything_else()
    {
        Assert.Equal(Target, CanvasDefinitionContent.TryReadGraphTargetEntity(GraphWithTarget));
        Assert.Null(CanvasDefinitionContent.TryReadGraphTargetEntity(GraphWithoutTarget));
        Assert.Null(CanvasDefinitionContent.TryReadGraphTargetEntity("not json"));
        Assert.Null(CanvasDefinitionContent.TryReadGraphTargetEntity(""));
    }
}