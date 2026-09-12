using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Mapping;

public sealed class MappingDefinitionService : IMappingDefinitionService
{
    // PPIQ T-256. THE PROJECTION-TARGET AUTHORITY IS NOT OURS TO HOLD.
    //
    // This class used to carry its own HashSet of twelve entity names. It agreed
    // with the canonical catalogue on every one of them, which is exactly why it
    // was dangerous: two lists that agree today diverge silently tomorrow. Marking
    // a thirteenth entity ICanonicalProjectionTarget would have widened what
    // authoring offers while mapping went on refusing it, and nothing would have
    // reported the disagreement.
    //
    // Eligibility is declared by the Domain marker and read through
    // ICanonicalEntityCatalog.IsProjectionTarget - the same predicate the Canvas
    // lifecycle and the authoring picker already use. One predicate, so a server
    // validating a submitted target and a picker offering one cannot disagree.

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ICanonicalEntityCatalog _canonicalEntities;
    private readonly ILogger<MappingDefinitionService> _logger;

    // ── Constructor ───────────────────────────────────────────────────────────
    public MappingDefinitionService(
        IPlantProcessDbContext dbContext,
        ICanonicalEntityCatalog canonicalEntities,
        ILogger<MappingDefinitionService> logger)
    {
        _dbContext = dbContext;
        _canonicalEntities = canonicalEntities;
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

        if (!_canonicalEntities.IsProjectionTarget(command.TargetEntityName.Trim()))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation(
                    $"Target entity '{command.TargetEntityName}' is not a legal authoring output target. " +
                    $"Allowed values: {string.Join(", ", _canonicalEntities.ProjectionTargetNames())}."));

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




