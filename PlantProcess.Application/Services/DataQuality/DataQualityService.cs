using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Domain.Entities.Quality;

namespace PlantProcess.Application.Services.DataQuality;

public sealed class DataQualityService : IDataQualityService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<DataQualityService> _logger;

    public DataQualityService(
        IPlantProcessDbContext dbContext,
        ILogger<DataQualityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }


    public async Task<ApplicationResult<Guid>> RaiseIssueAsync(
        RaiseDataQualityIssueCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IssueType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Issue type is required."));

        if (string.IsNullOrWhiteSpace(command.Description))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Issue description is required."));

        if (command.MaterialUnitId.HasValue)
        {
            var materialExists = await _dbContext.MaterialUnits
                .AnyAsync(x => x.Id == command.MaterialUnitId.Value, cancellationToken);

            if (!materialExists)
                return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));
        }

        var issue = new DataQualityIssue(
            issueType: command.IssueType,
            description: command.Description,
            isSynthetic: command.Metadata.IsSynthetic,
            materialUnitId: command.MaterialUnitId,
            severity: command.Severity ?? "Warning",
            affectedEntityName: command.AffectedEntityName,
            affectedEntityId: command.AffectedEntityId,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        _dbContext.DataQualityIssues.Add(issue);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Raised data-quality issue. DataQualityIssueId={DataQualityIssueId}, IssueType={IssueType}, Severity={Severity}, MaterialUnitId={MaterialUnitId}, CorrelationId={CorrelationId}",
            issue.Id,
            issue.IssueType,
            issue.Severity,
            issue.MaterialUnitId,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(issue.Id);
    }
}