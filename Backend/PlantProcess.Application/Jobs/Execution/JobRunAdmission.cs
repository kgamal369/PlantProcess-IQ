using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// T-106. EVERYTHING THAT MUST BE TRUE BEFORE A RUN RECORD IS OPENED.
///
/// Extracted so it can be falsified without a database. The defect this closes
/// was invisible precisely because the only way to reach it was to start a run
/// first: the capability question was answered after JobRunHistory already
/// carried a row, which made an unsupported family look like a failed run
/// rather than a request that should never have been accepted.
/// </summary>
public static class JobRunAdmission
{
    public static ApplicationResult Admit(
        JobDefinition job,
        IJobExecutionCapabilityAuthority capability)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(capability);

        if (!job.IsEnabled)
        {
            return ApplicationResult.Failure(
                ApplicationError.BusinessRule("Paused jobs cannot be executed. Resume the job first."));
        }

        JobExecutionCapability answer = capability.Describe(job.JobType);

        if (!answer.IsExecutableByRuntime)
        {
            return ApplicationResult.Failure(
                JobExecutionErrors.NoExecutorForJobFamily(job.JobType, answer.Statement));
        }

        return ApplicationResult.Success();
    }
}