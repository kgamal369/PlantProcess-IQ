using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface IMlReadinessService
{
    Task<MlReadinessScoreDto> GetReadinessAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MlJobReadinessDto>> GetMlJobsAsync(
        CancellationToken cancellationToken = default);

    Task EnsureMlJobDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<MlWorkspaceReadinessDto> GetWorkspaceAsync(
        int labelPreviewLimit,
        CancellationToken cancellationToken = default);
}