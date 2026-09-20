using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// EVERYTHING THE EXECUTOR MUST NOT REDISCOVER.
///
/// The dispatch point used to carry a JobDefinitionType and nothing else, which meant
/// an executor could only find out what it was executing by asking the store again.
/// Asking again is the defect: between admission and execution the published version
/// can move, and a run that re-resolved would execute something other than the version
/// its own evidence claims.
///
/// So the plan admitted before the run existed travels here, frozen, and the run owns
/// it for its whole life.
/// </summary>
public sealed record JobExecutionContext(
    Guid JobDefinitionId,
    string JobCode,
    JobDefinitionType JobType,
    Guid JobRunHistoryId,
    JobExecutionPlan Plan,
    string? CorrelationId)
{
    public Guid TargetDefinitionId => Plan?.Target?.DefinitionId ?? Guid.Empty;

    public int ResolvedVersion => Plan?.Target?.ResolvedVersion ?? 0;

    /// <summary>
    /// A context that cannot name the exact version it executes, or the genuine run it
    /// executes for, is not executable. Checked here so every executor inherits the same
    /// refusal rather than each one deciding for itself what a usable context is.
    /// </summary>
    public bool CarriesExactVersion =>
        Plan is not null
        && JobRunHistoryId != Guid.Empty
        && TargetDefinitionId != Guid.Empty
        && ResolvedVersion > 0;
}
