using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Services.Integration;

public sealed class StagingRecordService : IStagingRecordService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<StagingRecordService> _logger;

    public StagingRecordService(
        IPlantProcessDbContext dbContext,
        ILogger<StagingRecordService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<StagingRecordBulkCreateResult>> CreateBulkAsync(
        BulkCreateStagingRecordsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ImportBatchId == Guid.Empty)
            return ApplicationResult<StagingRecordBulkCreateResult>.Failure(ApplicationError.Validation("Import batch ID is required."));

        if (string.IsNullOrWhiteSpace(command.SourceObjectName))
            return ApplicationResult<StagingRecordBulkCreateResult>.Failure(ApplicationError.Validation("Source object name is required."));

        if (command.Rows.Count == 0)
            return ApplicationResult<StagingRecordBulkCreateResult>.Failure(ApplicationError.Validation("At least one staging row is required."));

        if (command.Rows.Count > 5000)
            return ApplicationResult<StagingRecordBulkCreateResult>.Failure(ApplicationError.Validation("Maximum 5000 staging rows can be inserted in one request."));

        var batch = await _dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => x.Id == command.ImportBatchId)
            .Select(x => new { x.Id, x.SourceSystemDefinitionId })
            .FirstOrDefaultAsync(cancellationToken);

        if (batch is null)
            return ApplicationResult<StagingRecordBulkCreateResult>.Failure(ApplicationError.NotFound("Import batch does not exist."));

        var accepted = 0;
        var rejected = new List<StagingRecordBulkRejectedRow>();
        var seenRows = new HashSet<int>();

        foreach (var row in command.Rows.OrderBy(x => x.RowNumber))
        {
            if (row.RowNumber <= 0)
            {
                rejected.Add(new StagingRecordBulkRejectedRow(row.RowNumber, "RowNumber must be greater than zero."));
                continue;
            }

            if (!seenRows.Add(row.RowNumber))
            {
                rejected.Add(new StagingRecordBulkRejectedRow(row.RowNumber, "Duplicate RowNumber in request."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.RawJson))
            {
                rejected.Add(new StagingRecordBulkRejectedRow(row.RowNumber, "RawJson is required."));
                continue;
            }

            try
            {
                using var _ = JsonDocument.Parse(row.RawJson);
            }
            catch (JsonException ex)
            {
                rejected.Add(new StagingRecordBulkRejectedRow(row.RowNumber, $"RawJson is not valid JSON: {ex.Message}"));
                continue;
            }

            var exists = await _dbContext.StagingRecords.AnyAsync(x =>
                    x.ImportBatchId == command.ImportBatchId &&
                    x.SourceObjectName == command.SourceObjectName.Trim() &&
                    x.RowNumber == row.RowNumber,
                cancellationToken);

            if (exists)
            {
                rejected.Add(new StagingRecordBulkRejectedRow(row.RowNumber, "Staging record already exists for this import batch/source object/row number."));
                continue;
            }

            var stagingRecord = new StagingRecord(
                importBatchId: command.ImportBatchId,
                sourceObjectName: command.SourceObjectName,
                rowNumber: row.RowNumber,
                rawJson: row.RawJson,
                isSynthetic: command.Metadata.IsSynthetic,
                sourceSystem: command.Metadata.SourceSystem,
                sourceRecordId: row.SourceRecordId ?? command.Metadata.SourceRecordId);

            _dbContext.StagingRecords.Add(stagingRecord);
            accepted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created staging records. ImportBatchId={ImportBatchId}, SourceObjectName={SourceObjectName}, Accepted={Accepted}, Rejected={Rejected}, CorrelationId={CorrelationId}",
            command.ImportBatchId,
            command.SourceObjectName,
            accepted,
            rejected.Count,
            command.Metadata.CorrelationId);

        return ApplicationResult<StagingRecordBulkCreateResult>.Success(
            new StagingRecordBulkCreateResult(
                command.ImportBatchId,
                command.SourceObjectName.Trim(),
                accepted,
                rejected.Count,
                rejected));
    }
}
