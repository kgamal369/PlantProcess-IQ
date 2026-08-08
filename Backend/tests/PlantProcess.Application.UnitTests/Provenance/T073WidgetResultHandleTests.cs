using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Provenance;

/// <summary>
/// T-073. The handle a numeric assistant claim cites.
/// </summary>
public class T073WidgetResultHandleTests
{
    [Fact]
    public void The_kind_exists_and_is_appended_last()
    {
        var members = Enum.GetNames<ProvenanceKind>();

        Assert.Contains("WidgetResult", members);
        /* Appended, never inserted: inserting renumbers every member above it. */
        Assert.Equal("WidgetResult", members[^1]);
    }

    [Fact]
    public void The_factory_produces_a_widget_result_handle()
    {
        var id = Guid.NewGuid().ToString();
        var handle = ProvenanceHandle.WidgetResult(id, "detail");

        Assert.Equal(ProvenanceKind.WidgetResult, handle.Kind);
        Assert.Equal(id, handle.Id);
        Assert.Equal("detail", handle.Detail);
    }

    [Fact]
    public void The_token_names_the_kind_so_a_citation_is_self_describing()
    {
        var id = Guid.NewGuid().ToString();

        Assert.Equal("WidgetResult:" + id, ProvenanceHandle.WidgetResult(id).Token);
    }

    [Fact]
    public void A_widget_result_is_not_a_dataset()
    {
        var id = Guid.NewGuid().ToString();

        /* The whole reason this kind exists. A Dataset handle proves a table
           exists; it cannot prove a widget returned a number. The two must never
           be interchangeable. */
        Assert.NotEqual(ProvenanceHandle.Dataset(id).Kind, ProvenanceHandle.WidgetResult(id).Kind);
        Assert.NotEqual(ProvenanceHandle.Dataset(id).Token, ProvenanceHandle.WidgetResult(id).Token);
    }
}