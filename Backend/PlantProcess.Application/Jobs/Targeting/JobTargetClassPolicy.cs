using PlantProcess.Application.Definitions;
using PlantProcess.Application.Jobs.Execution;
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
/// T-106. A THIN ADAPTER, NOT A SECOND DICTIONARY.
///
/// The class policy this replaces carried its own table of nine families, every
/// one of them Unconstrained, with a comment admitting that no declaration
/// existed anywhere in the repository. That made JB01 and JB02 mechanisms with
/// nothing to fire on, and it made the target contract a second record of a fact
/// the capability authority also holds.
///
/// The rule is now DERIVED. One family descriptor answers both questions, so the
/// two can never disagree, and T-064's resolver, its JB vocabulary and its codec
/// are untouched - they simply start receiving real answers.
/// </summary>
public sealed class CapabilityJobTargetClassPolicy : IJobTargetClassPolicy
{
    private readonly IJobExecutionCapabilityAuthority _capabilities;

    public CapabilityJobTargetClassPolicy(IJobExecutionCapabilityAuthority capabilities)
    {
        _capabilities = capabilities;
    }

    public JobTargetClassRule RuleFor(JobDefinitionType jobClass)
    {
        JobExecutionCapability capability = _capabilities.Describe(jobClass);

        if (capability.TargetRequirement != JobTargetRequirement.Required
            || capability.CanonicalTargetKind is null)
        {
            return JobTargetClassRule.Unconstrained;
        }

        return new JobTargetClassRule
        {
            RequiresTarget = true,
            PermittedKinds = new[] { capability.CanonicalTargetKind.Value }
        };
    }
}