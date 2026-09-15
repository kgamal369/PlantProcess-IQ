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
    string Reason,
    double? UpstreamAgeMinutes = null,
    int? ToleranceMinutes = null);

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
        => Evaluate(
            dependsOnJobDefinitionId,
            isRequired,
            pinnedVersion,
            upstreamRunId,
            upstreamStatus,
            upstreamVersion,
            upstreamRanInCurrentCycle: true,
            upstreamCompletedAtUtc: null,
            evaluatedAtUtc: null,
            stalenessToleranceMinutes: null,
            allowStaleReuse: false);

    /// <summary>
    /// T-106 B2.2. The same edge semantics, now with the measured inputs Chapter 5.3.6
    /// needs to decide freshness: when the upstream result was produced, what the edge
    /// tolerates, and whether the edge permits reusing a result from an earlier cycle.
    /// The decision itself belongs to DependencyFreshnessPolicy; this method maps its
    /// answer onto the edge vocabulary and adds no second rule.
    /// </summary>
    public static JobDependencyOutcome Evaluate(
        Guid dependsOnJobDefinitionId,
        bool isRequired,
        int? pinnedVersion,
        Guid? upstreamRunId,
        JobRunStatus? upstreamStatus,
        int? upstreamVersion,
        bool upstreamRanInCurrentCycle,
        DateTime? upstreamCompletedAtUtc,
        DateTime? evaluatedAtUtc,
        int? stalenessToleranceMinutes,
        bool allowStaleReuse)
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

        // The upstream succeeded. Whether that success is fresh enough to use is one
        // decision, taken once, by the accepted freshness authority. stale_accepted is
        // now reachable, and only through an edge that explicitly permits reuse inside
        // its declared tolerance.
        double? ageMinutes = null;
        if (upstreamCompletedAtUtc.HasValue)
        {
            var evaluatedAt = evaluatedAtUtc ?? DateTime.UtcNow;
            ageMinutes = (evaluatedAt - upstreamCompletedAtUtc.Value).TotalMinutes;
        }

        var freshness = DependencyFreshnessPolicy.Evaluate(
            new FreshnessInput(
                isRequired,
                upstreamRanInCurrentCycle,
                ageMinutes,
                stalenessToleranceMinutes,
                allowStaleReuse));

        var resolution = freshness.Resolution switch
        {
            FreshnessResolution.Satisfied => JobDependencyResolution.Satisfied,
            FreshnessResolution.StaleAccepted => JobDependencyResolution.StaleAccepted,
            FreshnessResolution.SkippedOptional => JobDependencyResolution.SkippedOptional,
            FreshnessResolution.Blocked => JobDependencyResolution.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness.Resolution, "Unknown freshness resolution.")
        };

        var reason = resolution == JobDependencyResolution.Satisfied && upstreamRanInCurrentCycle
            ? "Upstream job " + dependsOnJobDefinitionId + " completed successfully."
            : "Upstream job " + dependsOnJobDefinitionId + ": " + freshness.Reason;

        return new JobDependencyOutcome(
            dependsOnJobDefinitionId,
            upstreamRunId,
            resolution,
            resolution == JobDependencyResolution.Blocked,
            pinnedVersion,
            upstreamVersion,
            reason,
            freshness.UpstreamAgeMinutes,
            freshness.ToleranceMinutes);
    }
}