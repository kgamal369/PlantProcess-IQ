using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Services.Integration;

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

    Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
        Guid jobDefinitionId,
        int take,
        CancellationToken cancellationToken);
}