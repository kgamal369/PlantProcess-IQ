using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;

namespace PlantProcess.Application.Integration.Services.SourceSystems;

public sealed class IncrementalSyncStateService : IIncrementalSyncStateService
{
    private readonly IPlantProcessDbContext _dbContext;

    public IncrementalSyncStateService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<string?>> GetLastCursorValueAsync(
        Guid sourceDatasetDefinitionId,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == sourceDatasetDefinitionId, cancellationToken);

        if (dataset is null)
            return ApplicationResult<string?>.Failure(ApplicationError.NotFound("Source dataset definition was not found."));

        return ApplicationResult<string?>.Success(dataset.LastCursorValue);
    }

    public async Task<ApplicationResult<bool>> ShouldImportAsync(
        Guid sourceDatasetDefinitionId,
        string? sourceMaxCursorValue,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == sourceDatasetDefinitionId, cancellationToken);

        if (dataset is null)
            return ApplicationResult<bool>.Failure(ApplicationError.NotFound("Source dataset definition was not found."));

        if (string.IsNullOrWhiteSpace(sourceMaxCursorValue))
            return ApplicationResult<bool>.Success(false);

        if (string.IsNullOrWhiteSpace(dataset.LastCursorValue))
            return ApplicationResult<bool>.Success(true);

        return ApplicationResult<bool>.Success(
            CompareCursorValues(sourceMaxCursorValue, dataset.LastCursorValue) > 0);
    }

    public async Task<ApplicationResult> UpdateLastCursorValueAsync(
        Guid sourceDatasetDefinitionId,
        string? lastCursorValue,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == sourceDatasetDefinitionId, cancellationToken);

        if (dataset is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Source dataset definition was not found."));

        dataset.UpdateLastCursorValue(lastCursorValue);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    private static int CompareCursorValues(string sourceMaxCursorValue, string lastCursorValue)
    {
        if (decimal.TryParse(sourceMaxCursorValue, out var sourceNumber) &&
            decimal.TryParse(lastCursorValue, out var lastNumber))
        {
            return sourceNumber.CompareTo(lastNumber);
        }

        if (DateTime.TryParse(sourceMaxCursorValue, out var sourceDate) &&
            DateTime.TryParse(lastCursorValue, out var lastDate))
        {
            return sourceDate.ToUniversalTime().CompareTo(lastDate.ToUniversalTime());
        }

        return string.Compare(
            sourceMaxCursorValue,
            lastCursorValue,
            StringComparison.OrdinalIgnoreCase);
    }
}


