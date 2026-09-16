using PlantProcess.Application.Definitions.Canvas;
using Xunit;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// T-243. What an immutable Canvas version declares as a relationship, and what it
/// does not.
///
/// A JOIN IS NOT A RELATIONSHIP. JoinSpec carries two tables and two columns because
/// that is all a SELECT needs. A relationship additionally requires a code, a join
/// type, a cardinality and a grain on each side, and none of those can be recovered
/// from a join. Promoting one into the other would emit a relationship model the
/// author never stated, so a graph that only joins declares nothing at all.
/// </summary>
public sealed class CanvasRelationshipDeclarationTests
{
    private const string Target = "QualityEvent";

    private static string WithRelationship(string code) =>
        "{\"name\":\"r\",\"targetEntity\":\"" + Target + "\",\"tables\":[\"t0\",\"t1\"],"
        + "\"joins\":[{\"leftTable\":\"t0\",\"leftColumn\":\"id\",\"rightTable\":\"t1\",\"rightColumn\":\"t0_id\"}],"
        + "\"relationships\":[{\"relationshipCode\":\"" + code + "\",\"leftEntity\":\"t0\",\"rightEntity\":\"t1\","
        + "\"joinType\":\"inner\",\"cardinality\":\"one-to-many\",\"grainLeft\":\"row\",\"grainRight\":\"row\","
        + "\"isPreferredPath\":true,\"members\":[{\"leftColumn\":\"id\",\"rightColumn\":\"t0_id\",\"memberOrder\":1}]}]}";

    private const string JoinOnly =
        "{\"name\":\"j\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\",\"t1\"],"
        + "\"joins\":[{\"leftTable\":\"t0\",\"leftColumn\":\"id\",\"rightTable\":\"t1\",\"rightColumn\":\"t0_id\"}]}";

    private const string IncompleteDeclaration =
        "{\"name\":\"x\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],\"joins\":[],"
        + "\"relationships\":[{\"relationshipCode\":\"BAD\",\"leftEntity\":\"t0\",\"rightEntity\":\"t1\","
        + "\"joinType\":\"inner\",\"members\":[{\"leftColumn\":\"id\",\"rightColumn\":\"t0_id\"}]}]}";

    private const string DeclaredWithoutMembers =
        "{\"name\":\"m\",\"targetEntity\":\"QualityEvent\",\"tables\":[\"t0\"],\"joins\":[],"
        + "\"relationships\":[{\"relationshipCode\":\"NOMEM\",\"leftEntity\":\"t0\",\"rightEntity\":\"t1\","
        + "\"joinType\":\"inner\",\"cardinality\":\"one-to-many\",\"grainLeft\":\"row\",\"grainRight\":\"row\","
        + "\"members\":[]}]}";

    [Fact]
    public void A_graph_that_only_joins_declares_no_relationship()
    {
        var read = CanvasRelationshipDeclarations.Read(JoinOnly, out var refusal);

        Assert.Null(refusal);
        Assert.Empty(read);
    }

    [Fact]
    public void A_version_with_no_graph_declares_nothing_and_is_not_refused()
    {
        var read = CanvasRelationshipDeclarations.Read(null, out var refusal);

        Assert.Null(refusal);
        Assert.Empty(read);
    }

    [Fact]
    public void An_incomplete_declaration_is_refused_by_name_rather_than_defaulted()
    {
        var read = CanvasRelationshipDeclarations.Read(IncompleteDeclaration, out var refusal);

        Assert.Equal(CanvasRelationshipDeclarations.FieldMissingCode, refusal);
        Assert.Empty(read);
    }

    [Fact]
    public void A_declaration_without_members_is_refused()
    {
        var read = CanvasRelationshipDeclarations.Read(DeclaredWithoutMembers, out var refusal);

        Assert.Equal(CanvasRelationshipDeclarations.MemberMissingCode, refusal);
        Assert.Empty(read);
    }

    [Fact]
    public void A_complete_declaration_is_read_with_its_exact_semantics()
    {
        var read = CanvasRelationshipDeclarations.Read(WithRelationship("REL_A"), out var refusal);

        Assert.Null(refusal);
        var one = Assert.Single(read);
        Assert.Equal("REL_A", one.RelationshipCode);
        Assert.Equal("t0", one.LeftEntity);
        Assert.Equal("t1", one.RightEntity);
        Assert.Equal("inner", one.JoinType);
        Assert.Equal("one-to-many", one.Cardinality);
        Assert.Equal("row", one.GrainLeft);
        Assert.Equal("row", one.GrainRight);
        Assert.True(one.IsPreferredPath);
        var member = Assert.Single(one.Members);
        Assert.Equal("id", member.LeftColumn);
        Assert.Equal("t0_id", member.RightColumn);
        Assert.Equal("=", member.Comparison);
    }

    [Fact]
    public void Reading_the_same_version_twice_yields_the_same_declaration()
    {
        var first = CanvasRelationshipDeclarations.Read(WithRelationship("REL_B"), out _);
        var second = CanvasRelationshipDeclarations.Read(WithRelationship("REL_B"), out _);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first[0].RelationshipCode, second[0].RelationshipCode);
        Assert.Equal(first[0].Cardinality, second[0].Cardinality);
    }
}