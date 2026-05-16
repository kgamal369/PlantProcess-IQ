using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IIncrementalSyncStateService
{
    Task<ApplicationResult<string?>> GetLastCursorValueAsync(
        Guid sourceDatasetDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<bool>> ShouldImportAsync(
        Guid sourceDatasetDefinitionId,
        string? sourceMaxCursorValue,
        CancellationToken cancellationToken);

    Task<ApplicationResult> UpdateLastCursorValueAsync(
        Guid sourceDatasetDefinitionId,
        string? lastCursorValue,
        CancellationToken cancellationToken);
}