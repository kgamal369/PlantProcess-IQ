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

    /// <summary>
    /// T-106 B2.4. Records an operator's request that a run stop. It does not stop the run
    /// and does not change its status: only the executor can answer it. Repeating the
    /// request is a no-op that keeps the original requester and time.
    ///
    /// The default body FAILS CLOSED, so a runtime that has not implemented the request
    /// path cannot pretend to have accepted one.
    /// </summary>
    Task<ApplicationResult<JobRunHistoryDto>> RequestCancellationAsync(
        Guid jobDefinitionId,
        Guid jobRunHistoryId,
        string? requestedBy,
        string? reason,
        CancellationToken cancellationToken)
        => Task.FromResult(ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.BusinessRule(
            "This job runtime does not implement run cancellation requests.")));

    /// <summary>
    /// T-106 B2.4. The executor answering a request: it stopped cooperatively, so the run
    /// converges to the terminal Cancelled state. T-261 owns propagating the request into
    /// the real executor; this is the seam it will call.
    /// </summary>
    Task<ApplicationResult<JobRunHistoryDto>> AcknowledgeCancellationAsync(
        Guid jobRunHistoryId,
        string? message,
        CancellationToken cancellationToken)
        => Task.FromResult(ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.BusinessRule(
            "This job runtime does not implement cancellation acknowledgement.")));
}



