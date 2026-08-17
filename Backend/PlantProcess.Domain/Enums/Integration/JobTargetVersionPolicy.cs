namespace PlantProcess.Domain.Enums.Integration;

/// <summary>
/// T-064. WHICH VERSION OF ITS TARGET A JOB RUNS.
///
/// There are exactly two answers and no default. A job that has not stated one
/// has no target at all, which is a different fact from a job that follows the
/// published version, and the two must never collapse into each other.
///
/// T-089 and T-090 establish the canonical definition-store authority; T-106
/// owns the physical T-064 convergence onto definition_store(id). This enum is
/// the semantic half and does not change when that happens.
/// </summary>
public enum JobTargetVersionPolicy
{
    /// <summary>Run whichever version is published at the moment of the run.</summary>
    CurrentPublished = 1,

    /// <summary>Run one fixed version number, whatever is published later.</summary>
    Pinned = 2
}
