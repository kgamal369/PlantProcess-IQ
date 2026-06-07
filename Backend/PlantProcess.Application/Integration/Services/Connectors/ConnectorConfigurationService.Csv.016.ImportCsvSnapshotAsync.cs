using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Connectors;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Connectors;

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_SPLIT
public sealed partial class ConnectorConfigurationService
{
public async Task<ApplicationResult<CsvImportSnapshotResult>> ImportCsvSnapshotAsync(
        Guid sourceDatasetDefinitionId,
        CsvImportSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId && !x.IsDeleted, cancellationToken);

        if (dataset is null)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.NotFound("Source dataset not found."));

        var profile = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.NotFound("Connection profile not found."));

        if (!profile.IsActive)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.BusinessRule("Connection profile is inactive."));

        if (profile.ProviderType != "Csv")
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.Validation("CSV snapshot import is only available for Csv provider profiles in Phase 3."));

        var sourceSystem = await _dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == profile.SourceSystemDefinitionId && !x.IsDeleted, cancellationToken);

        if (sourceSystem is null)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.NotFound("Source system definition not found."));

        if (!sourceSystem.IsActive)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.BusinessRule("Source system is inactive."));

        var delimiter = ResolveDelimiter(request.Delimiter);
        var hasHeader = request.HasHeader ?? true;

        var parsed = CsvTextParser.Parse(
            request.CsvText,
            delimiter,
            hasHeader,
            maxRows: 200_000);

        if (parsed.Rows.Count == 0)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.Validation("CSV contains no data rows."));

        var importBatchCode = string.IsNullOrWhiteSpace(request.ImportBatchCode)
            ? $"CSV-{dataset.DatasetCode}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.ImportBatchCode.Trim();

        var batch = new ImportBatch(
            sourceSystemDefinitionId: profile.SourceSystemDefinitionId,
            importBatchCode: importBatchCode,
            importType: "CsvSnapshot",
            isSynthetic: request.IsSynthetic,
            sourceObjectName: dataset.SourceObjectName,
            fileName: request.FileName,
            checksum: request.Checksum,
            sourceSystem: request.SourceSystem ?? "PlantProcessIQ.AdminCsvConnector",
            sourceRecordId: request.SourceRecordId);

        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        batch.MarkRunning();

        var rowNumber = 1;
        var stagingRecords = parsed.Rows.Select(row =>
        {
            var rawJson = JsonSerializer.Serialize(row, JsonOptions);

            return new StagingRecord(
                importBatchId: batch.Id,
                sourceObjectName: dataset.SourceObjectName,
                rowNumber: rowNumber++,
                rawJson: rawJson,
                isSynthetic: request.IsSynthetic,
                sourceSystem: request.SourceSystem ?? "PlantProcessIQ.AdminCsvConnector",
                sourceRecordId: row.TryGetValue("SourceRecordId", out var sourceRecordId)
                    ? sourceRecordId
                    : null);
        }).ToList();

        _dbContext.StagingRecords.AddRange(stagingRecords);

        batch.MarkCompleted(stagingRecords.Count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<CsvImportSnapshotResult>.Success(
            new CsvImportSnapshotResult(
                batch.Id,
                batch.ImportBatchCode,
                dataset.Id,
                profile.Id,
                profile.SourceSystemDefinitionId,
                dataset.SourceObjectName,
                stagingRecords.Count,
                batch.Status,
                batch.StartedAtUtc,
                batch.CompletedAtUtc));
    }
}
