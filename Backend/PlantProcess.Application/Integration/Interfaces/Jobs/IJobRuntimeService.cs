using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Interfaces.Jobs;

public interface IJobRuntimeService
{
    Task<ApplicationResult<JobRunHistoryDto>> StartAsync(
        string jobCode,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// T-106 B2.3c. Creates the run for a governed scheduled occurrence. The occurrence
    /// identity is written by the same INSERT that creates the run, so migration 843's
    /// unique index is what decides a race, not a prior read.
    ///
    /// The default body FAILS CLOSED. An implementation that has not implemented scheduled
    /// admission must not be able to execute a scheduled run, and must never silently fall
    /// back to the manual path and discard the occurrence identity.
    /// </summary>
    Task<ApplicationResult<JobRunHistoryDto>> StartScheduledAsync(
        string jobCode,
        string occurrenceKey,
        DateTime nominalAtUtc,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.BusinessRule(
            "This job runtime does not implement governed scheduled admission, so it cannot start a scheduled occurrence.")));

    Task<ApplicationResult<JobRunHistoryDto>> CompleteAsync(
        Guid jobRunHistoryId,
        JobRunStatus finalStatus,
        string? message,
        string? failureReason,
        string? resultSummaryJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// T-106. Records an attempt that was admitted into orchestration and then
    /// prevented from computing. It is NOT StartAsync followed by a completion:
    /// claiming a run started when admission refused compute would be the same
    /// class of falsehood the corrective removes elsewhere.
    /// </summary>
    Task<ApplicationResult<JobRunHistoryDto>> RecordBlockedAsync(
        Guid jobDefinitionId,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        string reason,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
        Guid jobDefinitionId,
        int take,
        CancellationToken cancellationToken);
}



