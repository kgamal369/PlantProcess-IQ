using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Mapping;

public sealed class MappingDefinitionService : IMappingDefinitionService
{
    // ── Allowed canonical target entities ─────────────────────────────────────
    // This list must stay in sync with the actual domain entities registered in
    // IPlantProcessDbContext. Any new canonical entity must be added here before
    // a mapping definition can target it.
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

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<MappingDefinitionService> _logger;

    // ── Constructor ───────────────────────────────────────────────────────────
    public MappingDefinitionService(
        IPlantProcessDbContext dbContext,
        ILogger<MappingDefinitionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CreateAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApplicationResult<Guid>> CreateAsync(
        CreateMappingDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "MappingDefinitionService.CreateAsync called. " +
            "MappingCode={MappingCode}, SourceSystemDefinitionId={SourceSystemDefinitionId}, " +
            "TargetEntityName={TargetEntityName}, CorrelationId={CorrelationId}",
            command.MappingCode,
            command.SourceSystemDefinitionId,
            command.TargetEntityName,
            command.Metadata.CorrelationId);

        // ── Guard: required identifiers ────────────────────────────────────
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

        // ── Guard: target entity must be a known canonical entity ──────────
        if (string.IsNullOrWhiteSpace(command.TargetEntityName))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Target entity name is required."));

        if (!AllowedTargetEntities.Contains(command.TargetEntityName.Trim()))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation(
                    $"Target entity '{command.TargetEntityName}' is not a supported canonical entity. " +
                    $"Allowed values: {string.Join(", ", AllowedTargetEntities)}."));

        // ── Guard: mapping JSON must be present and valid JSON ─────────────
        if (string.IsNullOrWhiteSpace(command.MappingJson))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Mapping JSON is required."));

        try
        {
            // Parse to validate structure — dispose immediately, we don't need the document
            using var _ = JsonDocument.Parse(command.MappingJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "Invalid mapping JSON provided. MappingCode={MappingCode}, JsonError={JsonError}",
                command.MappingCode,
                ex.Message);

            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation($"Mapping JSON is not valid JSON: {ex.Message}"));
        }

        // ── Guard: source system must exist ────────────────────────────────
        var sourceExists = await _dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.Id == command.SourceSystemDefinitionId, cancellationToken);

        if (!sourceExists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.NotFound("Source system definition does not exist."));

        // ── Guard: mapping code must be globally unique ────────────────────
        var normalizedCode = command.MappingCode.Trim();

        var exists = await _dbContext.MappingDefinitions
            .AnyAsync(x => x.MappingCode == normalizedCode, cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Mapping definition '{normalizedCode}' already exists."));

        // ── Create and persist ─────────────────────────────────────────────
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
            "Created mapping definition. " +
            "MappingDefinitionId={MappingDefinitionId}, MappingCode={MappingCode}, " +
            "TargetEntityName={TargetEntityName}, SourceSystemDefinitionId={SourceSystemDefinitionId}, " +
            "CorrelationId={CorrelationId}",
            mapping.Id,
            mapping.MappingCode,
            mapping.TargetEntityName,
            mapping.SourceSystemDefinitionId,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(mapping.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UpdateMappingJsonAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApplicationResult> UpdateMappingJsonAsync(
        Guid mappingDefinitionId,
        string mappingJson,
        string mappingVersion,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "MappingDefinitionService.UpdateMappingJsonAsync called. " +
            "MappingDefinitionId={MappingDefinitionId}, MappingVersion={MappingVersion}",
            mappingDefinitionId,
            mappingVersion);

        // ── Guard: required fields ─────────────────────────────────────────
        if (mappingDefinitionId == Guid.Empty)
            return ApplicationResult.Failure(
                ApplicationError.Validation("Mapping definition ID is required."));

        if (string.IsNullOrWhiteSpace(mappingJson))
            return ApplicationResult.Failure(
                ApplicationError.Validation("Mapping JSON is required."));

        if (string.IsNullOrWhiteSpace(mappingVersion))
            return ApplicationResult.Failure(
                ApplicationError.Validation("Mapping version is required."));

        // ── Guard: new JSON must be valid ──────────────────────────────────
        try
        {
            using var _ = JsonDocument.Parse(mappingJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "Invalid mapping JSON provided during update. " +
                "MappingDefinitionId={MappingDefinitionId}, JsonError={JsonError}",
                mappingDefinitionId,
                ex.Message);

            return ApplicationResult.Failure(
                ApplicationError.Validation($"Mapping JSON is not valid JSON: {ex.Message}"));
        }

        // ── Load entity ────────────────────────────────────────────────────
        var mapping = await _dbContext.MappingDefinitions
            .FirstOrDefaultAsync(x => x.Id == mappingDefinitionId, cancellationToken);

        if (mapping is null)
            return ApplicationResult.Failure(
                ApplicationError.NotFound($"Mapping definition '{mappingDefinitionId}' not found."));

        // ── Apply domain method and persist ───────────────────────────────
        mapping.UpdateMappingJson(mappingJson.Trim(), mappingVersion.Trim());
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated mapping definition JSON. " +
            "MappingDefinitionId={MappingDefinitionId}, MappingCode={MappingCode}, " +
            "NewMappingVersion={NewMappingVersion}",
            mapping.Id,
            mapping.MappingCode,
            mapping.MappingVersion);

        return ApplicationResult.Success();
    }
}



