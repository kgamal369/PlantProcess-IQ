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

    Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
        Guid jobDefinitionId,
        int take,
        CancellationToken cancellationToken);
}



