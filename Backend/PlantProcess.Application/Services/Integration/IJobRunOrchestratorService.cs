using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface IJobRunOrchestratorService
{
    Task<ApplicationResult<JobActionResponseDto>> RunNowAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken);
}