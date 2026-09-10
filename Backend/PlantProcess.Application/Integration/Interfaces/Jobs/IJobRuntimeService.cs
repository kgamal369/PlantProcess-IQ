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



