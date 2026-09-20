using PlantProcess.Application.Definitions.Transformations;
using Xunit;

namespace PlantProcess.Application.UnitTests.Definitions;

/// <summary>
/// The compiler moved out of the API endpoint. These prove the grammar still refuses
/// what it always refused, from its new home, so the move cannot have quietly relaxed
/// it. They are not a redesign of the grammar and assert nothing new about it.
/// </summary>
public sealed class TransformationCompilerTests
{
    private static MapperGraph Graph(params string[] tables)
    {
        return new MapperGraph("g", "Target", tables, Array.Empty<JoinSpec>());
    }

    [Fact]
    public void An_illegal_schema_identifier_is_refused()
    {
        var (sql, err, _) = TransformationSafeSelect.BuildSafeSelect(Graph("t"), "public; drop table x");

        Assert.Null(sql);
        Assert.NotNull(err);
    }

    [Fact]
    public void An_illegal_table_identifier_is_refused()
    {
        var (sql, err, _) = TransformationSafeSelect.BuildSafeSelect(Graph("t\"x"), "ppiq_staging");

        Assert.Null(sql);
        Assert.NotNull(err);
    }

    [Fact]
    public void A_graph_with_no_tables_is_refused()
    {
        var (sql, err, _) = TransformationSafeSelect.BuildSafeSelect(Graph(), "ppiq_staging");

        Assert.Null(sql);
        Assert.NotNull(err);
    }

    [Fact]
    public void An_empty_select_block_is_refused_rather_than_defaulted_to_all_columns()
    {
        var graph = new MapperGraph(
            "g", "Target", new[] { "t" }, Array.Empty<JoinSpec>(),
            null, null, Array.Empty<SelectSpec>());

        var (sql, err, _) = TransformationSafeSelect.BuildSafeSelect(graph, "ppiq_staging");

        Assert.Null(sql);
        Assert.NotNull(err);
    }

    [Fact]
    public void A_legal_graph_compiles_to_a_bounded_statement()
    {
        var (sql, err, _) = TransformationSafeSelect.BuildSafeSelect(Graph("readings"), "ppiq_staging");

        Assert.Null(err);
        Assert.NotNull(sql);
        Assert.Contains("LIMIT", sql!);
    }

    [Fact]
    public void Identifier_validation_is_available_to_both_callers()
    {
        Assert.True(TransformationSafeSelect.Ident("legal_name_1"));
        Assert.False(TransformationSafeSelect.Ident("illegal-name"));
        Assert.False(TransformationSafeSelect.Ident(null));
    }
}