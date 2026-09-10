using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// T-106. What this runtime can actually execute, and what it cannot.
/// </summary>
public sealed record JobExecutionCapability(
    JobDefinitionType JobType,
    bool IsExecutableByRuntime,
    string Statement);

/// <summary>
/// T-106. THE ONE AUTHORITATIVE ANSWER TO "CAN THIS JOB FAMILY BE EXECUTED BY
/// THIS RUNTIME?"
///
/// The 08-Sep implementation review found job families reaching Run Now with no
/// executor behind them, ending in a runtime message that admitted the feature
/// did not exist. That is a truth defect, not a missing feature: the product
/// presented something as runnable and discovered otherwise after it had
/// already opened a run record.
///
/// This closes the truth side. Definition validation and orchestration ask the
/// SAME authority, and the orchestrator's executor switch has no reachable
/// default: a family the authority admits and the switch cannot execute is a
/// contradiction that throws rather than degrades.
///
/// It deliberately does NOT commission executors. Commissioning the real
/// families remains T-118. What this owes is that unsupported work fails early,
/// honestly, and with a stable code.
/// </summary>
public interface IJobExecutionCapabilityAuthority
{
    /// <summary>
    /// Total over the enum. A family this method does not classify is a
    /// programming error, not a default, so a new member cannot slip through
    /// as accidentally runnable.
    /// </summary>
    JobExecutionCapability Describe(JobDefinitionType jobType);

    IReadOnlyList<JobDefinitionType> ExecutableFamilies { get; }
}

public sealed class JobExecutionCapabilityAuthority : IJobExecutionCapabilityAuthority
{
    private static readonly JobDefinitionType[] Executable =
    {
        JobDefinitionType.DbLinkImport,
        JobDefinitionType.CanonicalRefresh,
        JobDefinitionType.DataQualityScan,
        JobDefinitionType.RiskScoring
    };

    public IReadOnlyList<JobDefinitionType> ExecutableFamilies => Executable;

    public JobExecutionCapability Describe(JobDefinitionType jobType)
    {
        switch (jobType)
        {
            case JobDefinitionType.DbLinkImport:
                return new JobExecutionCapability(jobType, true, "Import queue execution is commissioned in this runtime.");

            case JobDefinitionType.CanonicalRefresh:
                return new JobExecutionCapability(jobType, true, "Canonical refresh execution is commissioned in this runtime.");

            case JobDefinitionType.DataQualityScan:
                return new JobExecutionCapability(jobType, true, "Data quality scanning is commissioned in this runtime.");

            case JobDefinitionType.RiskScoring:
                return new JobExecutionCapability(jobType, true, "Risk scoring is commissioned in this runtime.");

            case JobDefinitionType.MlParamsVsDefects:
            case JobDefinitionType.MlParamsVsDowntime:
            case JobDefinitionType.MlParamsVsKpis:
            case JobDefinitionType.MlWeeklyFull:
                return new JobExecutionCapability(
                    jobType,
                    false,
                    "The learning job family " + jobType + " has no executor in this runtime. It is run through the "
                        + "governed analysis path, not through job orchestration.");

            case JobDefinitionType.Custom:
                return new JobExecutionCapability(
                    jobType,
                    false,
                    "Custom jobs are continuous and are observed by the worker runtime. There is no manual executor "
                        + "for them, so there is nothing for Run Now to start.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(jobType),
                    jobType,
                    "The capability authority does not classify this job family. Every family is either executable "
                        + "by this runtime or explicitly not, and there is no third answer.");
        }
    }
}

/// <summary>T-106. The executor-capability refusal, with a stable name.</summary>
public static class JobExecutionErrorCodes
{
    /// <summary>The job family has no executor in this runtime.</summary>
    public const string NoExecutorForJobFamily = "JX01";
}

public static class JobExecutionErrors
{
    public static ApplicationError NoExecutorForJobFamily(JobDefinitionType jobType, string statement)
    {
        return new ApplicationError(
            JobExecutionErrorCodes.NoExecutorForJobFamily,
            "Job family " + jobType + " cannot be executed by this runtime. " + statement,
            ApplicationErrorType.BusinessRule);
    }
}