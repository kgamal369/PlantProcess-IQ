using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// T-106. Whether a family declares a governed definition target at all.
/// Required and NotUsed are the only two states the current authority names;
/// there is no Optional, because no family declares one.
/// </summary>
public enum JobTargetRequirement
{
    NotUsed = 0,
    Required = 1
}

/// <summary>
/// T-106. ONE RECORD, TWO QUESTIONS.
///
/// Capability admission and the target contract are answered from the same row
/// because they are the same fact about a family. A second table would be a
/// second truth, and the placeholder class policy this replaces is exactly what
/// a second truth decays into.
///
/// VERSION POLICY IS A SHAPE, NOT A WHITELIST. Where a governed target is
/// declared, the legal policies are the frozen T-064 pair and nothing else, so
/// the descriptor states applicability rather than enumerating a per-family
/// subset that no authority declares.
/// </summary>
public sealed record JobExecutionCapability(
    JobDefinitionType JobType,
    bool IsExecutableByRuntime,
    JobTargetRequirement TargetRequirement,
    DefinitionKind? CanonicalTargetKind,
    string Statement)
{
    /// <summary>True only where a governed target is declared.</summary>
    public bool VersionPolicyApplies => TargetRequirement == JobTargetRequirement.Required;
}

public interface IJobExecutionCapabilityAuthority
{
    /// <summary>
    /// Total over the enum. A family this method does not classify is a
    /// programming error, not a default.
    /// </summary>
    JobExecutionCapability Describe(JobDefinitionType jobType);

    IReadOnlyList<JobDefinitionType> ExecutableFamilies { get; }
}

/// <summary>
/// T-106. THE ONE AUTHORITATIVE ANSWER ABOUT A JOB FAMILY.
///
/// WHAT CHANGED IN THE CORRECTIVE, AND WHY.
///
/// CanonicalRefresh was classified executable because a switch case existed for
/// it. That case invokes the generic pending-import processor. It does not
/// resolve, and cannot execute, the governed Transformation version a projection
/// job declares - measured: nothing in the backend reads compiled_sql, and the
/// only consumer of IDefinitionService on any job path is the resolver, which
/// resolves and never executes. The scheduled Worker path reaches the same
/// generic processor, so it proves nothing either. A working wrong behaviour is
/// still a defect, so the family is now honestly unavailable.
///
/// Its TARGET semantics are still stated. Knowing what a family would execute
/// is a different fact from being able to execute it, and the product is more
/// honest for holding both.
///
/// DataQualityScan and RiskScoring keep their real executors and declare no
/// canonical target, because the current design maps neither legacy enum name
/// to a definition kind. Inventing one to make the table look symmetrical would
/// be inventing product.
/// </summary>
public sealed class JobExecutionCapabilityAuthority : IJobExecutionCapabilityAuthority
{
    private static readonly JobDefinitionType[] Executable =
    {
        JobDefinitionType.DbLinkImport,
        JobDefinitionType.DataQualityScan,
        JobDefinitionType.RiskScoring
    };

    public IReadOnlyList<JobDefinitionType> ExecutableFamilies => Executable;

    public JobExecutionCapability Describe(JobDefinitionType jobType)
    {
        switch (jobType)
        {
            case JobDefinitionType.DbLinkImport:
                return new JobExecutionCapability(
                    jobType, true, JobTargetRequirement.NotUsed, null,
                    "Import queue execution is commissioned in this runtime. Its governing input "
                        + "identity is source, dataset and connection authority, not the definition store.");

            case JobDefinitionType.DataQualityScan:
                return new JobExecutionCapability(
                    jobType, true, JobTargetRequirement.NotUsed, null,
                    "Data quality scanning is commissioned in this runtime. Current authority maps "
                        + "this legacy family to no canonical definition kind.");

            case JobDefinitionType.RiskScoring:
                return new JobExecutionCapability(
                    jobType, true, JobTargetRequirement.NotUsed, null,
                    "Risk scoring is commissioned in this runtime. Current authority maps this "
                        + "legacy family to no canonical definition kind.");

            case JobDefinitionType.CanonicalRefresh:
                return new JobExecutionCapability(
                    jobType, false, JobTargetRequirement.Required, DefinitionKind.Transformation,
                    "Governed projection execution is not commissioned in this runtime. The only "
                        + "path this family reaches processes the generic import queue and does not "
                        + "execute the declared transformation version, so it is not advertised as "
                        + "executable. Existing schedules and history remain readable.");

            case JobDefinitionType.MlParamsVsDefects:
            case JobDefinitionType.MlParamsVsDowntime:
            case JobDefinitionType.MlParamsVsKpis:
            case JobDefinitionType.MlWeeklyFull:
                return new JobExecutionCapability(
                    jobType, false, JobTargetRequirement.Required, DefinitionKind.Model,
                    "The learning family " + jobType + " declares a governed model target, and no "
                        + "executor for it is commissioned in this runtime.");

            case JobDefinitionType.Custom:
                return new JobExecutionCapability(
                    jobType, false, JobTargetRequirement.NotUsed, null,
                    "Custom jobs are continuous and are observed by the worker runtime. There is no "
                        + "manual executor for them, so there is nothing for Run Now to start.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(jobType), jobType,
                    "The capability authority does not classify this job family. Every family is "
                        + "either executable by this runtime or explicitly not, and there is no "
                        + "third answer.");
        }
    }
}

/// <summary>T-106. The execution-capability refusal, with a stable name.</summary>
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