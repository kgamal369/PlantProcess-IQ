using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Jobs.Canvas;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Interfaces.Jobs;

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

    /// <summary>
    /// T-245. Binds a job to an exact governed definition target, through this authority
    /// and no other writer.
    ///
    /// IDEMPOTENT BY JOB CODE. Binding the same definition twice addresses the same job:
    /// it never creates a second one, and it never changes a job of another family. Only
    /// the target is written, so the schedule, the enabled state and any pool settings a
    /// job already carries survive untouched. A concurrent first bind is resolved by the
    /// database rather than by a check-then-write race.
    ///
    /// A new binding governs FUTURE runs. A run already in flight keeps the target and
    /// version its own record captured.
    /// </summary>
    Task<ApplicationResult<GovernedJobBindingResult>> BindGovernedTargetAsync(
        GovernedJobBindingRequest request,
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



