namespace PlantProcess.Application.Demo.Readiness;

/// <summary>
/// PPIQ-103: one-click demo readiness. Aggregates the demo gate into a single green/blocked
/// verdict that NAMES the exact blocker - all 8 sources linked, staging populated, mappings
/// published, the 4 standing jobs runnable, demo pages present.
/// </summary>
public sealed record DemoReadinessInputs(
    int SourcesLinked,
    int SourcesExpected,
    bool StagingPopulated,
    bool MappingsPublished,
    int JobsRunnable,
    int JobsExpected,
    bool DemoPagesPresent);

public sealed record DemoReadinessReport(
    bool IsReady,
    string Status,
    IReadOnlyList<string> Blockers,
    DemoReadinessInputs Inputs);

public static class DemoReadinessEvaluator
{
    public static DemoReadinessReport Evaluate(DemoReadinessInputs i)
    {
        var blockers = new List<string>();

        if (i.SourcesLinked < i.SourcesExpected)
            blockers.Add($"{i.SourcesExpected - i.SourcesLinked} of {i.SourcesExpected} demo sources not linked.");
        if (!i.StagingPopulated)
            blockers.Add("Staging tables are empty - load the demo dataset.");
        if (!i.MappingsPublished)
            blockers.Add("Canonical mappings are not published.");
        if (i.JobsRunnable < i.JobsExpected)
            blockers.Add($"{i.JobsExpected - i.JobsRunnable} of {i.JobsExpected} standing jobs are not runnable.");
        if (!i.DemoPagesPresent)
            blockers.Add("One or more demo pages are missing.");

        var ready = blockers.Count == 0;
        return new DemoReadinessReport(ready, ready ? "green" : "blocked", blockers, i);
    }
}