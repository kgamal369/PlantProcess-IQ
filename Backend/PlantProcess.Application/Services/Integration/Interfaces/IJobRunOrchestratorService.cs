using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Contracts.Integration.Jobs;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IJobRunOrchestratorService
{
    Task<ApplicationResult<JobActionResponseDto>> RunNowAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken);
}