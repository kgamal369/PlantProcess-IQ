using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Services.Integration;

public sealed class ImportBatchService : IImportBatchService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<ImportBatchService> _logger;

    public ImportBatchService(
        IPlantProcessDbContext dbContext,
        ILogger<ImportBatchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> CreateAsync(
        CreateImportBatchCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourceSystemDefinitionId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Source system definition ID is required."));

        if (string.IsNullOrWhiteSpace(command.ImportBatchCode))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Import batch code is required."));

        if (string.IsNullOrWhiteSpace(command.ImportType))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Import type is required."));

        var sourceExists = await _dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.Id == command.SourceSystemDefinitionId, cancellationToken);

        if (!sourceExists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.NotFound("Source system definition does not exist."));

        var normalizedCode = command.ImportBatchCode.Trim();

        var exists = await _dbContext.ImportBatches
            .AnyAsync(x => x.ImportBatchCode == normalizedCode, cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Import batch '{normalizedCode}' already exists."));

        var importBatch = new ImportBatch(
            sourceSystemDefinitionId: command.SourceSystemDefinitionId,
            importBatchCode: normalizedCode,
            importType: command.ImportType,
            isSynthetic: command.Metadata.IsSynthetic,
            sourceObjectName: command.SourceObjectName,
            fileName: command.FileName,
            checksum: command.Checksum,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        _dbContext.ImportBatches.Add(importBatch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created import batch. ImportBatchId={ImportBatchId}, ImportBatchCode={ImportBatchCode}, SourceSystemDefinitionId={SourceSystemDefinitionId}, CorrelationId={CorrelationId}",
            importBatch.Id,
            importBatch.ImportBatchCode,
            importBatch.SourceSystemDefinitionId,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(importBatch.Id);
    }

    public async Task<ApplicationResult> MarkRunningAsync(
        Guid importBatchId,
        CancellationToken cancellationToken)
    {
        var batch = await _dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);

        if (batch is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Import batch not found."));

        batch.MarkRunning();

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked import batch as running. ImportBatchId={ImportBatchId}, ImportBatchCode={ImportBatchCode}",
            batch.Id,
            batch.ImportBatchCode);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> MarkCompletedAsync(
        Guid importBatchId,
        int rowCount,
        CancellationToken cancellationToken)
    {
        if (rowCount < 0)
            return ApplicationResult.Failure(ApplicationError.Validation("Row count cannot be negative."));

        var batch = await _dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);

        if (batch is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Import batch not found."));

        batch.MarkCompleted(rowCount);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked import batch as completed. ImportBatchId={ImportBatchId}, ImportBatchCode={ImportBatchCode}, RowCount={RowCount}",
            batch.Id,
            batch.ImportBatchCode,
            rowCount);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> MarkFailedAsync(
        Guid importBatchId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var batch = await _dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);

        if (batch is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Import batch not found."));

        batch.MarkFailed(errorMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Marked import batch as failed. ImportBatchId={ImportBatchId}, ImportBatchCode={ImportBatchCode}, ErrorMessage={ErrorMessage}",
            batch.Id,
            batch.ImportBatchCode,
            errorMessage);

        return ApplicationResult.Success();
    }
}