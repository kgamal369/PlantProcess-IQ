using System;
using System.Text.Json.Nodes;
using PlantProcess.Application.Definitions.Canvas;
using Xunit;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// PPIQ T-262. THE PROJECTION DECLARATION AS IMMUTABLE CONTENT.
///
/// What the store KEEPS, and what it refuses to keep. No database: the subject is the
/// declaration and the content bytes, not what a writer later did with them.
/// </summary>
public sealed class CanvasProjectionDeclarationTests
{
    private const string Target = "QualityEvent";

    private const string Graph =
        "{\"name\":\"a\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],\"joins\":[]}";

    private static string Declaration(params string[] pairs)
    {
        var bindings = new JsonArray();
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            bindings.Add(new JsonObject
            {
                ["targetField"] = parts[0],
                ["sourceKind"] = "column",
                ["sourceTable"] = "t0",
                ["sourceField"] = parts[1],
            });
        }

        return new JsonObject { ["targetEntity"] = Target, ["fieldBindings"] = bindings }.ToJsonString();
    }

    private static string GraphWith(string declarationJson)
    {
        var g = (JsonObject)JsonNode.Parse(Graph)!;
        g[CanvasDefinitionContent.RootProjection] = JsonNode.Parse(declarationJson);
        return g.ToJsonString();
    }

    [Fact]
    [Trait("Gate", "T262_DECLARATION_IS_CONTENT")]
    public void The_declaration_is_kept_at_the_content_root_and_read_back()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWith(Declaration("Quantity=qty")), Target);
        var read = CanvasDefinitionContent.Read(content);

        Assert.NotNull(read.ProjectionJson);
        var parseError = CanvasProjectionDeclaration.TryParse(read.ProjectionJson, out var declaration);
        Assert.Null(parseError);
        Assert.Equal(Target, declaration!.TargetEntity);
        Assert.Single(declaration.FieldBindings);
        Assert.Equal("Quantity", declaration.FieldBindings[0].TargetField);
        Assert.Equal("qty", declaration.FieldBindings[0].SourceField);
        Assert.Equal("t0", declaration.FieldBindings[0].SourceTable);
    }

    [Fact]
    [Trait("Gate", "T262_EXECUTION_SHAPE_STAYS_CLEAN")]
    public void The_stored_graph_carries_no_declaration()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWith(Declaration("Quantity=qty")), Target);
        var graph = ((JsonObject)JsonNode.Parse(content)!)["graph"]!.AsObject();

        // The SQL generator reads this object. A declaration inside it would be a second
        // meaning for one payload, which is why it is lifted to the root.
        Assert.False(graph.ContainsKey(CanvasDefinitionContent.RootProjection));
    }

    [Fact]
    [Trait("Gate", "T262_REORDER_IS_NOT_A_DECISION")]
    public void Binding_order_does_not_change_what_a_definition_means()
    {
        var a = new CanvasProjectionDeclaration(Target, new[]
        {
            new ProjectionFieldBinding("Quantity", "column", "t0", "qty"),
            new ProjectionFieldBinding("Grade", "column", "t0", "grd"),
        });
        var b = new CanvasProjectionDeclaration(Target, new[]
        {
            new ProjectionFieldBinding("Grade", "column", "t0", "grd"),
            new ProjectionFieldBinding("Quantity", "column", "t0", "qty"),
        });

        Assert.Equal(a.ToJson(), b.ToJson());
        Assert.Equal(
            CanvasDefinitionContent.ForGraph(GraphWith(a.ToJson()), Target),
            CanvasDefinitionContent.ForGraph(GraphWith(b.ToJson()), Target));
    }

    [Fact]
    [Trait("Gate", "T262_BINDING_CHANGE_IS_A_NEW_VERSION")]
    public void Changing_a_binding_changes_the_content()
    {
        var one = CanvasDefinitionContent.ForGraph(GraphWith(Declaration("Quantity=qty")), Target);
        var two = CanvasDefinitionContent.ForGraph(GraphWith(Declaration("Quantity=quantity")), Target);

        Assert.NotEqual(one, two);
    }

    [Fact]
    [Trait("Gate", "T262_SQL_CARRIES_THE_SAME_SEMANTICS")]
    public void A_sql_definition_carries_the_same_declaration()
    {
        var content = CanvasDefinitionContent.ForSql("select 1", null, Target, Declaration("Quantity=qty"));
        var read = CanvasDefinitionContent.Read(content);

        Assert.NotNull(read.ProjectionJson);
        Assert.Null(CanvasProjectionDeclaration.TryParse(read.ProjectionJson, out var d));
        Assert.Equal(Target, d!.TargetEntity);
    }

    [Fact]
    [Trait("Gate", "T262_LEGACY_IS_NULL")]
    public void A_version_written_before_this_task_declares_no_projection()
    {
        var legacy = "{\"graph\":" + Graph + ",\"outputTarget\":\"QualityEvent\",\"representation\":\"graph\"}";

        Assert.Null(CanvasDefinitionContent.Read(legacy).ProjectionJson);
    }

    // ------------------------------------------------------------- REFUSALS

    [Fact]
    [Trait("Gate", "T262_PARSE_REFUSALS")]
    public void A_payload_that_cannot_be_a_declaration_is_named_rather_than_defaulted()
    {
        Assert.NotNull(CanvasProjectionDeclaration.TryParse("not json", out _));
        Assert.NotNull(CanvasProjectionDeclaration.TryParse("[]", out _));
        Assert.NotNull(CanvasProjectionDeclaration.TryParse(
            "{\"targetEntity\":\"QualityEvent\",\"fieldBindings\":[]}", out _));
        Assert.NotNull(CanvasProjectionDeclaration.TryParse(
            "{\"fieldBindings\":[{\"targetField\":\"Quantity\",\"sourceKind\":\"column\",\"sourceTable\":\"t0\",\"sourceField\":\"qty\"}]}", out _));
    }

    [Fact]
    [Trait("Gate", "T262_COLUMN_NEEDS_ITS_TABLE")]
    public void A_selected_column_is_identified_by_table_and_column_never_by_name_alone()
    {
        var error = CanvasProjectionDeclaration.TryParse(
            "{\"targetEntity\":\"QualityEvent\",\"fieldBindings\":[{\"targetField\":\"Quantity\","
            + "\"sourceKind\":\"column\",\"sourceField\":\"qty\"}]}", out _);

        Assert.NotNull(error);
        Assert.Contains("table", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Gate", "T262_SOURCE_KIND_IS_CLOSED")]
    public void An_unrecognised_source_kind_is_refused()
    {
        var error = CanvasProjectionDeclaration.TryParse(
            "{\"targetEntity\":\"QualityEvent\",\"fieldBindings\":[{\"targetField\":\"Quantity\","
            + "\"sourceKind\":\"ordinal\",\"sourceField\":\"1\"}]}", out _);

        Assert.NotNull(error);
    }

    [Fact]
    [Trait("Gate", "T262_AMBIGUOUS_SQL_OUTPUT")]
    public void Two_bindings_to_one_sql_output_name_do_not_identify_one_value()
    {
        var declaration = new CanvasProjectionDeclaration(Target, new[]
        {
            new ProjectionFieldBinding("Quantity", "sql", null, "value"),
            new ProjectionFieldBinding("Grade", "sql", null, "value"),
        });

        Assert.Equal("value", CanvasProjectionDeclaration.FirstAmbiguousSource(declaration));
    }

    // -------------------------------------------------- DETAIL PROJECTION

    [Fact]
    [Trait("Gate", "T262_DETAIL_IS_DERIVED")]
    public void The_detail_projection_is_derived_from_the_declaration()
    {
        var json = CanvasProjectionDeclaration.ToDetailProjection(Declaration("Quantity=qty"), Target);
        var array = JsonNode.Parse(json)!.AsArray();

        Assert.Single(array);
        Assert.Equal(Target, array[0]!["entity"]!.GetValue<string>());
        Assert.Equal("Quantity", array[0]!["fieldBindings"]!.AsArray()[0]!["targetField"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Gate", "T262_DETAIL_NEVER_INVENTS")]
    public void A_version_with_no_declaration_projects_an_empty_detail()
    {
        Assert.Equal("[]", CanvasProjectionDeclaration.ToDetailProjection(null, Target));
    }

    // ------------------------------------------------------ COMPATIBILITY

    [Theory]
    [Trait("Gate", "T262_TYPE_COMPATIBILITY")]
    [InlineData("int32", "Int32", true)]
    [InlineData("integer", "Int64", true)]
    [InlineData("bigint", "Int32", false)]
    [InlineData("integer", "Decimal", true)]
    [InlineData("numeric", "Int32", false)]
    [InlineData("text", "Int32", false)]
    [InlineData("integer", "String", false)]
    [InlineData("timestamp without time zone", "DateTime", true)]
    [InlineData("boolean", "Boolean", true)]
    [InlineData("uuid", "Guid", true)]
    [InlineData("something_unmapped", "Int32", false)]
    [InlineData(null, "Int32", false)]
    public void One_authority_decides_whether_a_value_may_be_written(string? source, string target, bool expected)
    {
        Assert.Equal(expected, ProjectionTypeCompatibility.IsCompatible(source, target));
    }
}