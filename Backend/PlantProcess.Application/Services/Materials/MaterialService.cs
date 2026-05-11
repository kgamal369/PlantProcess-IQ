using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Materials;
using PlantProcess.Domain.Entities.Materials;

namespace PlantProcess.Application.Services.Materials;

public sealed class MaterialService : IMaterialService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IPlantTimeContextResolver _timeContextResolver;
    private readonly ILogger<MaterialService> _logger;

    public MaterialService(
        IPlantProcessDbContext dbContext,
        IPlantTimeContextResolver timeContextResolver,
        ILogger<MaterialService> logger)
    {
        _dbContext = dbContext;
        _timeContextResolver = timeContextResolver;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> CreateAsync(
        CreateMaterialCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.MaterialCode))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material code is required."));

        if (string.IsNullOrWhiteSpace(command.MaterialUnitType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit type is required."));

        if (command.SiteId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Site ID is required."));

        if (command.ProductionStartUtc.HasValue &&
            command.ProductionStartUtc.Value.Kind != DateTimeKind.Utc)
        {
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("ProductionStartUtc must be UTC."));
        }

        if (command.ProductionEndUtc.HasValue &&
            command.ProductionEndUtc.Value.Kind != DateTimeKind.Utc)
        {
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("ProductionEndUtc must be UTC."));
        }

        var site = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == command.SiteId)
            .Select(x => new
            {
                x.Id,
                x.TimeZoneId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (site is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Site does not exist."));

        var normalizedMaterialCode = command.MaterialCode.Trim();

        var exists = await _dbContext.MaterialUnits.AnyAsync(x =>
                x.SiteId == command.SiteId &&
                x.MaterialCode == normalizedMaterialCode,
            cancellationToken);

        if (exists)
        {
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Material '{normalizedMaterialCode}' already exists for this site."));
        }

        var material = new MaterialUnit(
            materialCode: normalizedMaterialCode,
            materialUnitType: command.MaterialUnitType,
            siteId: command.SiteId,
            productFamily: command.ProductFamily,
            gradeOrRecipe: command.GradeOrRecipe,
            isSynthetic: command.Metadata.IsSynthetic,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        if (command.ProductionStartUtc.HasValue)
        {
            var referenceUtc = command.ProductionStartUtc.Value;

            var timeContext = _timeContextResolver.Resolve(
                command.PlantTimeZoneId ?? site.TimeZoneId,
                referenceUtc);

            material.SetProductionWindow(
                startUtc: command.ProductionStartUtc.Value,
                endUtc: command.ProductionEndUtc,
                plantUtcOffset: TimeSpan.FromMinutes(command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes),
                plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId);
        }

        _dbContext.MaterialUnits.Add(material);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created material. MaterialUnitId={MaterialUnitId}, MaterialCode={MaterialCode}, MaterialUnitType={MaterialUnitType}, SiteId={SiteId}, CorrelationId={CorrelationId}",
            material.Id,
            material.MaterialCode,
            material.MaterialUnitType,
            material.SiteId,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(material.Id);
    }

    public async Task<ApplicationResult<Guid>> AddAliasAsync(
        AddMaterialAliasCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit ID is required."));

        if (string.IsNullOrWhiteSpace(command.AliasCode))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Alias code is required."));

        if (string.IsNullOrWhiteSpace(command.SourceSystem))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Alias source system is required."));

        var materialExists = await _dbContext.MaterialUnits
            .AnyAsync(x => x.Id == command.MaterialUnitId, cancellationToken);

        if (!materialExists)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        var aliasCode = command.AliasCode.Trim();
        var sourceSystem = command.SourceSystem.Trim();

        var duplicate = await _dbContext.MaterialAliases.AnyAsync(x =>
                x.MaterialUnitId == command.MaterialUnitId &&
                x.AliasCode == aliasCode &&
                x.SourceSystem == sourceSystem,
            cancellationToken);

        if (duplicate)
            return ApplicationResult<Guid>.Failure(ApplicationError.Conflict("Material alias already exists."));

        var alias = new MaterialAlias(
            materialUnitId: command.MaterialUnitId,
            aliasCode: aliasCode,
            sourceSystem: sourceSystem,
            aliasType: command.AliasType,
            isSynthetic: command.Metadata.IsSynthetic);

        _dbContext.MaterialAliases.Add(alias);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added material alias. MaterialAliasId={MaterialAliasId}, MaterialUnitId={MaterialUnitId}, AliasCode={AliasCode}, SourceSystem={SourceSystem}, CorrelationId={CorrelationId}",
            alias.Id,
            alias.MaterialUnitId,
            alias.AliasCode,
            alias.SourceSystem,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(alias.Id);
    }
}