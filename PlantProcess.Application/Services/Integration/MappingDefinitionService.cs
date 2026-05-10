using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Services.Integration;

public sealed class MappingDefinitionService : IMappingDefinitionService
{
    private static readonly HashSet<string> AllowedTargetEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "MaterialUnit",
        "MaterialAlias",
        "GenealogyEdge",
        "ProcessStepExecution",
        "ParameterDefinition",
        "ParameterObservation",
        "ProcessEvent",
        "DowntimeEvent",
        "DefectCatalog",
        "QualityEvent",
        "RiskScore",
        "DataQualityIssue"
    };

    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<MappingDefinitionService> _logger;

    public MappingDefinitionService(
        IPlantProcessDbContext dbContext,
        ILogger<MappingDefinitionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> CreateAsync(
        CreateMappingDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourceSystemDefinitionId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Source system definition ID is required."));

        if (string.IsNullOrWhiteSpace(command.MappingCode))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Mapping code is required."));

        if (string.IsNullOrWhiteSpace(command.MappingName))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Mapping name is required."));

        if (string.IsNullOrWhiteSpace(command.SourceObjectName))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Source object name is required."));

        if (string.IsNullOrWhiteSpace(command.TargetEntityName))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Target entity name is required."));

        if (!AllowedTargetEntities.Contains(command.TargetEntityName.Trim()))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation($"Target entity '{command.TargetEntityName}' is not supported."));

        if (string.IsNullOrWhiteSpace(command.MappingJson))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Mapping JSON is required."));

        try
        {
            JsonDocument.Parse(command.MappingJson);
        }
        catch (JsonException ex)
        {
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation($"Mapping JSON is invalid: {ex.Message}"));
        }

        var sourceExists = await _dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.Id == command.SourceSystemDefinitionId, cancellationToken);

        if (!sourceExists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.NotFound("Source system definition does not exist."));

        var normalizedCode = command.MappingCode.Trim();

        var exists = await _dbContext.MappingDefinitions
            .AnyAsync(x => x.MappingCode == normalizedCode, cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Mapping definition '{normalizedCode}' already exists."));

        var mapping = new MappingDefinition(
            sourceSystemDefinitionId: command.SourceSystemDefinitionId,
            mappingCode: normalizedCode,
            mappingName: command.MappingName,
            sourceObjectName: command.SourceObjectName,
            targetEntityName: command.TargetEntityName,
            mappingJson: command.MappingJson,
            isSynthetic: command.Metadata.IsSynthetic,
            mappingVersion: command.MappingVersion ?? "v1",
            description: command.Description,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        _dbContext.MappingDefinitions.Add(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created mapping definition. MappingDefinitionId={MappingDefinitionId}, MappingCode={MappingCode}, TargetEntityName={TargetEntityName}, SourceSystemDefinitionId={SourceSystemDefinitionId}, CorrelationId={CorrelationId}",
            mapping.Id,
            mapping.MappingCode,
            mapping.TargetEntityName,
            mapping.SourceSystemDefinitionId,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(mapping.Id);
    }
}