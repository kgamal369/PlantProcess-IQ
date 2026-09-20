namespace PlantProcess.Domain.Enums.Integration;

/// <summary>
/// THE CHILD GRAIN, AND WHY IT IS NOT A SECOND RUN STATE MACHINE.
///
/// JobRunStatus answers what happened to the run. This answers what happened to one
/// authored block inside it. They are different grains over the same execution, so a
/// run can be Failed while most of its blocks Succeeded, and that is the whole point:
/// without the child grain the only available answer to "which block broke" is a
/// sentence in a message field.
///
/// Blocked here means a dependency did not succeed, so this block never executed. It
/// is never used to record a block that ran and failed - that is Failed - because a
/// reader cannot recover the difference afterwards.
/// </summary>
public enum JobRunBlockStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Blocked = 4,
    Cancelled = 5
}