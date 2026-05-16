using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface IJobRegistrationService
{
    Task<ApplicationResult> RegisterSystemJobsAsync(
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> UpsertJobAsync(
        UpsertJobDefinitionRequest request,
        CancellationToken cancellationToken);
}