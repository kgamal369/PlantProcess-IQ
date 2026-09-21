using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Admission;

public static class JobLaneAssignment
{
    public static string? DefaultFor(JobDefinitionType family) => family switch
    {
        JobDefinitionType.DbLinkImport => JobLaneCodes.Import,
        JobDefinitionType.CanonicalRefresh => JobLaneCodes.Projection,
        JobDefinitionType.DataQualityScan or JobDefinitionType.RiskScoring => JobLaneCodes.Analysis,
        _ => null
    };

    public static bool IsPermitted(string family, string lane)
        => Enum.TryParse<JobDefinitionType>(family, false, out var kind)
            && Enum.IsDefined(kind) && DefaultFor(kind) is { } expected
            && string.Equals(expected, lane, StringComparison.Ordinal);

    public static void Initialize(JobDefinition job)
        => job.AssignExecutionPool(DefaultFor(job.JobType), 1);

    // A family change cannot silently reinterpret an explicitly configured pool or weight.
    public static ApplicationError? ValidateChange(JobDefinition job, JobDefinitionType family)
    {
        var expected = DefaultFor(family);
        if (expected is not null && job.PoolCode is not null
            && !string.Equals(expected, job.PoolCode, StringComparison.Ordinal))
            return ApplicationError.Validation("Changing this job family requires an explicit compatible execution pool.");
        return null;
    }

    public static void CompleteFamilyChange(JobDefinition job)
    {
        if (job.PoolCode is null && DefaultFor(job.JobType) is { } lane)
            job.AssignExecutionPool(lane, job.ComputeWeight);
    }
}
