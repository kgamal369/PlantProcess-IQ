using PlantProcess.Application.Assistant;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// T-073. The pure evidence logic: it must be deterministic, it must never
/// invent a number, and it must carry no vocabulary.
/// </summary>
public class T073WidgetResultEvidenceTests
{
    private static WidgetEvidenceIdentity Identity() => new(
        "PAGE_ALPHA", "WIDGET_ALPHA", Guid.Empty, "chart", "bar", "DIM_ALPHA", "MEASURE_ALPHA", null);

    private static IReadOnlyList<IDictionary<string, object?>> Rows() => new List<IDictionary<string, object?>>
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

    private static IReadOnlyList<string> Columns() => new List<string>
    {
        "DIM_ALPHA", "dimensionLabel", "value", "observationCount", "secondaryCount"
    };

    [Fact]
    public void Normalisation_keeps_every_value_and_sums_the_population()
    {
        var result = WidgetResultEvidence.Normalise(Columns(), Rows());

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(1924, result.PopulationCount);
        Assert.Contains("3.4", result.Rows[0]);
        Assert.Contains("1.9", result.Rows[1]);
    }

    [Fact]
    public void The_same_result_fingerprints_the_same_every_time()
    {
        var first = WidgetResultEvidence.Normalise(Columns(), Rows());
        var second = WidgetResultEvidence.Normalise(Columns(), Rows());

        var queryFingerprint = WidgetResultEvidence.QueryFingerprint(Identity(), "{}");

        /* This is validation point 6. If these differed, every reindex would mint
           a new evidence identity and old citations would rot. */
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
    public void A_different_widget_fingerprints_differently()
    {
        var identity = Identity();
        var other = identity with { WidgetCode = "WIDGET_BETA" };

        Assert.NotEqual(
            WidgetResultEvidence.QueryFingerprint(identity, "{}"),
            WidgetResultEvidence.QueryFingerprint(other, "{}"));
    }

    [Fact]
    public void Every_number_in_the_sentence_comes_from_the_result()
    {
        var result = WidgetResultEvidence.Normalise(Columns(), Rows());
        var sentence = WidgetResultEvidence.Sentence(Identity(), result);

        Assert.Contains("LABEL_ONE 3.4", sentence);
        Assert.Contains("LABEL_TWO 1.9", sentence);
        Assert.Contains("2 result rows", sentence);
        Assert.Contains("1924 observations", sentence);
        Assert.Contains("PAGE_ALPHA", sentence);
        Assert.Contains("WIDGET_ALPHA", sentence);
    }

    [Fact]
    public void An_empty_result_says_so_rather_than_implying_one()
    {
        var empty = WidgetResultEvidence.Normalise(Columns(), new List<IDictionary<string, object?>>());
        var sentence = WidgetResultEvidence.Sentence(Identity(), empty);

        Assert.Contains("returned no rows", sentence);
        Assert.DoesNotContain("population", sentence);
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