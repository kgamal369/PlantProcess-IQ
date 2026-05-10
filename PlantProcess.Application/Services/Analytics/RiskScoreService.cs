using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Domain.Entities.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public sealed class RiskScoreService : IRiskScoreService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IPlantTimeContextResolver _timeContextResolver;
    private readonly ILogger<RiskScoreService> _logger;

    public RiskScoreService(
        IPlantProcessDbContext dbContext,
        IPlantTimeContextResolver timeContextResolver,
        ILogger<RiskScoreService> logger)
    {
        _dbContext = dbContext;
        _timeContextResolver = timeContextResolver;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> StoreAsync(
        StoreRiskScoreCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit ID is required."));

        if (string.IsNullOrWhiteSpace(command.RiskType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Risk type is required."));

        if (command.Score < 0 || command.Score > 1)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Risk score must be between 0 and 1."));

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == command.MaterialUnitId)
            .Select(x => new { x.Id, x.SiteId })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        var siteTimeZoneId = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == material.SiteId)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeContext = _timeContextResolver.Resolve(
            command.PlantTimeZoneId ?? siteTimeZoneId,
            DateTime.UtcNow);

        var riskClass = string.IsNullOrWhiteSpace(command.RiskClass)
            ? CalculateRiskClass(command.Score)
            : command.RiskClass.Trim();

        var riskScore = new RiskScore(
            materialUnitId: command.MaterialUnitId,
            riskType: command.RiskType,
            score: command.Score,
            isSynthetic: command.Metadata.IsSynthetic,
            riskClass: riskClass,
            mainContributorsJson: command.MainContributorsJson,
            modelVersion: command.ModelVersion,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.RiskScores.Add(riskScore);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stored risk score. RiskScoreId={RiskScoreId}, MaterialUnitId={MaterialUnitId}, RiskType={RiskType}, Score={Score}, RiskClass={RiskClass}, CorrelationId={CorrelationId}",
            riskScore.Id,
            riskScore.MaterialUnitId,
            riskScore.RiskType,
            riskScore.Score,
            riskScore.RiskClass,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(riskScore.Id);
    }

    private static string CalculateRiskClass(decimal score)
    {
        if (score >= 0.70m)
            return "High";

        if (score >= 0.40m)
            return "Medium";

        return "Low";
    }

    public Task<ApplicationResult> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}