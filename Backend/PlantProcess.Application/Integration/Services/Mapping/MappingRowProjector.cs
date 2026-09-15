using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Domain.Entities.Quality;

namespace PlantProcess.Application.Integration.Services.Mapping;

/// <summary>
/// PPIQ T-099. THE SHARED SINGLE-ROW PROJECTOR.
///
/// Everything that decides whether ONE staged row can lawfully become canonical
/// lives here, and exactly one copy of it exists: full execution, preview and
/// bounded reprocess all enter through ProcessOneRowAsync. A second validator
/// for preview would be a second product answer, and the two would drift.
///
/// NO EXCEPTION IS A CLASSIFICATION. Every expected refusal returns a typed
/// RowValidationRefusal; anything that throws out of this class is an
/// infrastructure fault and is meant to fail the operation rather than be
/// recorded as a property of the row.
/// </summary>
public sealed class MappingRowProjector : IMappingRowProjector
{
    private static readonly string[] SupportedTargetEntities =
    {
        "MaterialUnit",
        "MaterialAlias",
        "ProcessStepExecution",
        "ParameterObservation",
        "QualityEvent",
        "GenealogyEdge",
        "DefectCatalog",
        "ParameterDefinition"
    };

    private readonly IPlantProcessDbContext _dbContext;
    private readonly ProjectionRowValidationService _advancedValidation;

    public MappingRowProjector(IPlantProcessDbContext dbContext)
        : this(dbContext, new ProjectionRowValidationService(dbContext))
    {
    }

    public MappingRowProjector(
        IPlantProcessDbContext dbContext,
        ProjectionRowValidationService advancedValidation)
    {
        _dbContext = dbContext;
        _advancedValidation = advancedValidation;
    }

    // ------------------------------------------------------------------
    // DEFINITION ADMISSION. Answered before the first row, never as PVxx.
    // ------------------------------------------------------------------
    public ApplicationError? ValidateDefinition(MappingDefinition mapping, out IReadOnlyDictionary<string, string> fieldMap)
    {
        fieldMap = new Dictionary<string, string>();

        Dictionary<string, string>? map;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, string>>(mapping.MappingJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            return ApplicationError.Validation($"Mapping JSON is not valid field-map JSON: {ex.Message}");
        }

        if (map is null || map.Count == 0)
            return ApplicationError.Validation("Mapping JSON must contain at least one field mapping.");

        var target = mapping.TargetEntityName.Trim();
        if (!SupportedTargetEntities.Contains(target, StringComparer.Ordinal))
            return ApplicationError.Validation($"Target entity '{target}' is not supported by the projection engine.");

        fieldMap = map;
        return null;
    }

    // ------------------------------------------------------------------
    // ONE ROW.
    // ------------------------------------------------------------------
    public async Task<RowProjectionOutcome> ProcessOneRowAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        StagingRecord stagingRecord,
        MappingExecutionContext executionContext,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string?> sourceRow;
        try
        {
            using var doc = JsonDocument.Parse(stagingRecord.RawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return RowProjectionOutcome.Quarantined(new RowValidationRefusal(
                    ProjectionValidationCode.PV02,
                    "The staged payload is not a JSON object and cannot be read as a row.",
                    Truncate(stagingRecord.RawJson)));
            }

            sourceRow = FlattenObject(doc.RootElement);
        }
        catch (JsonException ex)
        {
            // A malformed SUPPLIED payload is the row's own fault, not the
            // system's, so it is typed evidence rather than an exception.
            return RowProjectionOutcome.Quarantined(new RowValidationRefusal(
                ProjectionValidationCode.PV02,
                $"The staged payload is not valid JSON: {ex.Message}",
                Truncate(stagingRecord.RawJson)));
        }

        var reader = new RowReader(fieldMap, sourceRow);
        var target = mapping.TargetEntityName.Trim();

        var outcome = target switch
        {
            "MaterialUnit" => await MapMaterialUnitAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "MaterialAlias" => await MapMaterialAliasAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "ProcessStepExecution" => await MapProcessStepExecutionAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "ParameterObservation" => await MapParameterObservationAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "QualityEvent" => await MapQualityEventAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "GenealogyEdge" => await MapGenealogyEdgeAsync(mapping, fieldMap, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "DefectCatalog" => await MapDefectCatalogAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),
            "ParameterDefinition" => await MapParameterDefinitionAsync(mapping, reader, stagingRecord, executionContext, previewOnly, cancellationToken),

            // Unreachable: ValidateDefinition refuses an unsupported target
            // before any row is read. Kept so the switch is total.
            _ => throw new InvalidOperationException($"Target entity '{target}' passed admission but has no projector.")
        };

        return outcome;
    }

    // ==================================================================
    // TARGET PROJECTORS
    // ==================================================================
    private async Task<RowProjectionOutcome> MapMaterialUnitAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialCode = reader.RequiredString("MaterialCode");
        var materialUnitType = reader.RequiredString("MaterialUnitType");
        var siteRaw = reader.OptionalString("SiteId") ?? reader.OptionalString("SiteCode");
        if (reader.Failed)
            return Refused(reader);

        var siteId = await ResolveSiteIdAsync(reader, siteRaw, cancellationToken);
        if (reader.Failed)
            return Refused(reader);

        // PV07 against the governed catalogue, never against a list written here.
        var typeRefusal = await ClassifyMaterialUnitTypeAsync(materialUnitType!, cancellationToken);
        if (typeRefusal is not null)
            return RowProjectionOutcome.Quarantined(typeRefusal);

        var duplicate = context.ClaimBusinessKey($"MaterialUnit|{siteId}|{materialCode}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("MaterialUnit", materialCode!, duplicate.Value));

        var existing = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.SiteId == siteId && x.MaterialCode == materialCode)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != Guid.Empty)
        {
            // Lawful idempotency preserved: the existing contract skips, and
            // turning that into PV04 would quarantine correct re-projection.
            if (!previewOnly)
                stagingRecord.MarkSkipped($"MaterialUnit already exists: {materialCode}.");

            return RowProjectionOutcome.Skipped(existing, "MaterialUnit", $"MaterialUnit already exists: {materialCode}.");
        }

        var productFamily = reader.OptionalString("ProductFamily");
        var gradeOrRecipe = reader.OptionalString("GradeOrRecipe");
        var sourceRecordId = reader.OptionalString("SourceRecordId");
        var startUtc = reader.OptionalDateTime("ProductionStartUtc");
        var endUtc = reader.OptionalDateTime("ProductionEndUtc");
        var offsetMinutes = reader.OptionalInt("PlantUtcOffsetMinutes");
        var timeZoneId = reader.OptionalString("PlantTimeZoneId");
        if (reader.Failed)
            return Refused(reader);

        var material = new MaterialUnit(
            materialCode: materialCode!,
            materialUnitType: materialUnitType!,
            siteId: siteId!.Value,
            productFamily: productFamily,
            gradeOrRecipe: gradeOrRecipe,
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId ?? sourceRecordId);

        if (startUtc.HasValue)
        {
            material.SetProductionWindow(
                startUtc.Value,
                endUtc,
                TimeSpan.FromMinutes(offsetMinutes ?? 0),
                timeZoneId ?? "UTC");
        }

        if (!previewOnly)
        {
            _dbContext.MaterialUnits.Add(material);
            stagingRecord.MarkMapped(material.Id, "MaterialUnit");
        }

        return RowProjectionOutcome.Mapped(material.Id, "MaterialUnit");
    }

    private async Task<RowProjectionOutcome> MapMaterialAliasAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(reader, cancellationToken);
        var aliasCode = reader.RequiredString("AliasCode");
        if (reader.Failed)
            return Refused(reader);

        var sourceSystem = reader.OptionalString("AliasSourceSystem")
            ?? reader.OptionalString("SourceSystem")
            ?? stagingRecord.SourceSystem
            ?? mapping.SourceSystem
            ?? "UnknownSource";
        var aliasType = reader.OptionalString("AliasType") ?? "SourceSystemId";
        if (reader.Failed)
            return Refused(reader);

        var duplicate = context.ClaimBusinessKey($"MaterialAlias|{materialId}|{aliasCode}|{sourceSystem}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("MaterialAlias", aliasCode!, duplicate.Value));

        var exists = await _dbContext.MaterialAliases
            .AsNoTracking()
            .AnyAsync(x => x.MaterialUnitId == materialId && x.AliasCode == aliasCode && x.SourceSystem == sourceSystem, cancellationToken);

        if (exists)
        {
            if (!previewOnly)
                stagingRecord.MarkSkipped($"MaterialAlias already exists: {aliasCode}.");

            return RowProjectionOutcome.Skipped(materialId, "MaterialAlias", $"MaterialAlias already exists: {aliasCode}.");
        }

        var alias = new MaterialAlias(materialId!.Value, aliasCode!, sourceSystem, aliasType, stagingRecord.IsSynthetic);
        if (!previewOnly)
        {
            _dbContext.MaterialAliases.Add(alias);
            stagingRecord.MarkMapped(alias.Id, "MaterialAlias");
        }

        return RowProjectionOutcome.Mapped(alias.Id, "MaterialAlias");
    }

    private async Task<RowProjectionOutcome> MapProcessStepExecutionAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(reader, cancellationToken);
        if (reader.Failed)
            return Refused(reader);

        var equipmentRaw = reader.OptionalString("EquipmentId") ?? reader.OptionalString("EquipmentCode");
        var equipmentId = await ResolveOptionalEquipmentIdAsync(reader, equipmentRaw, cancellationToken);
        var operationRaw = reader.OptionalString("OperationDefinitionId") ?? reader.OptionalString("OperationCode");
        var operationDefinitionId = await ResolveOptionalOperationDefinitionIdAsync(operationRaw, cancellationToken);
        var operationType = reader.OptionalString("OperationType") ?? reader.OptionalString("Operation") ?? reader.OptionalString("OperationCode") ?? "UnknownOperation";
        var startedAtUtc = reader.RequiredDateTime("StartedAtUtc");
        var endedAtUtc = reader.OptionalDateTime("EndedAtUtc");
        var operationCode = reader.OptionalString("OperationCode");
        var crewCode = reader.OptionalString("CrewCode");
        var executionStatus = reader.OptionalString("ExecutionStatus");
        var timeZoneId = reader.OptionalString("PlantTimeZoneId");
        var offsetMinutes = reader.OptionalInt("PlantUtcOffsetMinutes");
        if (reader.Failed)
            return Refused(reader);

        var duplicate = context.ClaimBusinessKey(
            $"ProcessStepExecution|{materialId}|{operationType}|{startedAtUtc!.Value:O}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("ProcessStepExecution", operationType, duplicate.Value));

        var step = new ProcessStepExecution(
            materialUnitId: materialId!.Value,
            operationType: operationType,
            startedAtUtc: startedAtUtc.Value,
            endedAtUtc: endedAtUtc,
            isSynthetic: stagingRecord.IsSynthetic,
            equipmentId: equipmentId,
            operationCode: operationCode,
            operationDefinitionId: operationDefinitionId,
            crewCode: crewCode,
            executionStatus: executionStatus,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            plantTimeZoneId: timeZoneId ?? "UTC",
            plantUtcOffsetMinutes: offsetMinutes ?? 0);

        if (!previewOnly)
        {
            _dbContext.ProcessStepExecutions.Add(step);
            stagingRecord.MarkMapped(step.Id, "ProcessStepExecution");
        }

        return RowProjectionOutcome.Mapped(step.Id, "ProcessStepExecution");
    }

    private async Task<RowProjectionOutcome> MapParameterObservationAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(reader, cancellationToken);
        if (reader.Failed)
            return Refused(reader);

        var parameterDefinitionId = await ResolveParameterDefinitionIdAsync(reader, cancellationToken);
        if (reader.Failed)
            return Refused(reader);

        var equipmentRaw = reader.OptionalString("EquipmentId") ?? reader.OptionalString("EquipmentCode");
        var equipmentId = await ResolveOptionalEquipmentIdAsync(reader, equipmentRaw, cancellationToken);
        var stepId = reader.OptionalGuid("ProcessStepExecutionId");
        var observedAtUtc = reader.RequiredDateTime("ObservedAtUtc");
        var numericValue = reader.OptionalDecimal("NumericValue");
        var textValue = reader.OptionalString("TextValue");
        var booleanValue = reader.OptionalBool("BooleanValue");
        var unitOfMeasure = reader.OptionalString("UnitOfMeasure");
        var qualityFlag = reader.OptionalString("QualityFlag");
        var rawValue = reader.OptionalString("RawValue");
        var timeZoneId = reader.OptionalString("PlantTimeZoneId");
        var offsetMinutes = reader.OptionalInt("PlantUtcOffsetMinutes");
        if (reader.Failed)
            return Refused(reader);

        // PV08 against the governed parameter definition, never a unit list here.
        var unitRefusal = await ClassifyUnitOfMeasureAsync(parameterDefinitionId!.Value, unitOfMeasure, cancellationToken);
        if (unitRefusal is not null)
            return RowProjectionOutcome.Quarantined(unitRefusal);

        var rangeRefusal = await _advancedValidation.ValidateParameterObservationRangeAsync(
            materialId!.Value,
            parameterDefinitionId.Value,
            observedAtUtc!.Value,
            numericValue,
            cancellationToken);

        if (rangeRefusal is not null)
            return RowProjectionOutcome.Quarantined(rangeRefusal);

        var duplicate = context.ClaimBusinessKey(
            $"ParameterObservation|{materialId}|{parameterDefinitionId}|{observedAtUtc!.Value:O}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("ParameterObservation", observedAtUtc.Value.ToString("O"), duplicate.Value));

        var observation = new ParameterObservation(
            materialUnitId: materialId!.Value,
            parameterDefinitionId: parameterDefinitionId.Value,
            observedAtUtc: observedAtUtc.Value,
            isSynthetic: stagingRecord.IsSynthetic,
            numericValue: numericValue,
            textValue: textValue,
            booleanValue: booleanValue,
            unitOfMeasure: unitOfMeasure,
            processStepExecutionId: stepId,
            equipmentId: equipmentId,
            qualityFlag: qualityFlag,
            rawValue: rawValue,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            plantTimeZoneId: timeZoneId ?? "UTC",
            plantUtcOffsetMinutes: offsetMinutes ?? 0);

        if (!previewOnly)
        {
            _dbContext.ParameterObservations.Add(observation);
            stagingRecord.MarkMapped(observation.Id, "ParameterObservation");
        }

        return RowProjectionOutcome.Mapped(observation.Id, "ParameterObservation");
    }

    private async Task<RowProjectionOutcome> MapQualityEventAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(reader, cancellationToken);
        if (reader.Failed)
            return Refused(reader);

        var defectRaw = reader.OptionalString("DefectCatalogId") ?? reader.OptionalString("DefectCode");
        var defectCatalogId = await ResolveOptionalDefectCatalogIdAsync(reader, defectRaw, cancellationToken);
        var eventType = reader.RequiredString("EventType");
        var eventAtUtc = reader.RequiredDateTime("EventAtUtc");
        var severity = reader.OptionalString("Severity");
        var decision = reader.OptionalString("Decision");
        var description = reader.OptionalString("Description");
        var timeZoneId = reader.OptionalString("PlantTimeZoneId");
        var offsetMinutes = reader.OptionalInt("PlantUtcOffsetMinutes");
        if (reader.Failed)
            return Refused(reader);

        var duplicate = context.ClaimBusinessKey(
            $"QualityEvent|{materialId}|{eventType}|{eventAtUtc!.Value:O}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("QualityEvent", eventType!, duplicate.Value));

        var qualityEvent = new QualityEvent(
            materialUnitId: materialId!.Value,
            eventType: eventType!,
            eventAtUtc: eventAtUtc.Value,
            isSynthetic: stagingRecord.IsSynthetic,
            defectCatalogId: defectCatalogId,
            severity: severity,
            decision: decision,
            description: description,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            plantTimeZoneId: timeZoneId ?? "UTC",
            plantUtcOffsetMinutes: offsetMinutes ?? 0);

        if (!previewOnly)
        {
            _dbContext.QualityEvents.Add(qualityEvent);
            stagingRecord.MarkMapped(qualityEvent.Id, "QualityEvent");
        }

        return RowProjectionOutcome.Mapped(qualityEvent.Id, "QualityEvent");
    }

    private async Task<RowProjectionOutcome> MapGenealogyEdgeAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var parentId = await ResolveGenealogyMaterialIdAsync(
            reader,
            cancellationToken,
            "ParentMaterialUnitId",
            "ParentMaterialCode",
            "ParentAliasCode");

        if (reader.Failed)
            return Refused(reader);

        var childId = await ResolveGenealogyMaterialIdAsync(
            reader,
            cancellationToken,
            "ChildMaterialUnitId",
            "ChildMaterialCode",
            "ChildAliasCode");

        if (reader.Failed)
            return Refused(reader);

        var relationshipType = reader.RequiredString("RelationshipType");
        var relationshipCode = reader.OptionalString("RelationshipCode");
        var contributionWeight = reader.OptionalDecimal("ContributionWeight") ?? 1m;
        var isTransition = reader.OptionalBool("IsTransition") ?? false;
        var provenanceConfidence = reader.OptionalDecimal("ProvenanceConfidence") ?? 1m;
        var effectiveFrom = reader.OptionalDateTime("EffectiveFromUtc");
        var effectiveTo = reader.OptionalDateTime("EffectiveToUtc");

        if (reader.Failed)
            return Refused(reader);

        var duplicate = context.ClaimBusinessKey(
            $"GenealogyEdge|{parentId}|{childId}|{relationshipType}",
            stagingRecord.RowNumber);

        if (duplicate.HasValue)
        {
            return RowProjectionOutcome.Quarantined(
                DuplicateInExecution(
                    "GenealogyEdge",
                    relationshipType!,
                    duplicate.Value));
        }

        var alreadyPersisted = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.ParentMaterialUnitId == parentId &&
                    x.ChildMaterialUnitId == childId &&
                    x.RelationshipType == relationshipType,
                cancellationToken);

        if (alreadyPersisted)
        {
            return RowProjectionOutcome.Quarantined(
                new RowValidationRefusal(
                    ProjectionValidationCode.PV04,
                    "A GenealogyEdge with this parent, child and relationship type already exists in canonical truth.",
                    relationshipType));
        }

        var advancedRefusal =
            await _advancedValidation.ValidateGenealogyAsync(
                mapping,
                fieldMap,
                stagingRecord,
                context.TenantId,
                parentId!.Value,
                childId!.Value,
                relationshipType!,
                relationshipCode,
                contributionWeight,
                isTransition,
                cancellationToken);

        if (advancedRefusal is not null)
            return RowProjectionOutcome.Quarantined(advancedRefusal);

        var edge = new GenealogyEdge(
            parentMaterialUnitId: parentId.Value,
            childMaterialUnitId: childId.Value,
            relationshipType: relationshipType!,
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            contributionWeight: contributionWeight,
            isTransition: isTransition,
            provenanceConfidence: provenanceConfidence);

        edge.SetEffectiveWindow(effectiveFrom, effectiveTo);

        if (!previewOnly)
        {
            _dbContext.GenealogyEdges.Add(edge);
            stagingRecord.MarkMapped(edge.Id, "GenealogyEdge");
        }

        return RowProjectionOutcome.Mapped(edge.Id, "GenealogyEdge");
    }

    private async Task<RowProjectionOutcome> MapDefectCatalogAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var defectCode = reader.RequiredString("DefectCode");
        if (reader.Failed)
            return Refused(reader);

        var defectName = reader.OptionalString("DefectName") ?? defectCode;
        var defectCategory = reader.OptionalString("DefectCategory");
        var industryTemplate = reader.OptionalString("IndustryTemplate");
        if (reader.Failed)
            return Refused(reader);

        var duplicate = context.ClaimBusinessKey($"DefectCatalog|{defectCode}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("DefectCatalog", defectCode!, duplicate.Value));

        var existingId = await _dbContext.DefectCatalogs
            .AsNoTracking()
            .Where(x => x.DefectCode == defectCode)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId != Guid.Empty)
        {
            // Lawful idempotency: the code-keyed contract already maps this
            // business key to the existing canonical identity.
            if (!previewOnly)
                stagingRecord.MarkMapped(existingId, "DefectCatalog");

            return RowProjectionOutcome.Mapped(existingId, "DefectCatalog");
        }

        var defect = new DefectCatalog(
            defectCode: defectCode!,
            defectName: defectName!,
            defectCategory: defectCategory,
            industryTemplate: industryTemplate,
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId);

        if (!previewOnly)
        {
            _dbContext.DefectCatalogs.Add(defect);
            stagingRecord.MarkMapped(defect.Id, "DefectCatalog");
        }

        return RowProjectionOutcome.Mapped(defect.Id, "DefectCatalog");
    }

    private async Task<RowProjectionOutcome> MapParameterDefinitionAsync(
        MappingDefinition mapping,
        RowReader reader,
        StagingRecord stagingRecord,
        MappingExecutionContext context,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var parameterCode = reader.RequiredString("ParameterCode");
        if (reader.Failed)
            return Refused(reader);

        var parameterName = reader.OptionalString("ParameterName") ?? parameterCode;
        var valueType = reader.OptionalString("ValueType") ?? "Numeric";
        var unitOfMeasure = reader.OptionalString("UnitOfMeasure");
        var parameterCategory = reader.OptionalString("ParameterCategory");
        var industryTemplate = reader.OptionalString("IndustryTemplate");
        if (reader.Failed)
            return Refused(reader);

        var duplicate = context.ClaimBusinessKey($"ParameterDefinition|{parameterCode}", stagingRecord.RowNumber);
        if (duplicate.HasValue)
            return RowProjectionOutcome.Quarantined(DuplicateInExecution("ParameterDefinition", parameterCode!, duplicate.Value));

        var existingId = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x => x.ParameterCode == parameterCode)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId != Guid.Empty)
        {
            if (!previewOnly)
                stagingRecord.MarkMapped(existingId, "ParameterDefinition");

            return RowProjectionOutcome.Mapped(existingId, "ParameterDefinition");
        }

        var definition = new ParameterDefinition(
            parameterCode: parameterCode!,
            parameterName: parameterName!,
            valueType: valueType,
            unitOfMeasure: unitOfMeasure,
            parameterCategory: parameterCategory,
            industryTemplate: industryTemplate,
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId);

        if (!previewOnly)
        {
            _dbContext.ParameterDefinitions.Add(definition);
            stagingRecord.MarkMapped(definition.Id, "ParameterDefinition");
        }

        return RowProjectionOutcome.Mapped(definition.Id, "ParameterDefinition");
    }

    // ==================================================================
    // GOVERNED CLASSIFIERS
    // ==================================================================

    /// <summary>
    /// PV07. The catalogue decides, not this file. When the catalogue carries no
    /// active entry the product has not yet governed this vocabulary, and
    /// refusing every row against an empty catalogue would be a false refusal.
    /// </summary>
    private async Task<RowValidationRefusal?> ClassifyMaterialUnitTypeAsync(string materialUnitType, CancellationToken cancellationToken)
    {
        var governed = await _dbContext.MaterialUnitTypeDefinitions
            .AsNoTracking()
            .AnyAsync(x => x.IsActive, cancellationToken);

        if (!governed)
            return null;

        var registered = await _dbContext.MaterialUnitTypeDefinitions
            .AsNoTracking()
            .AnyAsync(x => x.IsActive && x.MaterialUnitTypeCode == materialUnitType, cancellationToken);

        if (registered)
            return null;

        return new RowValidationRefusal(
            ProjectionValidationCode.PV07,
            $"Material unit type '{materialUnitType}' is not registered in the governed material unit type catalogue.",
            materialUnitType);
    }

    /// <summary>
    /// PV08. The governed parameter definition is the unit authority. A
    /// definition that declares no unit governs nothing, so a supplied unit is
    /// lawful there.
    /// </summary>
    private async Task<RowValidationRefusal?> ClassifyUnitOfMeasureAsync(
        Guid parameterDefinitionId,
        string? suppliedUnit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(suppliedUnit))
            return null;

        var governedUnit = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x => x.Id == parameterDefinitionId)
            .Select(x => x.UnitOfMeasure)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(governedUnit))
            return null;

        if (string.Equals(governedUnit, suppliedUnit, StringComparison.OrdinalIgnoreCase))
            return null;

        return new RowValidationRefusal(
            ProjectionValidationCode.PV08,
            $"Unit '{suppliedUnit}' is not the governed unit '{governedUnit}' for this parameter definition.",
            suppliedUnit);
    }

    private static RowValidationRefusal DuplicateInExecution(string entityName, string key, int ownerRowNumber) =>
        new(ProjectionValidationCode.PV05,
            $"Row {ownerRowNumber} in this execution already declares the same governed {entityName} business key.",
            key);

    // ==================================================================
    // GOVERNED REFERENCE RESOLUTION. Unresolvable SUPPLIED reference is PV06.
    // ==================================================================
    private async Task<Guid?> ResolveGenealogyMaterialIdAsync(
        RowReader reader,
        CancellationToken cancellationToken,
        string materialIdField,
        string materialCodeField,
        string aliasCodeField)
    {
        if (reader.Failed)
            return null;

        var materialId = reader.OptionalGuid(materialIdField);

        if (reader.Failed)
            return null;

        if (materialId.HasValue)
        {
            var exists =
                await _dbContext.MaterialUnits.AnyAsync(
                    x => x.Id == materialId.Value,
                    cancellationToken);

            if (exists)
                return materialId.Value;

            reader.Refuse(
                ProjectionValidationCode.PV15,
                $"Referenced material '{materialId.Value}' is not canonical yet and may arrive in another batch.",
                materialId.Value.ToString());

            return null;
        }

        var materialCode = reader.OptionalString(materialCodeField);

        if (!string.IsNullOrWhiteSpace(materialCode))
        {
            var byCode = await _dbContext.MaterialUnits
                .AsNoTracking()
                .Where(x => x.MaterialCode == materialCode)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (byCode != Guid.Empty)
                return byCode;
        }

        var aliasCode = reader.OptionalString(aliasCodeField);

        if (!string.IsNullOrWhiteSpace(aliasCode))
        {
            var byAlias = await _dbContext.MaterialAliases
                .AsNoTracking()
                .Where(x => x.AliasCode == aliasCode)
                .Select(x => x.MaterialUnitId)
                .FirstOrDefaultAsync(cancellationToken);

            if (byAlias != Guid.Empty)
                return byAlias;
        }

        if (string.IsNullOrWhiteSpace(materialCode) &&
            string.IsNullOrWhiteSpace(aliasCode))
        {
            reader.Refuse(
                ProjectionValidationCode.PV01,
                $"No material identity was supplied through {materialIdField}, {materialCodeField} or {aliasCodeField}.",
                null);

            return null;
        }

        reader.Refuse(
            ProjectionValidationCode.PV15,
            "Referenced material is not canonical yet and may arrive in another batch.",
            materialCode ?? aliasCode);

        return null;
    }

    private async Task<Guid?> ResolveMaterialIdAsync(
        RowReader reader,
        CancellationToken cancellationToken,
        string materialIdField = "MaterialUnitId",
        string materialCodeField = "MaterialCode",
        string aliasCodeField = "AliasCode")
    {
        if (reader.Failed)
            return null;

        var materialId = reader.OptionalGuid(materialIdField);
        if (reader.Failed)
            return null;

        if (materialId.HasValue)
        {
            var exists = await _dbContext.MaterialUnits.AnyAsync(x => x.Id == materialId.Value, cancellationToken);
            if (!exists)
            {
                reader.Refuse(ProjectionValidationCode.PV06,
                    $"MaterialUnit '{materialId.Value}' does not exist.", materialId.Value.ToString());
                return null;
            }
            return materialId.Value;
        }

        var materialCode = reader.OptionalString(materialCodeField);
        if (!string.IsNullOrWhiteSpace(materialCode))
        {
            var id = await _dbContext.MaterialUnits
                .AsNoTracking()
                .Where(x => x.MaterialCode == materialCode)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (id != Guid.Empty)
                return id;
        }

        var aliasCode = reader.OptionalString(aliasCodeField);
        if (!string.IsNullOrWhiteSpace(aliasCode))
        {
            var id = await _dbContext.MaterialAliases
                .AsNoTracking()
                .Where(x => x.AliasCode == aliasCode)
                .Select(x => x.MaterialUnitId)
                .FirstOrDefaultAsync(cancellationToken);

            if (id != Guid.Empty)
                return id;
        }

        // Nothing supplied at all is a missing declared field, not an
        // unresolvable reference: PV01 and PV06 are different questions.
        if (string.IsNullOrWhiteSpace(materialCode) && string.IsNullOrWhiteSpace(aliasCode))
        {
            reader.Refuse(ProjectionValidationCode.PV01,
                $"No material identity was supplied through {materialIdField}, {materialCodeField} or {aliasCodeField}.",
                null);
            return null;
        }

        reader.Refuse(ProjectionValidationCode.PV06,
            $"Could not resolve material using {materialIdField}, {materialCodeField} or {aliasCodeField}.",
            materialCode ?? aliasCode);
        return null;
    }

    private async Task<Guid?> ResolveSiteIdAsync(RowReader reader, string? value, CancellationToken cancellationToken)
    {
        if (reader.Failed)
            return null;

        if (string.IsNullOrWhiteSpace(value))
        {
            reader.Refuse(ProjectionValidationCode.PV01, "SiteId or SiteCode is required for MaterialUnit mapping.", null);
            return null;
        }

        if (Guid.TryParse(value, out var siteId))
        {
            var exists = await _dbContext.Sites.AnyAsync(x => x.Id == siteId, cancellationToken);
            if (exists)
                return siteId;
        }

        var byCode = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.SiteCode == value)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (byCode == Guid.Empty)
        {
            reader.Refuse(ProjectionValidationCode.PV06, $"Site '{value}' could not be resolved by ID or SiteCode.", value);
            return null;
        }

        return byCode;
    }

    private async Task<Guid?> ResolveOptionalEquipmentIdAsync(RowReader reader, string? value, CancellationToken cancellationToken)
    {
        if (reader.Failed || string.IsNullOrWhiteSpace(value))
            return null;

        if (Guid.TryParse(value, out var id))
        {
            var exists = await _dbContext.Equipment.AnyAsync(x => x.Id == id, cancellationToken);
            if (exists)
                return id;
        }

        var byCode = await _dbContext.Equipment
            .AsNoTracking()
            .Where(x => x.EquipmentCode == value)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (byCode == Guid.Empty)
        {
            // Supplied but unresolvable. An OMITTED optional reference never
            // reaches this line and is never PV06.
            reader.Refuse(ProjectionValidationCode.PV06, $"Equipment '{value}' could not be resolved.", value);
            return null;
        }

        return byCode;
    }

    private async Task<Guid?> ResolveOptionalOperationDefinitionIdAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Guid.TryParse(value, out var id))
        {
            var exists = await _dbContext.OperationDefinitions.AnyAsync(x => x.Id == id, cancellationToken);
            if (exists)
                return id;
        }

        var byCode = await _dbContext.OperationDefinitions
            .AsNoTracking()
            .Where(x => x.OperationCode == value)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return byCode == Guid.Empty ? null : byCode;
    }

    private async Task<Guid?> ResolveParameterDefinitionIdAsync(RowReader reader, CancellationToken cancellationToken)
    {
        if (reader.Failed)
            return null;

        var direct = reader.OptionalGuid("ParameterDefinitionId");
        if (reader.Failed)
            return null;

        if (direct.HasValue)
        {
            var exists = await _dbContext.ParameterDefinitions.AnyAsync(x => x.Id == direct.Value, cancellationToken);
            if (exists)
                return direct.Value;
        }

        var code = reader.RequiredString("ParameterCode");
        if (reader.Failed)
            return null;

        var id = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x => x.ParameterCode == code)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (id == Guid.Empty)
        {
            reader.Refuse(ProjectionValidationCode.PV06, $"ParameterDefinition '{code}' does not exist.", code);
            return null;
        }

        return id;
    }

    private async Task<Guid?> ResolveOptionalDefectCatalogIdAsync(RowReader reader, string? value, CancellationToken cancellationToken)
    {
        if (reader.Failed || string.IsNullOrWhiteSpace(value))
            return null;

        if (Guid.TryParse(value, out var id))
        {
            var exists = await _dbContext.DefectCatalogs.AnyAsync(x => x.Id == id, cancellationToken);
            if (exists)
                return id;
        }

        var byCode = await _dbContext.DefectCatalogs
            .AsNoTracking()
            .Where(x => x.DefectCode == value)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (byCode == Guid.Empty)
        {
            reader.Refuse(ProjectionValidationCode.PV06, $"Defect '{value}' could not be resolved in the governed catalogue.", value);
            return null;
        }

        return byCode;
    }

    private static RowProjectionOutcome Refused(RowReader reader) =>
        RowProjectionOutcome.Quarantined(reader.Refusal!);

    private static string? Truncate(string? value) =>
        value is null || value.Length <= 400 ? value : value.Substring(0, 400);

    private static Dictionary<string, string?> FlattenObject(JsonElement obj)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.Value.GetRawText()
            };
        }
        return result;
    }

    // ==================================================================
    // TYPED FIELD READING. FIRST REFUSAL WINS AND SHORT-CIRCUITS THE ROW.
    // ==================================================================
    private sealed class RowReader
    {
        private readonly IReadOnlyDictionary<string, string> _fieldMap;
        private readonly IReadOnlyDictionary<string, string?> _sourceRow;

        public RowReader(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow)
        {
            _fieldMap = fieldMap;
            _sourceRow = sourceRow;
        }

        public RowValidationRefusal? Refusal { get; private set; }

        public bool Failed => Refusal is not null;

        public void Refuse(ProjectionValidationCode code, string detail, string? offendingValue)
        {
            Refusal ??= new RowValidationRefusal(code, detail, offendingValue);
        }

        public FieldRead<string> ReadRaw(string targetField)
        {
            if (!_fieldMap.TryGetValue(targetField, out var sourceField))
                return FieldRead<string>.NotMapped();

            if (!string.IsNullOrEmpty(sourceField) && sourceField.StartsWith("const:", StringComparison.Ordinal))
            {
                var constant = sourceField.Substring("const:".Length).Trim();
                return string.IsNullOrWhiteSpace(constant) ? FieldRead<string>.Empty() : FieldRead<string>.Ok(constant);
            }

            if (!_sourceRow.TryGetValue(sourceField, out var value))
                return FieldRead<string>.Absent();

            return string.IsNullOrWhiteSpace(value) ? FieldRead<string>.Empty() : FieldRead<string>.Ok(value.Trim());
        }

        public string? RequiredString(string targetField)
        {
            if (Failed)
                return null;

            var read = ReadRaw(targetField);
            switch (read.State)
            {
                case FieldReadState.PresentValid:
                    return read.Value;

                case FieldReadState.NotMapped:
                    Refuse(ProjectionValidationCode.PV01,
                        $"Required field '{targetField}' is not declared by the mapping.", null);
                    return null;

                case FieldReadState.MappedButSourceAbsent:
                    Refuse(ProjectionValidationCode.PV01,
                        $"Declared source field for '{targetField}' is absent from the staged payload.", null);
                    return null;

                default:
                    Refuse(ProjectionValidationCode.PV03,
                        $"Required field '{targetField}' is present but empty.", null);
                    return null;
            }
        }

        /// <summary>An omitted optional field is lawful null. A SUPPLIED one is read.</summary>
        public string? OptionalString(string targetField)
        {
            if (Failed)
                return null;

            var read = ReadRaw(targetField);
            return read.State == FieldReadState.PresentValid ? read.Value : null;
        }

        public Guid? OptionalGuid(string targetField) =>
            ParseOptional(targetField, raw => Guid.TryParse(raw, out var v) ? v : (Guid?)null, "GUID");

        public int? OptionalInt(string targetField) =>
            ParseOptional(targetField, raw => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null, "integer");

        public decimal? OptionalDecimal(string targetField) =>
            ParseOptional(targetField, raw => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null, "decimal");

        public bool? OptionalBool(string targetField) =>
            ParseOptional(targetField, raw => bool.TryParse(raw, out var v) ? v : (bool?)null, "boolean");

        public DateTime? OptionalDateTime(string targetField) =>
            ParseOptional(targetField, ParseDate, "DateTime");

        public DateTime? RequiredDateTime(string targetField)
        {
            if (Failed)
                return null;

            var raw = RequiredString(targetField);
            if (Failed || raw is null)
                return null;

            var parsed = ParseDate(raw);
            if (parsed.HasValue)
                return parsed;

            Refuse(ProjectionValidationCode.PV02,
                $"Field '{targetField}' value cannot be read as a DateTime.", raw);
            return null;
        }

        /// <summary>
        /// A SUPPLIED value that cannot become the target type is PV02 and is
        /// never converted to a silent null. That silent null is precisely how a
        /// malformed number used to enter canonical truth unnoticed.
        /// </summary>
        private T? ParseOptional<T>(string targetField, Func<string, T?> parse, string typeName) where T : struct
        {
            if (Failed)
                return null;

            var read = ReadRaw(targetField);
            if (read.State != FieldReadState.PresentValid)
                return null;

            var parsed = parse(read.Value!);
            if (parsed.HasValue)
                return parsed;

            Refuse(ProjectionValidationCode.PV02,
                $"Field '{targetField}' value cannot be read as {typeName}.", read.Value);
            return null;
        }
    }

    private static readonly string[] SupportedPlantDateTimeFormats =
    {
        "O",
        "o",
        "yyyy-MM-ddTHH:mm:ss.fffffffK",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "dd/MM/yyyy HH:mm:ss.fff",
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy",
        "d/M/yyyy HH:mm:ss",
        "d/M/yyyy",
        "MM/dd/yyyy HH:mm:ss.fff",
        "MM/dd/yyyy HH:mm:ss",
        "MM/dd/yyyy",
        "M/d/yyyy HH:mm:ss",
        "M/d/yyyy",
        "dd.MM.yyyy HH:mm:ss",
        "dd.MM.yyyy"
    };

    private static DateTime? ParseDate(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var value = rawValue.Trim();

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            if (longValue > 10_000_000_000)
                return DateTimeOffset.FromUnixTimeMilliseconds(longValue).UtcDateTime;

            if (longValue > 1_000_000_000)
                return DateTimeOffset.FromUnixTimeSeconds(longValue).UtcDateTime;

            if (longValue > 20_000 && longValue < 80_000)
                return DateTime.SpecifyKind(DateTime.FromOADate(longValue), DateTimeKind.Utc);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue) &&
            doubleValue > 20_000 &&
            doubleValue < 80_000)
        {
            return DateTime.SpecifyKind(DateTime.FromOADate(doubleValue), DateTimeKind.Utc);
        }

        if (DateTimeOffset.TryParseExact(
                value,
                SupportedPlantDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exactOffset))
        {
            return exactOffset.UtcDateTime;
        }

        if (DateTime.TryParseExact(
                value,
                SupportedPlantDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exactDate))
        {
            return EnsureUtc(exactDate);
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedOffset))
        {
            return parsedOffset.UtcDateTime;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedDate))
        {
            return EnsureUtc(parsedDate);
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();

        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
