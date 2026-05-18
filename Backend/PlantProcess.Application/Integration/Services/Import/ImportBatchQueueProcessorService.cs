using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Services.DataQuality;

namespace PlantProcess.Application.Integration.Services.Import;

/// <summary>
/// Worker-facing processor that consumes already-created import batches with pending staging records.
/// It keeps background automation out of Worker.cs and in the Application layer.
/// </summary>
public sealed class ImportBatchQueueProcessorService : IImportBatchQueueProcessorService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IMappingExecutionService _mappingExecutionService;
    private readonly IDataQualityService _dataQualityService;
    private readonly ILogger<ImportBatchQueueProcessorService> _logger;

    public ImportBatchQueueProcessorService(
        IPlantProcessDbContext dbContext,
        IMappingExecutionService mappingExecutionService,
        IDataQualityService dataQualityService,
        ILogger<ImportBatchQueueProcessorService> logger)
    {
        _dbContext = dbContext;
        _mappingExecutionService = mappingExecutionService;
        _dataQualityService = dataQualityService;
        _logger = logger;
    }

    public async Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
        int maxBatches,
        int rowsPerBatch,
        bool stopOnFirstError,
        bool runDataQualityScan,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var batchLimit = Math.Clamp(maxBatches <= 0 ? 5 : maxBatches, 1, 100);
        var rowLimit = Math.Clamp(rowsPerBatch <= 0 ? 5000 : rowsPerBatch, 1, 50000);
        var items = new List<ImportQueueProcessingItem>();

        var candidates = await _dbContext.ImportBatches
            .Where(x => x.Status == "Created" || x.Status == "Running")
            .OrderBy(x => x.StartedAtUtc)
            .Take(batchLimit)
            .ToListAsync(cancellationToken);

        foreach (var batch in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(batch.SourceObjectName))
            {
                items.Add(new ImportQueueProcessingItem(batch.Id, batch.ImportBatchCode, batch.SourceObjectName ?? string.Empty, "Skipped", null, null, 0, 0, 0, "SourceObjectName is empty."));
                continue;
            }

            var pendingRows = await _dbContext.StagingRecords.CountAsync(x =>
                x.ImportBatchId == batch.Id &&
                (!x.IsProcessed || x.ProcessingStatus == "Pending"),
                cancellationToken);

            if (pendingRows == 0)
            {
                items.Add(new ImportQueueProcessingItem(batch.Id, batch.ImportBatchCode, batch.SourceObjectName, "Skipped", null, null, 0, 0, 0, "No pending staging rows."));
                continue;
            }

            var mapping = await _dbContext.MappingDefinitions
                .AsNoTracking()
                .Where(x =>
                    x.SourceSystemDefinitionId == batch.SourceSystemDefinitionId &&
                    x.SourceObjectName == batch.SourceObjectName &&
                    x.IsActive)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (mapping is null)
            {
                var message = $"No active mapping definition found for SourceObjectName '{batch.SourceObjectName}'.";
                batch.MarkFailed(message);
                await _dbContext.SaveChangesAsync(cancellationToken);
                items.Add(new ImportQueueProcessingItem(batch.Id, batch.ImportBatchCode, batch.SourceObjectName, "Failed", null, null, 0, 0, 0, message));
                continue;
            }

            try
            {
                batch.MarkRunning();
                await _dbContext.SaveChangesAsync(cancellationToken);

                var result = await _mappingExecutionService.ExecuteAsync(mapping.Id, batch.Id, rowLimit, stopOnFirstError, cancellationToken);
                if (result.IsFailure || result.Value is null)
                    throw new InvalidOperationException(result.Error?.Message ?? "Mapping execution failed.");

                var mappingResult = result.Value;

                if (mappingResult.FailedRows > 0)
                    batch.MarkFailed($"Mapping finished with {mappingResult.FailedRows} failed row(s).");
                else
                    batch.MarkCompleted(mappingResult.ProcessedRows);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (runDataQualityScan)
                    await _dataQualityService.RunFullScanAsync(500, cancellationToken);

                items.Add(new ImportQueueProcessingItem(
                    batch.Id,
                    batch.ImportBatchCode,
                    batch.SourceObjectName,
                    batch.Status,
                    mapping.Id,
                    mapping.MappingCode,
                    mappingResult.ProcessedRows,
                    mappingResult.MappedRows,
                    mappingResult.FailedRows,
                    batch.ErrorMessage));
            }
            catch (Exception ex)
            {
                batch.MarkFailed(ex.Message);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex, "Queued import batch processing failed. ImportBatchId={ImportBatchId}", batch.Id);

                items.Add(new ImportQueueProcessingItem(
                    batch.Id,
                    batch.ImportBatchCode,
                    batch.SourceObjectName,
                    "Failed",
                    mapping.Id,
                    mapping.MappingCode,
                    0,
                    0,
                    0,
                    ex.Message));
            }
        }

        var finishedAtUtc = DateTime.UtcNow;
        var summary = new ImportQueueProcessingSummary(
            StartedAtUtc: startedAtUtc,
            FinishedAtUtc: finishedAtUtc,
            Duration: finishedAtUtc - startedAtUtc,
            BatchesScanned: candidates.Count,
            BatchesProcessed: items.Count(x => x.Status is "Completed" or "Failed"),
            BatchesCompleted: items.Count(x => x.Status == "Completed"),
            BatchesFailed: items.Count(x => x.Status == "Failed"),
            BatchesSkipped: items.Count(x => x.Status == "Skipped"),
            Items: items);

        _logger.LogInformation(
            "Queued import batch processing finished. Scanned={Scanned}, Completed={Completed}, Failed={Failed}, Skipped={Skipped}, DurationMs={DurationMs}",
            summary.BatchesScanned,
            summary.BatchesCompleted,
            summary.BatchesFailed,
            summary.BatchesSkipped,
            (long)summary.Duration.TotalMilliseconds);

        return ApplicationResult<ImportQueueProcessingSummary>.Success(summary);
    }
}




