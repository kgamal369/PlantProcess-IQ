using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Mapping;

/// <summary>
/// PPIQ T-099. BATCH ORCHESTRATION ONLY.
///
/// Every decision about a single row now lives in IMappingRowProjector, which
/// preview, execution and bounded reprocess share. What remains here is the
/// batch: admission, one tenant resolution, the row loop, quarantine
/// persistence, and one SaveChanges.
///
/// ONE INVALID ROW NO LONGER ENDS A RUN. A refusal is a value that the loop
/// records before moving to the next row; the try/catch that used to turn every
/// refusal into a Failed row is gone, and an exception reaching this class again
/// means the infrastructure genuinely failed.
/// </summary>
public sealed class MappingExecutionService : IMappingExecutionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IMappingRowProjector _projector;
    private readonly ICanonicalIdentityResolver _identityResolver;
    private readonly ILogger<MappingExecutionService> _logger;

    public MappingExecutionService(
        IPlantProcessDbContext dbContext,
        IMappingRowProjector projector,
        ICanonicalIdentityResolver identityResolver,
        ILogger<MappingExecutionService> logger)
    {
        _dbContext = dbContext;
        _projector = projector;
        _identityResolver = identityResolver;
        _logger = logger;
    }

    public Task<ApplicationResult<MappingExecutionResult>> PreviewAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            mappingDefinitionId,
            importBatchId,
            take,
            previewOnly: true,
            stopOnFirstError: false,
            cancellationToken);
    }

    public Task<ApplicationResult<MappingExecutionResult>> ExecuteAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        bool stopOnFirstError,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            mappingDefinitionId,
            importBatchId,
            take,
            previewOnly: false,
            stopOnFirstError,
            cancellationToken);
    }

    private async Task<ApplicationResult<MappingExecutionResult>> RunAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        bool previewOnly,
        bool stopOnFirstError,
        CancellationToken cancellationToken)
    {
        if (mappingDefinitionId == Guid.Empty)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.Validation("Mapping definition ID is required."));

        if (importBatchId == Guid.Empty)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.Validation("Import batch ID is required."));

        var maxRows = Math.Clamp(take <= 0 ? 500 : take, 1, 5000);

        var mapping = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == mappingDefinitionId, cancellationToken);

        if (mapping is null)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.NotFound("Mapping definition does not exist."));

        if (!mapping.IsActive)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.BusinessRule("Mapping definition is inactive."));

        var batchExists = await _dbContext.ImportBatches
            .AnyAsync(x => x.Id == importBatchId, cancellationToken);

        if (!batchExists)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.NotFound("Import batch does not exist."));

        // DEFINITION FAILURE IS NOT ROW FAILURE. A malformed MappingJson or an
        // unsupported target entity ends the run here and creates no quarantine
        // evidence: no staged row caused it and no reprocess could fix it.
        var definitionError = _projector.ValidateDefinition(mapping, out var fieldMap);
        if (definitionError is not null)
            return ApplicationResult<MappingExecutionResult>.Failure(definitionError);

        // ADMISSION, NOT PVxx. One tenant for the whole execution.
        var tenantId = await _identityResolver.ResolveTenantAsync(null, cancellationToken);
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.BusinessRule(
                "No single active tenant could be resolved for this execution. Projection is refused before any row is processed."));
        }

        var executionContext = new MappingExecutionContext(tenantId.Value);

        var rows = await _dbContext.StagingRecords
            .Where(x =>
                x.ImportBatchId == importBatchId &&
                x.SourceObjectName == mapping.SourceObjectName &&
                (!x.IsProcessed || x.ProcessingStatus == "Pending"))
            .OrderBy(x => x.RowNumber)
            .Take(maxRows)
            .ToListAsync(cancellationToken);

        var rowResults = new List<MappingExecutionRowResult>();
        var mapped = 0;
        var skipped = 0;
        var quarantined = 0;

        foreach (var stagingRecord in rows)
        {
            var outcome = await _projector.ProcessOneRowAsync(
                mapping,
                fieldMap,
                stagingRecord,
                executionContext,
                previewOnly,
                cancellationToken);

            switch (outcome.Kind)
            {
                case RowProjectionKind.Mapped:
                    mapped++;
                    rowResults.Add(new MappingExecutionRowResult(
                        stagingRecord.Id, stagingRecord.RowNumber, "Mapped",
                        outcome.CanonicalEntityId, outcome.CanonicalEntityName, null));
                    break;

                case RowProjectionKind.Skipped:
                    skipped++;
                    rowResults.Add(new MappingExecutionRowResult(
                        stagingRecord.Id, stagingRecord.RowNumber, "Skipped",
                        outcome.CanonicalEntityId, outcome.CanonicalEntityName, outcome.Message));
                    break;

                default:
                    quarantined++;
                    var refusal = outcome.Refusal!;

                    if (!previewOnly)
                    {
                        await RecordQuarantineAsync(mapping, stagingRecord, executionContext, refusal, cancellationToken);

                        // Storage compatibility: no new staging state machine.
                        // The row keeps its existing Failed processing state and
                        // the typed evidence lives beside it.
                        stagingRecord.MarkFailed($"{refusal.Code}: {refusal.Detail}");
                    }

                    rowResults.Add(new MappingExecutionRowResult(
                        stagingRecord.Id, stagingRecord.RowNumber, "Quarantined",
                        null, null, refusal.Detail,
                        refusal.Code.ToString(), refusal.OffendingValue, refusal.SuggestedCorrection));

                    _logger.LogInformation(
                        "Row quarantined. Code={ValidationCode}, MappingDefinitionId={MappingDefinitionId}, ImportBatchId={ImportBatchId}, StagingRecordId={StagingRecordId}, RowNumber={RowNumber}",
                        refusal.Code,
                        mapping.Id,
                        importBatchId,
                        stagingRecord.Id,
                        stagingRecord.RowNumber);

                    // A quarantined row IS a row-level refusal, so the option
                    // still means what it says.
                    if (!previewOnly && stopOnFirstError)
                        goto finished;

                    break;
            }
        }

    finished:

        // PREVIEW PERSISTS NOTHING. Not a canonical entity, not a quarantine
        // record, not a staging state.
        if (!previewOnly)
            await _dbContext.SaveChangesAsync(cancellationToken);

        var output = new MappingExecutionResult(
            mapping.Id,
            importBatchId,
            mapping.MappingCode,
            mapping.TargetEntityName,
            previewOnly,
            maxRows,
            rowResults.Count,
            mapped,
            skipped,
            0,
            quarantined,
            rowResults);

        _logger.LogInformation(
            "Mapping execution finished. MappingDefinitionId={MappingDefinitionId}, ImportBatchId={ImportBatchId}, PreviewOnly={PreviewOnly}, Processed={Processed}, Mapped={Mapped}, Skipped={Skipped}, Quarantined={Quarantined}, Failed={Failed}",
            mapping.Id,
            importBatchId,
            previewOnly,
            output.ProcessedRows,
            output.MappedRows,
            output.SkippedRows,
            output.QuarantinedRows,
            output.FailedRows);

        return ApplicationResult<MappingExecutionResult>.Success(output);
    }

    /// <summary>
    /// One OPEN record per staged row per producing mapping version. Running the
    /// same version over the same row again counts an attempt instead of growing
    /// a duplicate queue.
    /// </summary>
    private async Task RecordQuarantineAsync(
        MappingDefinition mapping,
        StagingRecord stagingRecord,
        MappingExecutionContext executionContext,
        RowValidationRefusal refusal,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ProjectionQuarantineRecords
            .FirstOrDefaultAsync(
                x => x.StagingRecordId == stagingRecord.Id &&
                     x.MappingDefinitionId == mapping.Id &&
                     x.MappingVersion == mapping.MappingVersion &&
                     x.State == ProjectionQuarantineRecord.StateOpen,
                cancellationToken);

        if (existing is not null)
        {
            existing.RecordFailedAttempt(refusal.Code, refusal.Detail, refusal.OffendingValue);
            return;
        }

        var record = new ProjectionQuarantineRecord(
            validationCode: refusal.Code,
            detail: refusal.Detail,
            offendingValue: refusal.OffendingValue,
            importBatchId: stagingRecord.ImportBatchId,
            stagingRecordId: stagingRecord.Id,
            stagingRowNumber: stagingRecord.RowNumber,
            sourceObjectName: stagingRecord.SourceObjectName,
            mappingDefinitionId: mapping.Id,
            mappingVersion: mapping.MappingVersion,
            targetEntityName: mapping.TargetEntityName,
            tenantId: executionContext.TenantId,
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId);

        _dbContext.ProjectionQuarantineRecords.Add(record);
    }
}
