using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Interfaces.Staging;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Import;

/// <summary>
/// Professional import orchestration service for PlantProcess IQ.
///
/// This service is intentionally in the Application layer because it coordinates several domain areas:
/// ImportBatch, StagingRecord, MappingDefinition, MappingExecutionService and DataQualityService.
/// It is the clean end-to-end workflow required before correlation/ML can be trusted.
/// </summary>
public sealed class ImportWorkflowService : IImportWorkflowService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IStagingRecordService _stagingRecordService;
    private readonly IMappingExecutionService _mappingExecutionService;
    private readonly IDataQualityService _dataQualityService;
    private readonly ILogger<ImportWorkflowService> _logger;

    public ImportWorkflowService(
        IPlantProcessDbContext dbContext,
        IStagingRecordService stagingRecordService,
        IMappingExecutionService mappingExecutionService,
        IDataQualityService dataQualityService,
        ILogger<ImportWorkflowService> logger)
    {
        _dbContext = dbContext;
        _stagingRecordService = stagingRecordService;
        _mappingExecutionService = mappingExecutionService;
        _dataQualityService = dataQualityService;
        _logger = logger;
    }

    public async Task<ApplicationResult<ImportWorkflowResult>> RunAsync(
        RunImportWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;

        _logger.LogInformation(
            "Import workflow started. ImportBatchId={ImportBatchId}, SourceSystemDefinitionId={SourceSystemDefinitionId}, MappingDefinitionId={MappingDefinitionId}, Rows={Rows}, CorrelationId={CorrelationId}",
            command.ImportBatchId,
            command.SourceSystemDefinitionId,
            command.MappingDefinitionId,
            command.Rows.Count,
            command.Metadata.CorrelationId);

        if (command.MappingDefinitionId == Guid.Empty)
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.Validation("MappingDefinitionId is required."));

        if (command.ImportBatchId is null && command.SourceSystemDefinitionId == Guid.Empty)
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.Validation("SourceSystemDefinitionId is required when ImportBatchId is not supplied."));

        if (string.IsNullOrWhiteSpace(command.SourceObjectName))
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.Validation("SourceObjectName is required."));

        if (command.ImportBatchId is null && string.IsNullOrWhiteSpace(command.ImportBatchCode))
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.Validation("ImportBatchCode is required when creating a new batch."));

        var mapping = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.MappingDefinitionId, cancellationToken);

        if (mapping is null)
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.NotFound("Mapping definition does not exist."));

        if (!mapping.IsActive)
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.BusinessRule("Mapping definition is inactive."));

        if (!string.Equals(mapping.SourceObjectName, command.SourceObjectName, StringComparison.OrdinalIgnoreCase))
        {
            return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.BusinessRule(
                $"Workflow SourceObjectName '{command.SourceObjectName}' does not match MappingDefinition.SourceObjectName '{mapping.SourceObjectName}'."));
        }

        ImportBatch? batch = null;
        var stagingRowsCreated = 0;
        MappingExecutionResult? mappingResult = null;
        DataQualityScanSummary? dqSummary = null;

        try
        {
            batch = command.ImportBatchId.HasValue
                ? await _dbContext.ImportBatches.FirstOrDefaultAsync(x => x.Id == command.ImportBatchId.Value, cancellationToken)
                : null;

            if (command.ImportBatchId.HasValue && batch is null)
                return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.NotFound("Import batch does not exist."));

            if (batch is null)
            {
                var sourceExists = await _dbContext.SourceSystemDefinitions
                    .AnyAsync(x => x.Id == command.SourceSystemDefinitionId, cancellationToken);

                if (!sourceExists)
                    return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.NotFound("Source system definition does not exist."));

                var duplicateBatchCode = await _dbContext.ImportBatches
                    .AnyAsync(x => x.ImportBatchCode == command.ImportBatchCode, cancellationToken);

                if (duplicateBatchCode)
                    return ApplicationResult<ImportWorkflowResult>.Failure(ApplicationError.Conflict("Import batch code already exists."));

                batch = new ImportBatch(
                    sourceSystemDefinitionId: command.SourceSystemDefinitionId,
                    importBatchCode: command.ImportBatchCode!,
                    importType: string.IsNullOrWhiteSpace(command.ImportType) ? "Workflow" : command.ImportType,
                    isSynthetic: command.Metadata.IsSynthetic,
                    sourceObjectName: command.SourceObjectName,
                    fileName: command.FileName,
                    checksum: command.Checksum,
                    sourceSystem: command.Metadata.SourceSystem,
                    sourceRecordId: command.Metadata.SourceRecordId);

                _dbContext.ImportBatches.Add(batch);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            batch.MarkRunning();
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (command.Rows.Count > 0)
            {
                var bulkCommand = new BulkCreateStagingRecordsCommand(
                    ImportBatchId: batch.Id,
                    SourceObjectName: command.SourceObjectName,
                    Rows: command.Rows
                        .OrderBy(x => x.RowNumber)
                        .Select(x => new CreateStagingRecordRow(x.RowNumber, x.RawJson, x.SourceRecordId))
                        .ToList(),
                    Metadata: command.Metadata);

                var stagingResult = await _stagingRecordService.CreateBulkAsync(bulkCommand, cancellationToken);
                if (stagingResult.IsFailure || stagingResult.Value is null)
                    throw new InvalidOperationException(stagingResult.Error?.Message ?? "Failed to create staging records.");

                stagingRowsCreated = stagingResult.Value.Accepted;
            }

            var executionResult = await _mappingExecutionService.ExecuteAsync(
                command.MappingDefinitionId,
                batch.Id,
                command.MappingTake <= 0 ? 5000 : command.MappingTake,
                command.StopOnFirstError,
                cancellationToken);

            if (executionResult.IsFailure || executionResult.Value is null)
                throw new InvalidOperationException(executionResult.Error?.Message ?? "Mapping execution failed.");

            mappingResult = executionResult.Value;

            if (command.RunDataQualityScan)
            {
                var dqResult = await _dataQualityService.RunFullScanAsync(
                    command.DataQualityMaxCandidatesPerRule <= 0 ? 500 : command.DataQualityMaxCandidatesPerRule,
                    cancellationToken);

                if (dqResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Import workflow completed mapping but data quality scan failed. ImportBatchId={ImportBatchId}, Error={Error}",
                        batch.Id,
                        dqResult.Error?.Message);
                }
                else
                {
                    dqSummary = dqResult.Value;
                }
            }

            if (mappingResult.FailedRows > 0)
            {
                batch.MarkFailed($"Mapping completed with {mappingResult.FailedRows} failed row(s). Review staging row ProcessingError values.");
            }
            else
            {
                batch.MarkCompleted(mappingResult.ProcessedRows);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var finishedAtUtc = DateTime.UtcNow;
            var result = new ImportWorkflowResult(
                ImportBatchId: batch.Id,
                MappingDefinitionId: command.MappingDefinitionId,
                ImportBatchCode: batch.ImportBatchCode,
                SourceObjectName: command.SourceObjectName,
                ImportBatchStatus: batch.Status,
                InputRows: command.Rows.Count,
                StagingRowsCreated: stagingRowsCreated,
                MappingProcessedRows: mappingResult.ProcessedRows,
                MappingMappedRows: mappingResult.MappedRows,
                MappingSkippedRows: mappingResult.SkippedRows,
                MappingFailedRows: mappingResult.FailedRows,
                DataQualityCandidatesFound: dqSummary?.CandidatesFound ?? 0,
                DataQualityNewIssuesPersisted: dqSummary?.NewIssuesPersisted ?? 0,
                DataQualityExistingIssuesSkipped: dqSummary?.ExistingIssuesSkipped ?? 0,
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: finishedAtUtc,
                Duration: finishedAtUtc - startedAtUtc,
                ErrorMessage: batch.ErrorMessage,
                MappingRows: mappingResult.Rows);

            _logger.LogInformation(
                "Import workflow finished. ImportBatchId={ImportBatchId}, Status={Status}, Processed={Processed}, Mapped={Mapped}, Failed={Failed}, NewDQIssues={NewDQIssues}, DurationMs={DurationMs}",
                result.ImportBatchId,
                result.ImportBatchStatus,
                result.MappingProcessedRows,
                result.MappingMappedRows,
                result.MappingFailedRows,
                result.DataQualityNewIssuesPersisted,
                (long)result.Duration.TotalMilliseconds);

            return ApplicationResult<ImportWorkflowResult>.Success(result);
        }
        catch (Exception ex)
        {
            if (batch is not null)
            {
                batch.MarkFailed(ex.Message);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogError(
                ex,
                "Import workflow failed. ImportBatchId={ImportBatchId}, MappingDefinitionId={MappingDefinitionId}, Error={Error}",
                batch?.Id,
                command.MappingDefinitionId,
                ex.Message);

            var finishedAtUtc = DateTime.UtcNow;
            var failureResult = new ImportWorkflowResult(
                ImportBatchId: batch?.Id ?? Guid.Empty,
                MappingDefinitionId: command.MappingDefinitionId,
                ImportBatchCode: batch?.ImportBatchCode ?? command.ImportBatchCode ?? string.Empty,
                SourceObjectName: command.SourceObjectName,
                ImportBatchStatus: batch?.Status ?? "Failed",
                InputRows: command.Rows.Count,
                StagingRowsCreated: stagingRowsCreated,
                MappingProcessedRows: mappingResult?.ProcessedRows ?? 0,
                MappingMappedRows: mappingResult?.MappedRows ?? 0,
                MappingSkippedRows: mappingResult?.SkippedRows ?? 0,
                MappingFailedRows: mappingResult?.FailedRows ?? 0,
                DataQualityCandidatesFound: dqSummary?.CandidatesFound ?? 0,
                DataQualityNewIssuesPersisted: dqSummary?.NewIssuesPersisted ?? 0,
                DataQualityExistingIssuesSkipped: dqSummary?.ExistingIssuesSkipped ?? 0,
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: finishedAtUtc,
                Duration: finishedAtUtc - startedAtUtc,
                ErrorMessage: ex.Message,
                MappingRows: mappingResult?.Rows ?? Array.Empty<MappingExecutionRowResult>());

            return ApplicationResult<ImportWorkflowResult>.Failure(
                ApplicationError.Infrastructure($"Import workflow failed: {ex.Message}"));
        }
    }
}




