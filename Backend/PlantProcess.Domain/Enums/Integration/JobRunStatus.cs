namespace PlantProcess.Domain.Enums.Integration;

/// <summary>
/// Last known runtime status of a configured PlantProcess IQ job.
/// </summary>
public enum JobRunStatus
{
    NeverRun = 0,
    Running = 1,
    Ok = 2,
    Failed = 3,
    Timeout = 4
}