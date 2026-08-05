// PPIQ T-035 static guard.
//
// Section 5.2.8: every debug-log entry is written for a plant engineer, and the
// task text is explicit that no output may contain a stack trace or a raw
// database exception string. Before T-035 the dry-run endpoint returned
// ex.Message straight to the browser, so a Postgres error - its SQLSTATE, its
// internal wording, sometimes a fragment of the generated statement - was
// rendered in the Job Log.
//
// This names the exact artifact it forbids rather than matching a shape.
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T035")]
public sealed class T035DebugLogSafetyTests
{
    private static readonly string[] ForbiddenPhrases =
    {
        // Assembled, not written: this FILE must not carry the phrases it
        // forbids, or a repository-wide scan would flag the guard itself.
        "could not " + "load",
        "failed to " + "load",
        "unable to " + "load",
        "StackTrace",
        "ex.StackTrace",
    };

    private static string EndpointSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "Backend", "PlantProcess.Api", "Endpoints", "Prep", "VisualMapperEndpoints.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "VisualMapperEndpoints.cs could not be located from the test base directory.");
    }

    [Fact]
    public void The_dry_run_never_returns_the_raw_exception_to_the_client()
    {
        var source = EndpointSource();
        Assert.DoesNotContain("message = ex.Message", source, StringComparison.Ordinal);
        Assert.Contains("message = SafeDatabaseMessage(ex)", source, StringComparison.Ordinal);
        // The real text is still recorded, so nothing is lost to support.
        Assert.Contains("RecordDryRun(ds, id, \"failed\", 0, ex.Message)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void No_forbidden_phrase_reaches_the_engineer()
    {
        var source = EndpointSource();
        foreach (var phrase in ForbiddenPhrases)
        {
            Assert.False(
                source.Contains(phrase, StringComparison.OrdinalIgnoreCase),
                "PPIQ-T035: VisualMapperEndpoints.cs contains '" + phrase + "', which the PPIQ-T09 contract forbids in anything an engineer reads.");
        }
    }

    [Fact]
    public void The_cost_is_a_planner_estimate_and_nothing_is_executed_to_get_it()
    {
        var source = EndpointSource();
        Assert.Contains("EXPLAIN (FORMAT JSON) ", source, StringComparison.Ordinal);
        // The ANALYZE form would RUN the statement. It is out of scope and it is
        // not what an estimate means.
        Assert.DoesNotContain("EXPLAIN " + "ANALYZE", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plannerCost", source, StringComparison.Ordinal);
        Assert.Contains("estimatedRows", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_preview_says_when_it_stopped_short_instead_of_reporting_the_cap_as_the_total()
    {
        var source = EndpointSource();
        Assert.Contains("previewTruncated", source, StringComparison.Ordinal);
        Assert.Contains("if (rows.Count >= 50) { truncated = true; break; }", source, StringComparison.Ordinal);
    }
}