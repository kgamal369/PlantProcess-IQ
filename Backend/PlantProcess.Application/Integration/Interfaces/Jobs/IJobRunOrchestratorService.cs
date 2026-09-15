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
    /// T-106 B2.3c. Executes one governed scheduled occurrence through the same admission,
    /// capability and target authorities Run Now uses. Manual and scheduled intent stay
    /// distinct: Run Now never carries an occurrence, and this never pretends to be manual.
    ///
    /// The default body FAILS CLOSED, so an implementation that has not implemented
    /// scheduled orchestration cannot execute a scheduled occurrence by falling back.
    /// </summary>
    Task<ApplicationResult<JobActionResponseDto>> RunScheduledAsync(
        Guid jobDefinitionId,
        string occurrenceKey,
        DateTime nominalAtUtc,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(ApplicationResult<JobActionResponseDto>.Failure(ApplicationError.BusinessRule(
            "This orchestrator does not implement governed scheduled execution.")));

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