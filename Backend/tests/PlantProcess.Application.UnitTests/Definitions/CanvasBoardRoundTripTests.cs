using System;
using System.Text.Json.Nodes;
using PlantProcess.Application.Definitions.Canvas;
using Xunit;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// PPIQ T-243. THE AUTHORED BOARD AS CANONICAL CONTENT.
///
/// The question these gates answer is not "does it run" - T-253 and the engine tests
/// answer that. It is "can this be opened again": does the store keep the blocks, the
/// positions, the wiring and the purpose, and does an older version survive a newer one
/// being written.
///
/// No database. The subject is what ContentJson says.
/// </summary>
public sealed class CanvasBoardRoundTripTests
{
    private const string Target = "QualityEvent";

    private static string GraphWithBoard(string filterValue, int x)
        => "{\"name\":\"a\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],\"joins\":[],"
         + "\"filters\":[{\"table\":\"t0\",\"column\":\"quantity\",\"op\":\">\",\"value\":\"" + filterValue + "\"}],"
         + "\"board\":{\"purpose\":\"S1\","
         + "\"nodes\":[{\"id\":\"t0\",\"kind\":\"dataset\",\"position\":{\"x\":" + x + ",\"y\":90},\"data\":{}},"
         + "{\"id\":\"f1\",\"kind\":\"filter\",\"position\":{\"x\":460,\"y\":320},\"data\":{\"column\":\"quantity\"}}],"
         + "\"edges\":[{\"source\":\"t0\",\"target\":\"f1\",\"sourceHandle\":\"flow:out\",\"targetHandle\":\"flow:in\"}]}}";

    private static JsonObject Root(string content) => (JsonObject)JsonNode.Parse(content)!;

    [Fact]
    [Trait("Gate", "T243_BOARD_IS_CONTENT")]
    public void The_authored_board_is_kept_at_the_content_root()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);
        var board = Root(content)[CanvasDefinitionContent.RootBoard];

        Assert.NotNull(board);
        Assert.Equal("S1", board!["purpose"]!.GetValue<string>());
        Assert.Equal(2, board["nodes"]!.AsArray().Count);
        Assert.Equal(1, board["edges"]!.AsArray().Count);
    }

    [Fact]
    [Trait("Gate", "T243_EXECUTION_SHAPE_STAYS_CLEAN")]
    public void The_stored_graph_is_the_execution_shape_and_carries_no_board()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);
        var graph = Root(content)["graph"]!.AsObject();

        // The SQL generator reads this object. A board inside it would be a second
        // meaning for one payload, which is what lifting it to the root prevents.
        Assert.False(graph.ContainsKey("board"));
        Assert.True(graph.ContainsKey("tables"));
    }

    [Fact]
    [Trait("Gate", "T243_BOARD_ROUND_TRIP")]
    public void Reading_content_back_returns_the_board_that_was_authored()
    {
        var content = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);
        var read = CanvasDefinitionContent.Read(content);

        Assert.NotNull(read.BoardJson);

        var board = (JsonObject)JsonNode.Parse(read.BoardJson!)!;
        var nodes = board["nodes"]!.AsArray();

        Assert.Equal("t0", nodes[0]!["id"]!.GetValue<string>());
        Assert.Equal("dataset", nodes[0]!["kind"]!.GetValue<string>());
        Assert.Equal(80, nodes[0]!["position"]!["x"]!.GetValue<int>());
        Assert.Equal("filter", nodes[1]!["kind"]!.GetValue<string>());
        Assert.Equal(Target, read.OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T243_LEGACY_GRAPH_HAS_NO_BOARD")]
    public void A_version_saved_before_boards_were_kept_reopens_with_none()
    {
        // Exactly the pre-T-243 shape: graph, target, representation, no board.
        var legacy = "{\"graph\":{\"name\":\"a\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],\"joins\":[]},"
                   + "\"outputTarget\":\"QualityEvent\",\"representation\":\"graph\"}";

        var read = CanvasDefinitionContent.Read(legacy);

        Assert.Null(read.BoardJson);
        Assert.Equal(Target, read.OutputTarget);
    }

    [Fact]
    [Trait("Gate", "T243_SQL_HAS_NO_BOARD")]
    public void A_sql_definition_never_claims_a_board()
    {
        var content = CanvasDefinitionContent.ForSql("select 1", null, Target);

        Assert.Null(CanvasDefinitionContent.Read(content).BoardJson);
    }

    // -------------------------------------------------------- IMMUTABILITY

    [Fact]
    [Trait("Gate", "T243_V1_UNCHANGED_BY_V2")]
    public void Writing_a_second_version_does_not_alter_the_first()
    {
        // V1 is captured as bytes BEFORE V2 exists, then compared after. The canonical
        // writer hashes exactly this string, so identical bytes is identical identity.
        var v1 = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);
        var v1Again = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);

        var v2 = CanvasDefinitionContent.ForGraph(GraphWithBoard("20", 80), Target);

        Assert.Equal(v1, v1Again);
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    [Trait("Gate", "T243_UNCHANGED_BOARD_IS_THE_SAME_VERSION")]
    public void Saving_an_untouched_board_produces_byte_identical_content()
    {
        var a = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);
        var b = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);

        Assert.Equal(a, b);
    }

    [Fact]
    [Trait("Gate", "T243_LAYOUT_IS_AUTHORED_STATE")]
    public void Moving_a_block_changes_the_content_and_therefore_the_version()
    {
        // STATED, NOT DISCOVERED. The writer hashes the whole content and there is no
        // unhashed sibling to hide layout in, so a moved block IS a new version. The
        // alternative was not persisting position, which is the thing this task exists
        // to fix.
        var placed = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);
        var moved = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 240), Target);

        Assert.NotEqual(placed, moved);
    }

    [Fact]
    [Trait("Gate", "T243_DETERMINISM")]
    public void Key_order_in_the_authored_payload_does_not_change_identity()
    {
        var ordered = CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), Target);

        var reordered = CanvasDefinitionContent.ForGraph(
            "{\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],"
            + "\"board\":{\"edges\":[{\"targetHandle\":\"flow:in\",\"sourceHandle\":\"flow:out\",\"target\":\"f1\",\"source\":\"t0\"}],"
            + "\"nodes\":[{\"position\":{\"y\":90,\"x\":80},\"kind\":\"dataset\",\"data\":{},\"id\":\"t0\"},"
            + "{\"data\":{\"column\":\"quantity\"},\"position\":{\"y\":320,\"x\":460},\"id\":\"f1\",\"kind\":\"filter\"}],"
            + "\"purpose\":\"S1\"},"
            + "\"joins\":[],\"filters\":[{\"value\":\"10\",\"op\":\">\",\"column\":\"quantity\",\"table\":\"t0\"}],"
            + "\"name\":\"a\"}",
            Target);

        Assert.Equal(ordered, reordered);
    }

    [Fact]
    [Trait("Gate", "T243_TARGET_STILL_GOVERNED")]
    public void A_board_does_not_loosen_the_governed_target_rule()
    {
        Assert.Throws<ArgumentException>(
            () => CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), "   "));

        Assert.Throws<ArgumentException>(
            () => CanvasDefinitionContent.ForGraph(GraphWithBoard("10", 80), "ProcessEvent"));
    }
}