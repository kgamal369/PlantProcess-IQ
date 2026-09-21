using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// Turns a stored job into a capacity request. The lane and the weight are the job's own
/// persisted facts; nothing is chosen here. A job that carries no lawful lane produces a
/// request the controller refuses by name, before any run record exists.
/// </summary>
public static class JobAdmissionRequests
{
    public static JobAdmissionRequest ForJob(JobDefinition job, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new JobAdmissionRequest(
            job.Id,
            job.JobType.ToString(),
            job.PoolCode ?? string.Empty,
            new JobResourceDemand(job.ComputeWeight),
            correlationId);
    }
}
