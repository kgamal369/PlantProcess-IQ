using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Domain.Entities.Quality;

namespace PlantProcess.Application.Integration.Services.Mapping;

public sealed class MappingExecutionService : IMappingExecutionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<MappingExecutionService> _logger;

    public MappingExecutionService(
        IPlantProcessDbContext dbContext,
        ILogger<MappingExecutionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<ApplicationResult<MappingExecutionResult>> PreviewAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            mappingDefinitionId,
            importBatchId,
            take,
            previewOnly: true,
            stopOnFirstError: false,
            cancellationToken);
    }

    public Task<ApplicationResult<MappingExecutionResult>> ExecuteAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        bool stopOnFirstError,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            mappingDefinitionId,
            importBatchId,
            take,
            previewOnly: false,
            stopOnFirstError,
            cancellationToken);
    }

    private async Task<ApplicationResult<MappingExecutionResult>> RunAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        bool previewOnly,
        bool stopOnFirstError,
        CancellationToken cancellationToken)
    {
        if (mappingDefinitionId == Guid.Empty)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.Validation("Mapping definition ID is required."));

        if (importBatchId == Guid.Empty)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.Validation("Import batch ID is required."));

        var maxRows = Math.Clamp(take <= 0 ? 500 : take, 1, 5000);

        var mapping = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == mappingDefinitionId, cancellationToken);

        if (mapping is null)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.NotFound("Mapping definition does not exist."));

        if (!mapping.IsActive)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.BusinessRule("Mapping definition is inactive."));

        var batchExists = await _dbContext.ImportBatches
            .AnyAsync(x => x.Id == importBatchId, cancellationToken);

        if (!batchExists)
            return ApplicationResult<MappingExecutionResult>.Failure(ApplicationError.NotFound("Import batch does not exist."));

        var fieldMapResult = ParseFieldMap(mapping.MappingJson);
        if (fieldMapResult.Error is not null)
            return ApplicationResult<MappingExecutionResult>.Failure(fieldMapResult.Error);

        var fieldMap = fieldMapResult.Map;

        var rows = await _dbContext.StagingRecords
            .Where(x =>
                x.ImportBatchId == importBatchId &&
                x.SourceObjectName == mapping.SourceObjectName &&
                (!x.IsProcessed || x.ProcessingStatus == "Pending"))
            .OrderBy(x => x.RowNumber)
            .Take(maxRows)
            .ToListAsync(cancellationToken);

        var rowResults = new List<MappingExecutionRowResult>();
        var mapped = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var stagingRecord in rows)
        {
            try
            {
                using var doc = JsonDocument.Parse(stagingRecord.RawJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("RawJson root must be a JSON object.");

                var sourceRow = FlattenObject(doc.RootElement);
                var result = await MapOneRowAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken);

                if (result.Status == "Mapped")
                    mapped++;
                else if (result.Status == "Skipped")
                    skipped++;
                else if (result.Status == "Failed")
                    failed++;

                rowResults.Add(result);

                if (!previewOnly && result.Status == "Failed" && stopOnFirstError)
                    break;
            }
            catch (Exception ex)
            {
                failed++;
                var message = ex.Message;

                if (!previewOnly)
                    stagingRecord.MarkFailed(message);

                rowResults.Add(new MappingExecutionRowResult(
                    stagingRecord.Id,
                    stagingRecord.RowNumber,
                    "Failed",
                    null,
                    null,
                    message));

                _logger.LogWarning(
                    ex,
                    "Mapping row failed. MappingDefinitionId={MappingDefinitionId}, ImportBatchId={ImportBatchId}, StagingRecordId={StagingRecordId}, RowNumber={RowNumber}",
                    mappingDefinitionId,
                    importBatchId,
                    stagingRecord.Id,
                    stagingRecord.RowNumber);

                if (!previewOnly && stopOnFirstError)
                    break;
            }
        }

        if (!previewOnly)
            await _dbContext.SaveChangesAsync(cancellationToken);

        var output = new MappingExecutionResult(
            mapping.Id,
            importBatchId,
            mapping.MappingCode,
            mapping.TargetEntityName,
            previewOnly,
            maxRows,
            rowResults.Count,
            mapped,
            skipped,
            failed,
            rowResults);

        _logger.LogInformation(
            "Mapping execution finished. MappingDefinitionId={MappingDefinitionId}, ImportBatchId={ImportBatchId}, PreviewOnly={PreviewOnly}, Processed={Processed}, Mapped={Mapped}, Skipped={Skipped}, Failed={Failed}",
            mapping.Id,
            importBatchId,
            previewOnly,
            output.ProcessedRows,
            output.MappedRows,
            output.SkippedRows,
            output.FailedRows);

        return ApplicationResult<MappingExecutionResult>.Success(output);
    }

    private async Task<MappingExecutionRowResult> MapOneRowAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var target = mapping.TargetEntityName.Trim();

        return target switch
        {
            "MaterialUnit" => await MapMaterialUnitAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken),
            "MaterialAlias" => await MapMaterialAliasAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken),
            "ProcessStepExecution" => await MapProcessStepExecutionAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken),
            "ParameterObservation" => await MapParameterObservationAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken),
            "QualityEvent" => await MapQualityEventAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken),
            "GenealogyEdge" => await MapGenealogyEdgeAsync(mapping, fieldMap, sourceRow, stagingRecord, previewOnly, cancellationToken),
            _ => FailOrThrow(stagingRecord, previewOnly, $"Target entity '{target}' is not supported by MappingExecutionService yet.")
        };
    }

    private async Task<MappingExecutionRowResult> MapMaterialUnitAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialCode = Required(fieldMap, sourceRow, "MaterialCode");
        var materialUnitType = Required(fieldMap, sourceRow, "MaterialUnitType");
        var siteId = await ResolveSiteIdAsync(Optional(fieldMap, sourceRow, "SiteId") ?? Optional(fieldMap, sourceRow, "SiteCode"), cancellationToken);

        var existing = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.SiteId == siteId && x.MaterialCode == materialCode)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != Guid.Empty)
        {
            if (!previewOnly)
                stagingRecord.MarkSkipped($"MaterialUnit already exists: {materialCode}.");

            return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Skipped", existing, "MaterialUnit", $"MaterialUnit already exists: {materialCode}.");
        }

        var material = new MaterialUnit(
            materialCode: materialCode,
            materialUnitType: materialUnitType,
            siteId: siteId,
            productFamily: Optional(fieldMap, sourceRow, "ProductFamily"),
            gradeOrRecipe: Optional(fieldMap, sourceRow, "GradeOrRecipe"),
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId ?? Optional(fieldMap, sourceRow, "SourceRecordId"));

        var startUtc = OptionalDateTime(fieldMap, sourceRow, "ProductionStartUtc");
        var endUtc = OptionalDateTime(fieldMap, sourceRow, "ProductionEndUtc");
        if (startUtc.HasValue)
        {
            material.SetProductionWindow(
                startUtc.Value,
                endUtc,
                TimeSpan.FromMinutes(OptionalInt(fieldMap, sourceRow, "PlantUtcOffsetMinutes") ?? 0),
                Optional(fieldMap, sourceRow, "PlantTimeZoneId") ?? "UTC");
        }

        if (!previewOnly)
        {
            _dbContext.MaterialUnits.Add(material);
            stagingRecord.MarkMapped(material.Id, "MaterialUnit");
        }

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", material.Id, "MaterialUnit", null);
    }

    private async Task<MappingExecutionRowResult> MapMaterialAliasAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(fieldMap, sourceRow, cancellationToken);
        var aliasCode = Required(fieldMap, sourceRow, "AliasCode");
        var sourceSystem = RequiredAny(fieldMap, sourceRow, "AliasSourceSystem", "SourceSystem")
            ?? stagingRecord.SourceSystem
            ?? mapping.SourceSystem
            ?? "UnknownSource";
        var aliasType = Optional(fieldMap, sourceRow, "AliasType") ?? "SourceSystemId";

        var exists = await _dbContext.MaterialAliases
            .AsNoTracking()
            .AnyAsync(x => x.MaterialUnitId == materialId && x.AliasCode == aliasCode && x.SourceSystem == sourceSystem, cancellationToken);

        if (exists)
        {
            if (!previewOnly)
                stagingRecord.MarkSkipped($"MaterialAlias already exists: {aliasCode}.");

            return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Skipped", materialId, "MaterialAlias", $"MaterialAlias already exists: {aliasCode}.");
        }

        var alias = new MaterialAlias(materialId, aliasCode, sourceSystem, aliasType, stagingRecord.IsSynthetic);
        if (!previewOnly)
        {
            _dbContext.MaterialAliases.Add(alias);
            stagingRecord.MarkMapped(alias.Id, "MaterialAlias");
        }

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", alias.Id, "MaterialAlias", null);
    }

    private async Task<MappingExecutionRowResult> MapProcessStepExecutionAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(fieldMap, sourceRow, cancellationToken);
        var equipmentId = await ResolveOptionalEquipmentIdAsync(Optional(fieldMap, sourceRow, "EquipmentId") ?? Optional(fieldMap, sourceRow, "EquipmentCode"), cancellationToken);
        var operationDefinitionId = await ResolveOptionalOperationDefinitionIdAsync(Optional(fieldMap, sourceRow, "OperationDefinitionId") ?? Optional(fieldMap, sourceRow, "OperationCode"), cancellationToken);
        var operationType = Optional(fieldMap, sourceRow, "OperationType") ?? Optional(fieldMap, sourceRow, "Operation") ?? Optional(fieldMap, sourceRow, "OperationCode") ?? "UnknownOperation";
        var startedAtUtc = RequiredDateTime(fieldMap, sourceRow, "StartedAtUtc");
        var endedAtUtc = OptionalDateTime(fieldMap, sourceRow, "EndedAtUtc");

        var step = new ProcessStepExecution(
            materialUnitId: materialId,
            operationType: operationType,
            startedAtUtc: startedAtUtc,
            endedAtUtc: endedAtUtc,
            isSynthetic: stagingRecord.IsSynthetic,
            equipmentId: equipmentId,
            operationCode: Optional(fieldMap, sourceRow, "OperationCode"),
            operationDefinitionId: operationDefinitionId,
            crewCode: Optional(fieldMap, sourceRow, "CrewCode"),
            executionStatus: Optional(fieldMap, sourceRow, "ExecutionStatus"),
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            plantTimeZoneId: Optional(fieldMap, sourceRow, "PlantTimeZoneId") ?? "UTC",
            plantUtcOffsetMinutes: OptionalInt(fieldMap, sourceRow, "PlantUtcOffsetMinutes") ?? 0);

        if (!previewOnly)
        {
            _dbContext.ProcessStepExecutions.Add(step);
            stagingRecord.MarkMapped(step.Id, "ProcessStepExecution");
        }

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", step.Id, "ProcessStepExecution", null);
    }

    private async Task<MappingExecutionRowResult> MapParameterObservationAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(fieldMap, sourceRow, cancellationToken);
        var parameterDefinitionId = await ResolveParameterDefinitionIdAsync(fieldMap, sourceRow, cancellationToken);
        var equipmentId = await ResolveOptionalEquipmentIdAsync(Optional(fieldMap, sourceRow, "EquipmentId") ?? Optional(fieldMap, sourceRow, "EquipmentCode"), cancellationToken);
        var stepId = OptionalGuid(fieldMap, sourceRow, "ProcessStepExecutionId");
        var observedAtUtc = RequiredDateTime(fieldMap, sourceRow, "ObservedAtUtc");

        var observation = new ParameterObservation(
            materialUnitId: materialId,
            parameterDefinitionId: parameterDefinitionId,
            observedAtUtc: observedAtUtc,
            isSynthetic: stagingRecord.IsSynthetic,
            numericValue: OptionalDecimal(fieldMap, sourceRow, "NumericValue"),
            textValue: Optional(fieldMap, sourceRow, "TextValue"),
            booleanValue: OptionalBool(fieldMap, sourceRow, "BooleanValue"),
            unitOfMeasure: Optional(fieldMap, sourceRow, "UnitOfMeasure"),
            processStepExecutionId: stepId,
            equipmentId: equipmentId,
            qualityFlag: Optional(fieldMap, sourceRow, "QualityFlag"),
            rawValue: Optional(fieldMap, sourceRow, "RawValue"),
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            plantTimeZoneId: Optional(fieldMap, sourceRow, "PlantTimeZoneId") ?? "UTC",
            plantUtcOffsetMinutes: OptionalInt(fieldMap, sourceRow, "PlantUtcOffsetMinutes") ?? 0);

        if (!previewOnly)
        {
            _dbContext.ParameterObservations.Add(observation);
            stagingRecord.MarkMapped(observation.Id, "ParameterObservation");
        }

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", observation.Id, "ParameterObservation", null);
    }

    private async Task<MappingExecutionRowResult> MapQualityEventAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var materialId = await ResolveMaterialIdAsync(fieldMap, sourceRow, cancellationToken);
        var defectCatalogId = await ResolveOptionalDefectCatalogIdAsync(Optional(fieldMap, sourceRow, "DefectCatalogId") ?? Optional(fieldMap, sourceRow, "DefectCode"), cancellationToken);
        var eventType = Required(fieldMap, sourceRow, "EventType");
        var eventAtUtc = RequiredDateTime(fieldMap, sourceRow, "EventAtUtc");

        var qualityEvent = new QualityEvent(
            materialUnitId: materialId,
            eventType: eventType,
            eventAtUtc: eventAtUtc,
            isSynthetic: stagingRecord.IsSynthetic,
            defectCatalogId: defectCatalogId,
            severity: Optional(fieldMap, sourceRow, "Severity"),
            decision: Optional(fieldMap, sourceRow, "Decision"),
            description: Optional(fieldMap, sourceRow, "Description"),
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId,
            plantTimeZoneId: Optional(fieldMap, sourceRow, "PlantTimeZoneId") ?? "UTC",
            plantUtcOffsetMinutes: OptionalInt(fieldMap, sourceRow, "PlantUtcOffsetMinutes") ?? 0);

        if (!previewOnly)
        {
            _dbContext.QualityEvents.Add(qualityEvent);
            stagingRecord.MarkMapped(qualityEvent.Id, "QualityEvent");
        }

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", qualityEvent.Id, "QualityEvent", null);
    }

    private async Task<MappingExecutionRowResult> MapGenealogyEdgeAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        StagingRecord stagingRecord,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var parentId = await ResolveMaterialIdAsync(fieldMap, sourceRow, cancellationToken, "ParentMaterialUnitId", "ParentMaterialCode", "ParentAliasCode");
        var childId = await ResolveMaterialIdAsync(fieldMap, sourceRow, cancellationToken, "ChildMaterialUnitId", "ChildMaterialCode", "ChildAliasCode");
        var relationshipType = Required(fieldMap, sourceRow, "RelationshipType");

        var edge = new GenealogyEdge(
            parentMaterialUnitId: parentId,
            childMaterialUnitId: childId,
            relationshipType: relationshipType,
            isSynthetic: stagingRecord.IsSynthetic,
            sourceSystem: stagingRecord.SourceSystem ?? mapping.SourceSystem,
            sourceRecordId: stagingRecord.SourceRecordId);

        edge.SetEffectiveWindow(
            OptionalDateTime(fieldMap, sourceRow, "EffectiveFromUtc"),
            OptionalDateTime(fieldMap, sourceRow, "EffectiveToUtc"));

        if (!previewOnly)
        {
            _dbContext.GenealogyEdges.Add(edge);
            stagingRecord.MarkMapped(edge.Id, "GenealogyEdge");
        }

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", edge.Id, "GenealogyEdge", null);
    }

    private static MappingExecutionRowResult FailOrThrow(StagingRecord stagingRecord, bool previewOnly, string message)
    {
        if (!previewOnly)
            stagingRecord.MarkFailed(message);

        return new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Failed", null, null, message);
    }

    private async Task<Guid> ResolveMaterialIdAsync(
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        CancellationToken cancellationToken,
        string materialIdField = "MaterialUnitId",
        string materialCodeField = "MaterialCode",
        string aliasCodeField = "AliasCode")
    {
        var materialId = OptionalGuid(fieldMap, sourceRow, materialIdField);
        if (materialId.HasValue)
        {
            var exists = await _dbContext.MaterialUnits.AnyAsync(x => x.Id == materialId.Value, cancellationToken);
            if (!exists)
                throw new InvalidOperationException($"MaterialUnit '{materialId.Value}' does not exist.");
            return materialId.Value;
        }

        var materialCode = Optional(fieldMap, sourceRow, materialCodeField);
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

        var aliasCode = Optional(fieldMap, sourceRow, aliasCodeField);
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

        throw new InvalidOperationException($"Could not resolve material using {materialIdField}, {materialCodeField}, or {aliasCodeField}.");
    }

    private async Task<Guid> ResolveSiteIdAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("SiteId or SiteCode is required for MaterialUnit mapping.");

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
            throw new InvalidOperationException($"Site '{value}' could not be resolved by ID or SiteCode.");

        return byCode;
    }

    private async Task<Guid?> ResolveOptionalEquipmentIdAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
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
            throw new InvalidOperationException($"Equipment '{value}' could not be resolved.");

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

        if (byCode == Guid.Empty)
            return null;

        return byCode;
    }

    private async Task<Guid> ResolveParameterDefinitionIdAsync(
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> sourceRow,
        CancellationToken cancellationToken)
    {
        var direct = OptionalGuid(fieldMap, sourceRow, "ParameterDefinitionId");
        if (direct.HasValue)
        {
            var exists = await _dbContext.ParameterDefinitions.AnyAsync(x => x.Id == direct.Value, cancellationToken);
            if (exists)
                return direct.Value;
        }

        var code = Required(fieldMap, sourceRow, "ParameterCode");
        var id = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x => x.ParameterCode == code)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (id == Guid.Empty)
            throw new InvalidOperationException($"ParameterDefinition '{code}' does not exist.");

        return id;
    }

    private async Task<Guid?> ResolveOptionalDefectCatalogIdAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
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
            return null;

        return byCode;
    }

    private static (Dictionary<string, string> Map, ApplicationError? Error) ParseFieldMap(string mappingJson)
    {
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (map is null || map.Count == 0)
                return (new Dictionary<string, string>(), ApplicationError.Validation("Mapping JSON must contain at least one field mapping."));

            return (map, null);
        }
        catch (JsonException ex)
        {
            return (new Dictionary<string, string>(), ApplicationError.Validation($"Mapping JSON is not valid field-map JSON: {ex.Message}"));
        }
    }

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

    private static string Required(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, string targetField)
    {
        var value = Optional(fieldMap, sourceRow, targetField);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Required mapped field '{targetField}' is missing or empty.");
        return value.Trim();
    }

    private static string? RequiredAny(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, params string[] targetFields)
    {
        foreach (var field in targetFields)
        {
            var value = Optional(fieldMap, sourceRow, field);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return null;
    }

    private static string? Optional(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, string targetField)
    {
        if (!fieldMap.TryGetValue(targetField, out var sourceField))
            return null;

        return sourceRow.TryGetValue(sourceField, out var value)
            ? value?.Trim()
            : null;
    }

    private static Guid? OptionalGuid(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, string targetField)
    {
        var value = Optional(fieldMap, sourceRow, targetField);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static int? OptionalInt(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, string targetField)
    {
        var value = Optional(fieldMap, sourceRow, targetField);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static decimal? OptionalDecimal(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, string targetField)
    {
        var value = Optional(fieldMap, sourceRow, targetField);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static bool? OptionalBool(IReadOnlyDictionary<string, string> fieldMap, IReadOnlyDictionary<string, string?> sourceRow, string targetField)
    {
        var value = Optional(fieldMap, sourceRow, targetField);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return bool.TryParse(value, out var result) ? result : null;
    }

   private static DateTime RequiredDateTime(
    IReadOnlyDictionary<string, string> fieldMap,
    IReadOnlyDictionary<string, string?> sourceRow,
    string targetField)
    {
        var value = Required(fieldMap, sourceRow, targetField);

        var parsed = ParseDate(value);

        if (!parsed.HasValue)
        {
            throw new InvalidOperationException(
                $"Mapped field '{targetField}' value '{value}' is not a valid DateTime. " +
                "Supported formats include ISO 8601, yyyy-MM-dd HH:mm:ss.fff, dd/MM/yyyy, MM/dd/yyyy, Unix epoch seconds/milliseconds, and Excel OADate.");
        }

        return parsed.Value;
    }

   private static DateTime? OptionalDateTime(
    IReadOnlyDictionary<string, string> fieldMap,
    IReadOnlyDictionary<string, string?> sourceRow,
    string targetField)
    {
        var value = Optional(fieldMap, sourceRow, targetField);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parsed = ParseDate(value);

        if (!parsed.HasValue)
        {
            throw new InvalidOperationException(
                $"Mapped field '{targetField}' value '{value}' is not a valid DateTime. " +
                "Supported formats include ISO 8601, yyyy-MM-dd HH:mm:ss.fff, dd/MM/yyyy, MM/dd/yyyy, Unix epoch seconds/milliseconds, and Excel OADate.");
        }

        return parsed.Value;
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
            // Unix milliseconds
            if (longValue > 10_000_000_000)
            {
                return DateTimeOffset
                    .FromUnixTimeMilliseconds(longValue)
                    .UtcDateTime;
            }

            // Unix seconds
            if (longValue > 1_000_000_000)
            {
                return DateTimeOffset
                    .FromUnixTimeSeconds(longValue)
                    .UtcDateTime;
            }

            // Excel OADate, common in Excel exports
            if (longValue > 20_000 && longValue < 80_000)
            {
                return DateTime.SpecifyKind(
                    DateTime.FromOADate(longValue),
                    DateTimeKind.Utc);
            }
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue) &&
            doubleValue > 20_000 &&
            doubleValue < 80_000)
        {
            return DateTime.SpecifyKind(
                DateTime.FromOADate(doubleValue),
                DateTimeKind.Utc);
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




