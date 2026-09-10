namespace PlantProcess.Domain.Enums.Integration;

/// <summary>
/// Last known runtime status of a configured PlantProcess IQ job.
///
/// NUMERIC VALUES 0..4 ARE FROZEN. They are persisted as text today, but the
/// numbers are the contract every DTO and every stored integer would carry if
/// the conversion ever changed, so nothing here is renumbered.
///
/// Blocked is appended by the T-106 corrective. Chapter 5.3.6 requires a run
/// that was prevented from computing to appear as a REAL run rather than as an
/// absence: a child whose required upstream failed has an identity, a reason
/// and a row, and the dependency evidence references it. Without this member a
/// blocked attempt could only be recorded by inventing a run identity, which is
/// what the corrective exists to avoid.
///
/// Blocked is TERMINAL and is never reached from Running. There is deliberately
/// no Skipped member: skipped_optional is a dependency resolution state, and the
/// downstream job it describes actually runs.
/// </summary>
public enum JobRunStatus
{
    NeverRun = 0,
    Running = 1,
    Ok = 2,
    Failed = 3,
    Timeout = 4,

    /// <summary>The attempt exists and compute never started.</summary>
    Blocked = 5
}