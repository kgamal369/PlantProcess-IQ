using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;

namespace PlantProcess.Application.Integration.Interfaces.Jobs;

public interface IJobRunOrchestratorService
{
    Task<ApplicationResult<JobActionResponseDto>> RunNowAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// T-106. Runs the job together with everything it declares a dependency
    /// on, predecessors first, through the same execution path Run Now uses.
    /// The chain stops at the first failure: running a successor whose
    /// predecessor failed would produce a result nobody could trust.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<JobActionResponseDto>>> RunWithDependenciesAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken);
}