using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Queries;
using Xunit;

namespace PlantProcess.Application.UnitTests.Dashboarding;

/// <summary>
/// PR-050-01. These test the population descriptor, which is the half of the
/// drill-down contract that can be proven without a database. The evidence
/// write path is proven against PostgreSQL in the integration estate.
///
/// Every test here is written to FALSIFY a specific untruth the descriptor
/// could tell, not to confirm that it runs.
/// </summary>
public sealed class PR05001PopulationDescriptorTests
{
    private static DashboardWidgetResolvedDto Resolved(string measure = "m1", string? parameter = null) =>
        new("chart", "bar", "d1", measure, parameter, 100, 1000, "desc", null, null);

    private static IReadOnlyList<DashboardWidgetColumnDto> Columns() => new List<DashboardWidgetColumnDto>
    {
        new("d1", "Dimension", "string"),
        new("dimensionLabel", "Dimension Label", "string"),
        new("value", "Value", "number"),
        new("observationCount", "Observation Count", "number")
    };

    private static IDictionary<string, object?> Row(string key, string label, decimal value, object? observations) =>
        new Dictionary<string, object?>
        {
            ["d1"] = key,
            ["dimensionLabel"] = label,
            ["value"] = value,
            ["observationCount"] = observations
        };

    [Fact]
    public void No_filters_canonicalises_to_the_same_empty_context_the_reindex_path_writes()
    {
        // If this drifted, a live unfiltered execution could never reuse the
        // evidence identity an Assistant reindex already wrote for it.
        Assert.Equal("{}", DashboardPopulationDescriptor.CanonicaliseFilterContext(null));
        Assert.Equal("{}", DashboardPopulationDescriptor.CanonicaliseFilterContext(
            new DashboardWidgetFiltersDto(null, null, null, null, null, null, null, null, null, null, null, null)));
    }

    [Fact]
    public void Blank_and_whitespace_filter_values_are_not_filters()
    {
        var blank = new DashboardWidgetFiltersDto(null, null, null, "   ", null, null, null, null, null, null, null, null);
        Assert.Equal("{}", DashboardPopulationDescriptor.CanonicaliseFilterContext(blank));
    }

    [Fact]
    public void The_same_filters_always_canonicalise_identically()
    {
        var site = Guid.NewGuid();
        var from = new DateTime(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc);

        var a = new DashboardWidgetFiltersDto(site, null, null, "MAT-1", null, null, null, null, "B", null, from, null);
        var b = new DashboardWidgetFiltersDto(site, null, null, "MAT-1", null, null, null, null, "B", null, from, null);

        Assert.Equal(
            DashboardPopulationDescriptor.CanonicaliseFilterContext(a),
            DashboardPopulationDescriptor.CanonicaliseFilterContext(b));
    }

    [Fact]
    public void A_changed_filter_changes_the_context_and_therefore_the_evidence_identity()
    {
        var a = new DashboardWidgetFiltersDto(null, null, null, "MAT-1", null, null, null, null, null, null, null, null);
        var b = new DashboardWidgetFiltersDto(null, null, null, "MAT-2", null, null, null, null, null, null, null, null);

        Assert.NotEqual(
            DashboardPopulationDescriptor.CanonicaliseFilterContext(a),
            DashboardPopulationDescriptor.CanonicaliseFilterContext(b));
    }

    [Fact]
    public void Every_returned_row_receives_a_descriptor()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 10), Row("b", "B", 2m, 20) };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{}");

        Assert.Equal(rows.Count, described.Count);
        Assert.Equal(new[] { 0, 1 }, described.Select(d => d.RowIndex).ToArray());
    }

    [Fact]
    public void Population_count_is_the_results_own_count_and_never_the_number_of_rows()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 428), Row("b", "B", 2m, 9) };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{}");

        Assert.Equal(428, described[0].PopulationCount);
        Assert.Equal(9, described[1].PopulationCount);
        Assert.DoesNotContain(described, d => d.PopulationCount == rows.Count);
    }

    [Fact]
    public void A_result_without_a_count_column_reports_unavailable_rather_than_inventing_one()
    {
        var columns = new List<DashboardWidgetColumnDto>
        {
            new("d1", "Dimension", "string"),
            new("value", "Value", "number")
        };

        var rows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["d1"] = "a", ["value"] = 1m },
            new Dictionary<string, object?> { ["d1"] = "b", ["value"] = 2m }
        };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), columns, rows, "{}");

        Assert.All(described, d => Assert.Null(d.PopulationCount));
        Assert.All(described, d => Assert.NotNull(d.RowFingerprint));
    }

    [Fact]
    public void A_null_count_value_is_unavailable_not_zero()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, null) };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{}");

        Assert.Null(described[0].PopulationCount);
    }

    [Fact]
    public void Different_populations_never_share_one_identity()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 10), Row("b", "B", 2m, 20) };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{}");

        Assert.NotEqual(described[0].RowFingerprint, described[1].RowFingerprint);
    }

    [Fact]
    public void Reordering_the_same_result_does_not_invent_new_populations()
    {
        var forward = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 10), Row("b", "B", 2m, 20) };
        var reversed = new List<IDictionary<string, object?>> { Row("b", "B", 2m, 20), Row("a", "A", 1m, 10) };

        var one = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), forward, "{}");
        var two = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), reversed, "{}");

        Assert.Equal(one[0].RowFingerprint, two[1].RowFingerprint);
        Assert.Equal(one[1].RowFingerprint, two[0].RowFingerprint);
    }

    [Fact]
    public void A_moved_number_is_not_a_moved_population()
    {
        // The value is the answer, not the question. If the fingerprint tracked
        // the value, every refresh would look like a different population.
        var before = new List<IDictionary<string, object?>> { Row("a", "A", 4.6m, 428) };
        var after = new List<IDictionary<string, object?>> { Row("a", "A", 5.1m, 428) };

        var one = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), before, "{}");
        var two = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), after, "{}");

        Assert.Equal(one[0].RowFingerprint, two[0].RowFingerprint);
    }

    [Fact]
    public void A_relabelled_row_is_not_a_different_population()
    {
        var one = DashboardPopulationDescriptor.Describe(
            Resolved(), Columns(), new List<IDictionary<string, object?>> { Row("a", "Line A", 1m, 10) }, "{}");

        var two = DashboardPopulationDescriptor.Describe(
            Resolved(), Columns(), new List<IDictionary<string, object?>> { Row("a", "Renamed", 1m, 10) }, "{}");

        Assert.Equal(one[0].RowFingerprint, two[0].RowFingerprint);
    }

    [Fact]
    public void A_different_filter_context_is_a_different_population_for_the_same_dimension_value()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 10) };

        var one = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{}");
        var two = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{\"shiftCode\":\"B\"}");

        Assert.NotEqual(one[0].RowFingerprint, two[0].RowFingerprint);
        Assert.NotEqual(one[0].FilterContextFingerprint, two[0].FilterContextFingerprint);
    }

    [Fact]
    public void A_different_measure_is_a_different_population_for_the_same_dimension_value()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 10) };

        var one = DashboardPopulationDescriptor.Describe(Resolved("m1"), Columns(), rows, "{}");
        var two = DashboardPopulationDescriptor.Describe(Resolved("m2"), Columns(), rows, "{}");

        Assert.NotEqual(one[0].RowFingerprint, two[0].RowFingerprint);
    }

    [Fact]
    public void An_execution_that_cannot_distinguish_its_rows_withholds_the_identity()
    {
        // No string column means no categorical coordinate. Two such rows are
        // two rows this execution genuinely cannot tell apart, so neither gets
        // an identity. A shared identity would send a drill-down to the wrong
        // population, which is worse than no drill-down.
        var columns = new List<DashboardWidgetColumnDto> { new("value", "Value", "number") };

        var rows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["value"] = 1m },
            new Dictionary<string, object?> { ["value"] = 2m }
        };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), columns, rows, "{}");

        Assert.Equal(2, described.Count);
        Assert.All(described, d => Assert.Null(d.RowFingerprint));
    }

    [Fact]
    public void A_single_row_result_with_no_bindings_is_still_a_distinct_population()
    {
        var columns = new List<DashboardWidgetColumnDto> { new("value", "Value", "number") };
        var rows = new List<IDictionary<string, object?>> { new Dictionary<string, object?> { ["value"] = 1m } };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), columns, rows, "{}");

        Assert.NotNull(described[0].RowFingerprint);
    }

    [Fact]
    public void Two_rows_that_would_collide_both_lose_the_identity_rather_than_share_it()
    {
        var rows = new List<IDictionary<string, object?>> { Row("a", "A", 1m, 10), Row("a", "A", 2m, 20) };

        var described = DashboardPopulationDescriptor.Describe(Resolved(), Columns(), rows, "{}");

        Assert.All(described, d => Assert.Null(d.RowFingerprint));
    }

    [Fact]
    public void The_presentation_label_is_never_a_binding()
    {
        var described = DashboardPopulationDescriptor.Describe(
            Resolved(), Columns(), new List<IDictionary<string, object?>> { Row("a", "Line A", 1m, 10) }, "{}");

        Assert.True(described[0].DimensionBindings.ContainsKey("d1"));
        Assert.False(described[0].DimensionBindings.ContainsKey("dimensionLabel"));
    }
}