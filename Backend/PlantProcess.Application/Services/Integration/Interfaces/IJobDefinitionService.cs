using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration.Jobs;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IJobDefinitionService
{
    Task<ApplicationResult<IReadOnlyList<JobDefinitionDto>>> GetJobsAsync(
        JobDefinitionType? jobType,
        bool includeDisabled,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> GetJobByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> CreateJobAsync(
        CreateJobDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> UpdateJobAsync(
        Guid id,
        UpdateJobDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> EnableJobAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> DisableJobAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobDefinitionDto>> UpdateRunStatusAsync(
        Guid id,
        UpdateJobRunStatusRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> EnsureSystemJobsSeededAsync(
        CancellationToken cancellationToken);
}