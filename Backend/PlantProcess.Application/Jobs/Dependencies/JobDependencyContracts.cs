using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Jobs.Dependencies;

/// <summary>
/// T-106. One edge, read the same way everywhere: the dependent job runs after
/// the predecessor.
/// </summary>
public sealed record JobDependencyEdge(Guid JobDefinitionId, Guid DependsOnJobDefinitionId);

/// <summary>
/// T-106. THE JD REFUSAL VOCABULARY.
///
/// Stable names, because a refusal a caller cannot switch on is a log line.
/// They sit inside the repository's existing error convention rather than
/// beside it as a second error architecture, exactly as the JB codes do.
/// </summary>
public static class JobDependencyErrorCodes
{
    /// <summary>An edge from a job to itself.</summary>
    public const string SelfDependency = "JD01";

    /// <summary>An edge that would close a cycle. Refused at save time.</summary>
    public const string DependencyCycle = "JD02";

    /// <summary>The same edge already exists.</summary>
    public const string DuplicateDependencyEdge = "JD03";

    /// <summary>One end of the edge names a job that does not exist.</summary>
    public const string UnknownJobInDependency = "JD04";
}

public static class JobDependencyErrors
{
    public static ApplicationError SelfDependency(Guid jobDefinitionId)
    {
        return new ApplicationError(
            JobDependencyErrorCodes.SelfDependency,
            "Job " + jobDefinitionId + " cannot depend on itself. A self dependency can never be satisfied, "
                + "so the job would never become runnable.",
            ApplicationErrorType.BusinessRule);
    }

    public static ApplicationError DependencyCycle(Guid jobDefinitionId, Guid dependsOnJobDefinitionId)
    {
        return new ApplicationError(
            JobDependencyErrorCodes.DependencyCycle,
            "The dependency from job " + jobDefinitionId + " to job " + dependsOnJobDefinitionId
                + " would close a cycle. Execution order would be undefined and the chain would not terminate.",
            ApplicationErrorType.BusinessRule);
    }

    public static ApplicationError DuplicateDependencyEdge(Guid jobDefinitionId, Guid dependsOnJobDefinitionId)
    {
        return new ApplicationError(
            JobDependencyErrorCodes.DuplicateDependencyEdge,
            "Job " + jobDefinitionId + " already depends on job " + dependsOnJobDefinitionId
                + ". One edge states the ordering once.",
            ApplicationErrorType.Conflict);
    }

    public static ApplicationError UnknownJobInDependency(Guid jobDefinitionId)
    {
        return new ApplicationError(
            JobDependencyErrorCodes.UnknownJobInDependency,
            "Job " + jobDefinitionId + " does not exist, so it cannot take part in a dependency.",
            ApplicationErrorType.NotFound);
    }
}