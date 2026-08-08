using PlantProcess.Application.Assistant;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// T-073. The pure evidence logic: deterministic, never inventing a number,
/// carrying no vocabulary, and binding every semantic the sentence states.
/// </summary>
public class T073WidgetResultEvidenceTests
{
    private static WidgetEvidenceIdentity Identity() => new(
        "PAGE_ALPHA", "WIDGET_ALPHA", Guid.Empty, "chart", "bar", "DIM_ALPHA", "MEASURE_ALPHA", null);

    private static IReadOnlyList<string> Columns() => new List<string>
    {
        "DIM_ALPHA", "dimensionLabel", "value", "observationCount", "secondaryCount"
    };

    private static IReadOnlyList<string> ColumnsWithoutObservationCount() => new List<string>
    {
        "DIM_ALPHA", "dimensionLabel", "value"
    };

    private static List<IDictionary<string, object?>> Rows() => new()
    {
        new Dictionary<string, object?>
        {
            ["DIM_ALPHA"] = "KEY_ONE",
            ["dimensionLabel"] = "LABEL_ONE",
            ["value"] = 3.4d,
            ["observationCount"] = 1284,
            ["secondaryCount"] = 0
        },
        new Dictionary<string, object?>
        {
            ["DIM_ALPHA"] = "KEY_TWO",
            ["dimensionLabel"] = "LABEL_TWO",
            ["value"] = 1.9d,
            ["observationCount"] = 640,
            ["secondaryCount"] = 0
        }
    };

    [Fact]
    public void Normalisation_keeps_every_value_and_totals_the_observation_count_column()
    {
        var result = WidgetResultEvidence.Normalise(Columns(), Rows());

        Assert.Equal(2, result.Rows.Count);
        Assert.True(result.HasObservationCount);
        Assert.Equal(1924, result.ObservationCountTotal);
        Assert.Contains("3.4", result.Rows[0]);
        Assert.Contains("1.9", result.Rows[1]);
    }

    [Fact]
    public void Without_that_column_nothing_is_counted_and_nothing_is_inferred()
    {
        var rows = Rows();
        foreach (var row in rows) row.Remove("observationCount");

        var result = WidgetResultEvidence.Normalise(ColumnsWithoutObservationCount(), rows);

        Assert.False(result.HasObservationCount);
        Assert.Equal(0, result.ObservationCountTotal);

        /* Two rows must never become a count of two of anything. */
        var sentence = WidgetResultEvidence.Sentence(Identity(), result);
        Assert.DoesNotContain("observationCount", sentence);
        Assert.Contains("2 result rows", sentence);
    }

    [Fact]
    public void The_same_result_fingerprints_the_same_every_time()
    {
        var first = WidgetResultEvidence.Normalise(Columns(), Rows());
        var second = WidgetResultEvidence.Normalise(Columns(), Rows());
        var queryFingerprint = WidgetResultEvidence.QueryFingerprint(Identity(), "{}");

        Assert.Equal(
            WidgetResultEvidence.ResultFingerprint(queryFingerprint, first),
            WidgetResultEvidence.ResultFingerprint(queryFingerprint, second));
    }

    [Fact]
    public void A_changed_value_changes_the_result_fingerprint()
    {
        var queryFingerprint = WidgetResultEvidence.QueryFingerprint(Identity(), "{}");
        var original = WidgetResultEvidence.Normalise(Columns(), Rows());

        var changedRows = Rows();
        changedRows[0]["value"] = 3.5d;
        var changed = WidgetResultEvidence.Normalise(Columns(), changedRows);

        Assert.NotEqual(
            WidgetResultEvidence.ResultFingerprint(queryFingerprint, original),
            WidgetResultEvidence.ResultFingerprint(queryFingerprint, changed));
    }

    [Fact]
    public void Every_semantic_the_sentence_states_is_bound_to_the_evidence_identity()
    {
        var identity = Identity();
        var baseline = WidgetResultEvidence.QueryFingerprint(identity, "{}");

        /* The invariant: an old citation must never resolve to semantically
           different evidence after a widget definition changes. Each of these is
           a semantic the sentence states, so each must move the fingerprint. */
        Assert.NotEqual(baseline, WidgetResultEvidence.QueryFingerprint(identity with { WidgetCode = "WIDGET_BETA" }, "{}"));
        Assert.NotEqual(baseline, WidgetResultEvidence.QueryFingerprint(identity with { PageCode = "PAGE_BETA" }, "{}"));
        Assert.NotEqual(baseline, WidgetResultEvidence.QueryFingerprint(identity with { MeasureCode = "MEASURE_BETA" }, "{}"));
        Assert.NotEqual(baseline, WidgetResultEvidence.QueryFingerprint(identity with { ChartType = "line" }, "{}"));
        Assert.NotEqual(baseline, WidgetResultEvidence.QueryFingerprint(identity with { DimensionCode = "DIM_BETA" }, "{}"));
        Assert.NotEqual(baseline, WidgetResultEvidence.QueryFingerprint(identity, "{\"siteId\":\"SITE_ALPHA\"}"));
    }

    [Fact]
    public void Every_number_in_the_sentence_comes_from_the_result()
    {
        var result = WidgetResultEvidence.Normalise(Columns(), Rows());
        var sentence = WidgetResultEvidence.Sentence(Identity(), result);

        Assert.Contains("LABEL_ONE 3.4", sentence);
        Assert.Contains("LABEL_TWO 1.9", sentence);
        Assert.Contains("2 result rows", sentence);
        Assert.Contains("observationCount column totals 1924", sentence);
    }

    [Fact]
    public void The_sentence_states_the_semantics_it_was_fingerprinted_against()
    {
        var sentence = WidgetResultEvidence.Sentence(Identity(), WidgetResultEvidence.Normalise(Columns(), Rows()));

        Assert.Contains("PAGE_ALPHA", sentence);
        Assert.Contains("WIDGET_ALPHA", sentence);
        Assert.Contains("MEASURE_ALPHA", sentence);
        Assert.Contains("DIM_ALPHA", sentence);
        Assert.Contains("bar", sentence);
    }

    [Fact]
    public void An_empty_result_says_so_rather_than_implying_one()
    {
        var empty = WidgetResultEvidence.Normalise(Columns(), new List<IDictionary<string, object?>>());
        var sentence = WidgetResultEvidence.Sentence(Identity(), empty);

        Assert.Contains("returned no rows", sentence);
        Assert.DoesNotContain("observationCount column totals", sentence);
    }

    [Fact]
    public void A_long_result_says_how_many_of_its_rows_are_listed()
    {
        var rows = new List<IDictionary<string, object?>>();
        for (var i = 0; i < 20; i++)
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["DIM_ALPHA"] = "KEY_" + i,
                ["dimensionLabel"] = "LABEL_" + i,
                ["value"] = i,
                ["observationCount"] = 1,
                ["secondaryCount"] = 0
            });
        }

        var sentence = WidgetResultEvidence.Sentence(Identity(), WidgetResultEvidence.Normalise(Columns(), rows));

        Assert.Contains("20 result rows", sentence);
        Assert.Contains("the first 6 are listed here", sentence);
    }

    [Fact]
    public void The_sentence_carries_no_timestamp_so_a_repeat_reindex_is_silent()
    {
        var result = WidgetResultEvidence.Normalise(Columns(), Rows());

        Assert.Equal(
            WidgetResultEvidence.Sentence(Identity(), result),
            WidgetResultEvidence.Sentence(Identity(), result));

        Assert.DoesNotContain("UTC", WidgetResultEvidence.Sentence(Identity(), result));
    }
}