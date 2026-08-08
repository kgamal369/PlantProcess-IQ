using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Metadata;
using Xunit;

namespace PlantProcess.Application.UnitTests.Dashboarding;

/// PPIQ T-041 acceptance A. THE STRUCTURAL WIDGET GRAMMAR IS CLOSED AT SEVEN.
///
/// The endpoint is the only authority for what a widget can BE, so the picker
/// cannot offer a kind the product does not implement and cannot miss one it
/// does. These proofs fail the moment an eighth appears or a seventh is dropped.
public sealed class WidgetKindGrammarTests
{
    private static IReadOnlyList<DashboardWidgetKindMetadataDto> Kinds()
    {
        var service = new DashboardMetadataService(null!);
        var result = service.GetMetadataAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess);
        return result.Value!.WidgetKinds;
    }

    [Fact]
    public void Metadata_publishes_exactly_the_seven_structural_kinds()
    {
        var codes = Kinds().Select(k => k.Code).ToArray();

        Assert.Equal(7, codes.Length);
        Assert.Equal(
            new[] { "chart", "table", "kpi", "calculated-label", "filter", "container", "text" },
            codes);
    }

    [Fact]
    public void The_three_codes_that_already_shipped_are_reused_unchanged()
    {
        Assert.Equal(DashboardMetadataCodes.WidgetTypes.Chart, DashboardMetadataCodes.WidgetKinds.Chart);
        Assert.Equal(DashboardMetadataCodes.WidgetTypes.Table, DashboardMetadataCodes.WidgetKinds.Table);
        Assert.Equal(DashboardMetadataCodes.WidgetTypes.Kpi, DashboardMetadataCodes.WidgetKinds.Kpi);
    }

    [Fact]
    public void Every_kind_carries_a_label_and_a_sentence_and_no_code_repeats()
    {
        var kinds = Kinds();

        Assert.All(kinds, k => Assert.False(string.IsNullOrWhiteSpace(k.Label)));
        Assert.All(kinds, k => Assert.False(string.IsNullOrWhiteSpace(k.Description)));
        Assert.Equal(kinds.Count, kinds.Select(k => k.Code).Distinct().Count());
    }

    [Fact]
    public void A_chart_type_is_not_a_structural_kind()
    {
        var codes = Kinds().Select(k => k.Code).ToHashSet();

        // Bar and Line are the two the retiring union promoted to kinds. They are
        // chart types, and a chart type reaches the page through the Chart kind.
        Assert.DoesNotContain(DashboardMetadataCodes.ChartTypes.Bar, codes);
        Assert.DoesNotContain(DashboardMetadataCodes.ChartTypes.Line, codes);
        Assert.Contains(DashboardMetadataCodes.WidgetKinds.Chart, codes);
    }

    [Fact]
    public void Only_the_chart_kind_declares_that_it_uses_a_chart_type()
    {
        var usesChartType = Kinds().Where(k => k.UsesChartType).Select(k => k.Code).ToArray();

        Assert.Equal(new[] { DashboardMetadataCodes.WidgetKinds.Chart }, usesChartType);
    }

    [Fact]
    public void Container_and_text_carry_no_query_because_they_state_no_measured_value()
    {
        var withoutQuery = Kinds().Where(k => !k.UsesQuery).Select(k => k.Code).ToArray();

        Assert.Equal(
            new[] { DashboardMetadataCodes.WidgetKinds.Container, DashboardMetadataCodes.WidgetKinds.Text },
            withoutQuery);
    }
}