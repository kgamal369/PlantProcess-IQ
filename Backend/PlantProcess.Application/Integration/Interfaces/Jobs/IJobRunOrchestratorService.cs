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
}



