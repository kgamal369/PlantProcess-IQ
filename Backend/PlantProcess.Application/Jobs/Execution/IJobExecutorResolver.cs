using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// DISPATCH ONLY.
///
/// This answers one question - which implementation executes this family - and it is
/// deliberately unable to answer whether the family is supported. Support policy stays
/// with the capability authority, because two places able to say "supported" is exactly
/// how a runtime ends up advertising an executor it does not have.
///
/// A family with no executor returns null. The caller turns that into the runtime
/// diagnostic, because at that point capability and dispatch disagree and that is a
/// programming defect worth failing loudly on rather than falling back from.
/// </summary>
public interface IJobExecutorResolver
{
    IJobExecutor? Resolve(JobDefinitionType jobType);
}