using PlantProcess.Application.Definitions;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>
/// T-064. WHETHER A JOB CLASS MUST DECLARE A TARGET, AND WHICH KINDS IT MAY RUN.
///
/// Null PermittedKinds means unconstrained. An empty list would mean "may run
/// nothing", which is a different statement, so the two are never conflated.
/// </summary>
public sealed record JobTargetClassRule
{
    public required bool RequiresTarget { get; init; }

    /// <summary>Null when the class constrains nothing. Never empty.</summary>
    public IReadOnlyList<DefinitionKind>? PermittedKinds { get; init; }

    public static readonly JobTargetClassRule Unconstrained =
        new() { RequiresTarget = false, PermittedKinds = null };
}

public interface IJobTargetClassPolicy
{
    JobTargetClassRule RuleFor(JobDefinitionType jobClass);
}

/// <summary>
/// T-064. THE DECLARED POLICY, AND WHAT IT DELIBERATELY DOES NOT CLAIM.
///
/// JB01 and JB02 exist because some job classes require a target and constrain
/// which kind of definition they may run. WHICH classes those are is a product
/// ruling, and no such declaration exists anywhere in this repository today:
/// JobDefinitionType carries nine members and not one of them has ever held a
/// governed target.
///
/// So this table is total over those nine and every one of them is currently
/// Unconstrained. That is a measured statement about today, not a claim that no
/// class will ever require a target. Declaring one is a one-line change here,
/// and the mechanism that turns the declaration into a JB01 or a JB02 refusal is
/// implemented and falsified by tests using an explicit policy.
///
/// Inventing the declarations instead would have produced a table that reads
/// like specification and is guesswork.
/// </summary>
public sealed class DeclaredJobTargetClassPolicy : IJobTargetClassPolicy
{
    private static readonly Dictionary<JobDefinitionType, JobTargetClassRule> Rules = new()
    {
        [JobDefinitionType.DbLinkImport] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.CanonicalRefresh] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.MlParamsVsDefects] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.MlParamsVsDowntime] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.MlParamsVsKpis] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.MlWeeklyFull] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.DataQualityScan] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.RiskScoring] = JobTargetClassRule.Unconstrained,
        [JobDefinitionType.Custom] = JobTargetClassRule.Unconstrained
    };

    public JobTargetClassRule RuleFor(JobDefinitionType jobClass)
    {
        if (!Rules.TryGetValue(jobClass, out JobTargetClassRule? rule))
        {
            // A member added to the enum without a rule here is a gap, and a gap
            // that answers "unconstrained" is a gap nobody finds.
            throw new InvalidOperationException(
                "Job class " + jobClass + " has no declared target rule. Every member of "
                + "JobDefinitionType must be declared in DeclaredJobTargetClassPolicy.");
        }

        return rule;
    }

    /// <summary>Exposed so a test can prove the table is total over the enum.</summary>
    public static IReadOnlyCollection<JobDefinitionType> DeclaredClasses => Rules.Keys;
}
