using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Contracts.Integration.Jobs;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IJobRegistrationService
{
    Task<ApplicationResult> RegisterSystemJobsAsync(
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> UpsertJobAsync(
        UpsertJobDefinitionRequest request,
        CancellationToken cancellationToken);
}