using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>
/// T-064. THE JB REFUSAL VOCABULARY OF CHAPTER 3 SECTION 4.5.5a.
///
/// These four identifiers are supplied by the specification and are recorded
/// here, not authored here. They sit inside the repository's existing error
/// convention - ApplicationError carries the code, the sentence and the type -
/// rather than beside it as a second error architecture.
///
/// One state named by the product is deliberately NOT given a JB identifier
/// below: a stored target identity that resolves to no definition at all.
/// 4.5.5a as supplied names JB01 for a job saved with no target and JB03 for a
/// pinned version that is unpublished or superseded, and neither describes a
/// target id pointing at nothing. That state is refused through the existing
/// NotFound convention with a readable sentence, and it is reported as an open
/// question rather than answered with an invented JB05.
/// </summary>
public static class JobTargetErrorCodes
{
    /// <summary>A job of a target-requiring class was saved with no target. The message names the class.</summary>
    public const string NoTargetOnTargetRequiringClass = "JB01";

    /// <summary>The target surface does not match the job class. The message names both.</summary>
    public const string TargetSurfaceDoesNotMatchJobClass = "JB02";

    /// <summary>The pinned version is not published, or has been superseded.</summary>
    public const string PinnedVersionNotPublishedOrSuperseded = "JB03";

    /// <summary>Deletion attempted for a definition targeted by jobs. The message names the jobs.</summary>
    public const string DefinitionTargetedByJobs = "JB04";
}

/// <summary>
/// Builds the four governed refusals. Every one of them names the thing it is
/// refusing about, because a code with no sentence beside it is not a refusal a
/// plant engineer can act on.
/// </summary>
public static class JobTargetErrors
{
    public static ApplicationError NoTargetOnTargetRequiringClass(string jobClass)
    {
        return new ApplicationError(
            JobTargetErrorCodes.NoTargetOnTargetRequiringClass,
            "A job of class " + jobClass + " must declare which definition it executes, "
                + "and this one declares none. Assign a target definition and a version policy.",
            ApplicationErrorType.BusinessRule);
    }

    public static ApplicationError TargetSurfaceDoesNotMatchJobClass(string jobClass, string targetKind)
    {
        return new ApplicationError(
            JobTargetErrorCodes.TargetSurfaceDoesNotMatchJobClass,
            "A job of class " + jobClass + " cannot execute a definition of kind " + targetKind + ".",
            ApplicationErrorType.BusinessRule);
    }

    public static ApplicationError PinnedVersionNotPublishedOrSuperseded(
        string targetKind, Guid definitionId, int pinnedVersion, string detail)
    {
        return new ApplicationError(
            JobTargetErrorCodes.PinnedVersionNotPublishedOrSuperseded,
            "Version " + pinnedVersion + " of " + targetKind + " definition " + definitionId
                + " cannot be run: " + detail,
            ApplicationErrorType.BusinessRule);
    }

    public static ApplicationError DefinitionTargetedByJobs(
        string targetKind, Guid definitionId, IReadOnlyList<string> jobCodes)
    {
        return new ApplicationError(
            JobTargetErrorCodes.DefinitionTargetedByJobs,
            targetKind + " definition " + definitionId + " cannot be deleted: it is the declared "
                + "target of " + string.Join(", ", jobCodes) + ".",
            ApplicationErrorType.Conflict);
    }
}
