using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Quality;
using PlantProcess.Domain.Entities.Quality;

namespace PlantProcess.Application.Services.Quality;

public sealed class QualityService : IQualityService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IPlantTimeContextResolver _timeContextResolver;
    private readonly ILogger<QualityService> _logger;

    public QualityService(
        IPlantProcessDbContext dbContext,
        IPlantTimeContextResolver timeContextResolver,
        ILogger<QualityService> logger)
    {
        _dbContext = dbContext;
        _timeContextResolver = timeContextResolver;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> AddDefectCatalogAsync(
        AddDefectCatalogCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DefectCode))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Defect code is required."));

        if (string.IsNullOrWhiteSpace(command.DefectName))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Defect name is required."));

        var defectCode = command.DefectCode.Trim();

        var exists = await _dbContext.DefectCatalogs
            .AnyAsync(x => x.DefectCode == defectCode, cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(ApplicationError.Conflict("Defect code already exists."));

        var defect = new DefectCatalog(
            defectCode: defectCode,
            defectName: command.DefectName,
            defectCategory: command.DefectCategory,
            industryTemplate: command.IndustryTemplate,
            isSynthetic: command.Metadata.IsSynthetic,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        _dbContext.DefectCatalogs.Add(defect);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added defect catalog. DefectCatalogId={DefectCatalogId}, DefectCode={DefectCode}, CorrelationId={CorrelationId}",
            defect.Id,
            defect.DefectCode,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(defect.Id);
    }

    public async Task<ApplicationResult<Guid>> AddQualityEventAsync(
        AddQualityEventCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit ID is required."));

        if (string.IsNullOrWhiteSpace(command.EventType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Event type is required."));

        if (command.EventAtUtc.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("EventAtUtc must be UTC."));

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == command.MaterialUnitId)
            .Select(x => new { x.Id, x.SiteId })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        if (command.DefectCatalogId.HasValue)
        {
            var defectExists = await _dbContext.DefectCatalogs
                .AnyAsync(x => x.Id == command.DefectCatalogId.Value, cancellationToken);

            if (!defectExists)
                return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Defect catalog does not exist."));
        }

        var siteTimeZoneId = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == material.SiteId)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeContext = _timeContextResolver.Resolve(
            command.PlantTimeZoneId ?? siteTimeZoneId,
            command.EventAtUtc);

        var qualityEvent = new QualityEvent(
            materialUnitId: command.MaterialUnitId,
            eventType: command.EventType,
            eventAtUtc: command.EventAtUtc,
            isSynthetic: command.Metadata.IsSynthetic,
            defectCatalogId: command.DefectCatalogId,
            severity: command.Severity,
            decision: command.Decision,
            description: command.Description,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.QualityEvents.Add(qualityEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added quality event. QualityEventId={QualityEventId}, MaterialUnitId={MaterialUnitId}, EventType={EventType}, Decision={Decision}, CorrelationId={CorrelationId}",
            qualityEvent.Id,
            qualityEvent.MaterialUnitId,
            qualityEvent.EventType,
            qualityEvent.Decision,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(qualityEvent.Id);
    }
}


