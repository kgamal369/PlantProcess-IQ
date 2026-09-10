using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Dependencies;

/// <summary>
/// T-106. What one edge resolved to for one downstream attempt, before any
/// evidence is persisted. Carrying the upstream run identity here is what makes
/// the persisted row reference a real run rather than a manufactured one.
/// </summary>
public sealed record JobDependencyOutcome(
    Guid DependsOnJobDefinitionId,
    Guid? DependsOnRunId,
    JobDependencyResolution Resolution,
    bool BlocksDownstream,
    int? ExpectedVersion,
    int? ActualVersion,
    string Reason);

/// <summary>
/// T-106. THE EDGE SEMANTICS OF CHAPTER 5.3.6, AS A PURE FUNCTION.
///
/// Pure so that every state can be falsified without a database, and so the one
/// state this runtime must NOT produce is provably unreachable rather than
/// merely absent from the code path anybody happened to read.
/// </summary>
public static class JobDependencyEvaluator
{
    /// <summary>
    /// upstreamRunId, upstreamStatus and upstreamVersion describe the most
    /// recent upstream run, or are absent when the upstream has never run.
    /// </summary>
    public static JobDependencyOutcome Evaluate(
        Guid dependsOnJobDefinitionId,
        bool isRequired,
        int? pinnedVersion,
        Guid? upstreamRunId,
        JobRunStatus? upstreamStatus,
        int? upstreamVersion)
    {
        // Upstream never ran. The only state in the design with no upstream run
        // identity at all, and the only one where the persisted evidence carries
        // a NULL rather than a fabricated id.
        if (upstreamRunId is null || upstreamStatus is null)
        {
            return isRequired
                ? new JobDependencyOutcome(
                    dependsOnJobDefinitionId, null, JobDependencyResolution.Blocked, true,
                    pinnedVersion, null,
                    "Required upstream job " + dependsOnJobDefinitionId + " has never run.")
                : new JobDependencyOutcome(
                    dependsOnJobDefinitionId, null, JobDependencyResolution.SkippedOptional, false,
                    pinnedVersion, null,
                    "Optional upstream job " + dependsOnJobDefinitionId + " has never run; the job ran without it.");
        }

        if (upstreamStatus.Value != JobRunStatus.Ok)
        {
            return isRequired
                ? new JobDependencyOutcome(
                    dependsOnJobDefinitionId, upstreamRunId, JobDependencyResolution.FailedUpstream, true,
                    pinnedVersion, upstreamVersion,
                    "Required upstream job " + dependsOnJobDefinitionId + " did not complete successfully ("
                        + upstreamStatus.Value + ").")
                : new JobDependencyOutcome(
                    dependsOnJobDefinitionId, upstreamRunId, JobDependencyResolution.SkippedOptional, false,
                    pinnedVersion, upstreamVersion,
                    "Optional upstream job " + dependsOnJobDefinitionId + " did not complete successfully ("
                        + upstreamStatus.Value + "); the job ran without it.");
        }

        // A pinned edge that resolved to another version blocks whether it is
        // required or not: running against the wrong version is not the same
        // request, so there is nothing to skip.
        if (pinnedVersion.HasValue && upstreamVersion != pinnedVersion.Value)
        {
            return new JobDependencyOutcome(
                dependsOnJobDefinitionId, upstreamRunId, JobDependencyResolution.Blocked, true,
                pinnedVersion, upstreamVersion,
                "Dependency pins version " + pinnedVersion.Value + " of upstream job "
                    + dependsOnJobDefinitionId + " and the upstream run produced version "
                    + (upstreamVersion.HasValue ? upstreamVersion.Value.ToString() : "none") + ".");
        }

        // stale_accepted would be produced here, and is not. The edge declares a
        // staleness tolerance but the design declares no authority that permits
        // accepting a stale upstream, so this runtime never claims that state.
        return new JobDependencyOutcome(
            dependsOnJobDefinitionId, upstreamRunId, JobDependencyResolution.Satisfied, false,
            pinnedVersion, upstreamVersion,
            "Upstream job " + dependsOnJobDefinitionId + " completed successfully.");
    }
}