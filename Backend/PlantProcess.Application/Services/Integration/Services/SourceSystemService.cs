using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration.Commands;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Services.Integration.Services;

public sealed class SourceSystemService : ISourceSystemService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<SourceSystemService> _logger;

    public SourceSystemService(
        IPlantProcessDbContext dbContext,
        ILogger<SourceSystemService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> RegisterAsync(
        RegisterSourceSystemCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.SourceSystemCode))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Source system code is required."));

        if (string.IsNullOrWhiteSpace(command.SourceSystemName))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Source system name is required."));

        if (string.IsNullOrWhiteSpace(command.SourceSystemType))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Source system type is required."));

        var normalizedCode = command.SourceSystemCode.Trim();

        var exists = await _dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.SourceSystemCode == normalizedCode, cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Source system '{normalizedCode}' already exists."));

        var sourceSystem = new SourceSystemDefinition(
            sourceSystemCode: normalizedCode,
            sourceSystemName: command.SourceSystemName,
            sourceSystemType: command.SourceSystemType,
            isSynthetic: command.Metadata.IsSynthetic,
            description: command.Description,
            isReadOnlySource: command.IsReadOnlySource,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        _dbContext.SourceSystemDefinitions.Add(sourceSystem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registered source system. SourceSystemId={SourceSystemId}, SourceSystemCode={SourceSystemCode}, SourceSystemType={SourceSystemType}, IsReadOnly={IsReadOnly}, CorrelationId={CorrelationId}",
            sourceSystem.Id,
            sourceSystem.SourceSystemCode,
            sourceSystem.SourceSystemType,
            sourceSystem.IsReadOnlySource,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(sourceSystem.Id);
    }
}