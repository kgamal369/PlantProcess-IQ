using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Mapping;

/// <summary>
/// PPIQ T-099. BOUNDED REPROCESSING OF EXACTLY ONE ROW.
///
/// This service never calls the batch executor. A queue entry names one exact
/// staged row; reaching it by re-running a batch would touch rows nobody asked
/// about and would depend on pending-row ordering no caller controls. It also
/// never calls ResetProcessing followed by a broad execution, for the same
/// reason.
///
/// RESOLVED IS ONLY EVER REPORTED AFTER THE CANONICAL WRITE PERSISTED.
/// </summary>
public sealed class ProjectionQuarantineReprocessService : IProjectionQuarantineReprocessService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IMappingRowProjector _projector;
    private readonly ICanonicalIdentityResolver _identityResolver;
    private readonly ILogger<ProjectionQuarantineReprocessService> _logger;

    public ProjectionQuarantineReprocessService(
        IPlantProcessDbContext dbContext,
        IMappingRowProjector projector,
        ICanonicalIdentityResolver identityResolver,
        ILogger<ProjectionQuarantineReprocessService> logger)
    {
        _dbContext = dbContext;
        _projector = projector;
        _identityResolver = identityResolver;
        _logger = logger;
    }

    public async Task<ApplicationResult<QuarantineReprocessResult>> ReprocessAsync(
        Guid quarantineId,
        string? tenantCode,
        CancellationToken cancellationToken)
    {
        if (quarantineId == Guid.Empty)
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.Validation("Quarantine ID is required."));

        var tenantId = await _identityResolver.ResolveTenantAsync(tenantCode, cancellationToken);
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.BusinessRule(
                "No single active tenant could be resolved for this reprocess. The row was not touched."));
        }

        var quarantine = await _dbContext.ProjectionQuarantineRecords
            .FirstOrDefaultAsync(
                x => x.Id == quarantineId &&
                     x.TenantId == tenantId.Value &&
                     x.State == ProjectionQuarantineRecord.StateOpen,
                cancellationToken);

        if (quarantine is null)
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.NotFound("No open quarantine record exists for this identity under the resolved tenant."));

        var stagingRecord = await _dbContext.StagingRecords
            .FirstOrDefaultAsync(x => x.Id == quarantine.StagingRecordId, cancellationToken);

        if (stagingRecord is null)
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.NotFound("The staged row this quarantine record refers to no longer exists."));

        var mapping = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == quarantine.MappingDefinitionId, cancellationToken);

        if (mapping is null)
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.NotFound("The mapping definition this quarantine record refers to no longer exists."));

        if (!mapping.IsActive)
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.BusinessRule("Mapping definition is inactive."));

        var definitionError = _projector.ValidateDefinition(mapping, out var fieldMap);
        if (definitionError is not null)
            return ApplicationResult<QuarantineReprocessResult>.Failure(definitionError);

        // A fresh context: this is one row, so nothing in this execution can
        // collide with a sibling and PV05 cannot fire spuriously.
        var executionContext = new MappingExecutionContext(tenantId.Value);

        var outcome = await _projector.ProcessOneRowAsync(
            mapping,
            fieldMap,
            stagingRecord,
            executionContext,
            previewOnly: false,
            cancellationToken);

        if (outcome.Kind == RowProjectionKind.Quarantined)
        {
            var refusal = outcome.Refusal!;
            quarantine.RecordFailedAttempt(refusal.Code, refusal.Detail, refusal.OffendingValue);
            stagingRecord.MarkFailed($"{refusal.Code}: {refusal.Detail}");

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Quarantine reprocess still refused. QuarantineId={QuarantineId}, Code={ValidationCode}, Attempt={AttemptCount}",
                quarantine.Id,
                refusal.Code,
                quarantine.AttemptCount);

            return ApplicationResult<QuarantineReprocessResult>.Success(Describe(quarantine, stagingRecord, null));
        }

        // Mapped or Skipped both mean the row no longer refuses. Only a
        // canonical identity can close the record, and MarkResolved is called
        // AFTER the write is persisted below.
        if (outcome.CanonicalEntityId is null || outcome.CanonicalEntityId == Guid.Empty)
        {
            return ApplicationResult<QuarantineReprocessResult>.Failure(ApplicationError.BusinessRule(
                "The row projected without producing a canonical identity, so the quarantine record cannot be resolved."));
        }

        quarantine.MarkResolved(outcome.CanonicalEntityId.Value);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Quarantine reprocess resolved. QuarantineId={QuarantineId}, CanonicalId={CanonicalId}",
            quarantine.Id,
            outcome.CanonicalEntityId.Value);

        return ApplicationResult<QuarantineReprocessResult>.Success(
            Describe(quarantine, stagingRecord, outcome.CanonicalEntityName));
    }

    private static QuarantineReprocessResult Describe(
        ProjectionQuarantineRecord quarantine,
        StagingRecord stagingRecord,
        string? canonicalName) =>
        new(
            quarantine.Id,
            stagingRecord.Id,
            stagingRecord.RowNumber,
            quarantine.State,
            quarantine.AttemptCount,
            quarantine.State == ProjectionQuarantineRecord.StateOpen ? quarantine.ValidationCode : null,
            quarantine.State == ProjectionQuarantineRecord.StateOpen ? quarantine.Detail : null,
            quarantine.State == ProjectionQuarantineRecord.StateOpen ? quarantine.OffendingValue : null,
            quarantine.State == ProjectionQuarantineRecord.StateOpen ? quarantine.SuggestedCorrection : null,
            quarantine.ResolvedCanonicalId,
            canonicalName);
}
