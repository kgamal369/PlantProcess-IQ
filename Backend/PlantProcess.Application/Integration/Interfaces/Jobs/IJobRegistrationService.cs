using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;

namespace PlantProcess.Application.Integration.Interfaces.Jobs;

public interface IJobRegistrationService
{
    Task<ApplicationResult> RegisterSystemJobsAsync(
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> UpsertJobAsync(
        UpsertJobDefinitionRequest request,
        CancellationToken cancellationToken);
}



