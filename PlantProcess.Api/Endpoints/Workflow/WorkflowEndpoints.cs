using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Workflow;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workflow")
            .WithTags("Workflow");

        group.MapGet("/overview", GetWorkflowOverview);
        group.MapGet("/status", GetWorkflowStatusAsync);

        group.MapPost("/source-systems", RegisterSourceSystemAsync);
        group.MapPost("/import-batches", CreateImportBatchAsync);
        group.MapPost("/mapping-definitions", CreateMappingDefinitionAsync);

        group.MapPost("/materials", CreateMaterialAsync);
        group.MapPost("/materials/{materialUnitId:guid}/aliases", AddMaterialAliasAsync);
        group.MapPost("/genealogy-edges", CreateGenealogyEdgeAsync);

        group.MapPost("/process-steps", AddProcessStepAsync);
        group.MapPost("/parameter-definitions", AddParameterDefinitionAsync);
        group.MapPost("/parameter-observations", AddParameterObservationAsync);

        group.MapPost("/process-events", AddProcessEventAsync);
        group.MapPost("/downtime-events", AddDowntimeEventAsync);
        group.MapPost("/defects", AddDefectCatalogAsync);
        group.MapPost("/quality-events", AddQualityEventAsync);

        group.MapPost("/data-quality-issues", RaiseDataQualityIssueAsync);
        group.MapPost("/risk-scores", StoreRiskScoreAsync);

        group.MapGet("/materials/{materialUnitId:guid}/investigation", InvestigateMaterialAsync);

        return app;
    }

    private static IResult GetWorkflowOverview()
    {
        return Results.Ok(new
        {
            product = "PlantProcess IQ",
            purpose = "Generic manufacturing process-to-quality intelligence workflow.",
            rule = "API contains no demo data. Demo/sample data must be inserted into the database through SQL scripts, imports, or synthetic generators.",
            workflow = new[]
            {
                "1. Register source system",
                "2. Create import batch",
                "3. Apply mapping definition",
                "4. Create canonical material and genealogy",
                "5. Add process steps and parameter observations",
                "6. Add process events, downtime events and quality events",
                "7. Raise data-quality issues",
                "8. Store risk scores",
                "9. Investigate one material from genealogy to risk"
            }
        });
    }

    private static async Task<IResult> GetWorkflowStatusAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = new
        {
            sourceSystems = await dbContext.SourceSystemDefinitions.CountAsync(cancellationToken),
            importBatches = await dbContext.ImportBatches.CountAsync(cancellationToken),
            mappingDefinitions = await dbContext.MappingDefinitions.CountAsync(cancellationToken),

            materials = await dbContext.MaterialUnits.CountAsync(cancellationToken),
            aliases = await dbContext.MaterialAliases.CountAsync(cancellationToken),
            genealogyEdges = await dbContext.GenealogyEdges.CountAsync(cancellationToken),

            processSteps = await dbContext.ProcessStepExecutions.CountAsync(cancellationToken),
            parameterDefinitions = await dbContext.ParameterDefinitions.CountAsync(cancellationToken),
            parameterObservations = await dbContext.ParameterObservations.CountAsync(cancellationToken),

            processEvents = await dbContext.ProcessEvents.CountAsync(cancellationToken),
            downtimeEvents = await dbContext.DowntimeEvents.CountAsync(cancellationToken),
            defectCatalogs = await dbContext.DefectCatalogs.CountAsync(cancellationToken),
            qualityEvents = await dbContext.QualityEvents.CountAsync(cancellationToken),

            dataQualityIssues = await dbContext.DataQualityIssues.CountAsync(cancellationToken),
            riskScores = await dbContext.RiskScores.CountAsync(cancellationToken)
        };

        return Results.Ok(result);
    }

    // ---------------------------------------------------------------------
    // 1. Register source system
    // ---------------------------------------------------------------------
    private static async Task<IResult> RegisterSourceSystemAsync(
        RegisterSourceSystemRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.SourceSystemCode == request.SourceSystemCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Source system code already exists." });

        var sourceSystem = new SourceSystemDefinition(
            sourceSystemCode: request.SourceSystemCode,
            sourceSystemName: request.SourceSystemName,
            sourceSystemType: request.SourceSystemType,
            isSynthetic: request.IsSynthetic,
            description: request.Description,
            isReadOnlySource: request.IsReadOnlySource,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.SourceSystemDefinitions.Add(sourceSystem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/source-systems/{sourceSystem.Id}", new
        {
            sourceSystem.Id,
            sourceSystem.SourceSystemCode,
            sourceSystem.SourceSystemName,
            sourceSystem.SourceSystemType
        });
    }

    // ---------------------------------------------------------------------
    // 2. Create import batch
    // ---------------------------------------------------------------------
    private static async Task<IResult> CreateImportBatchAsync(
        CreateImportBatchRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sourceExists = await dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.Id == request.SourceSystemDefinitionId, cancellationToken);

        if (!sourceExists)
            return Results.BadRequest(new { message = "Source system definition does not exist." });

        var exists = await dbContext.ImportBatches
            .AnyAsync(x => x.ImportBatchCode == request.ImportBatchCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Import batch code already exists." });

        var importBatch = new ImportBatch(
            sourceSystemDefinitionId: request.SourceSystemDefinitionId,
            importBatchCode: request.ImportBatchCode,
            importType: request.ImportType,
            isSynthetic: request.IsSynthetic,
            sourceObjectName: request.SourceObjectName,
            fileName: request.FileName,
            checksum: request.Checksum,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.ImportBatches.Add(importBatch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/import-batches/{importBatch.Id}", new
        {
            importBatch.Id,
            importBatch.ImportBatchCode,
            importBatch.ImportType,
            importBatch.Status
        });
    }

    // ---------------------------------------------------------------------
    // 3. Apply mapping definition
    // ---------------------------------------------------------------------
    private static async Task<IResult> CreateMappingDefinitionAsync(
        CreateMappingDefinitionRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sourceExists = await dbContext.SourceSystemDefinitions
            .AnyAsync(x => x.Id == request.SourceSystemDefinitionId, cancellationToken);

        if (!sourceExists)
            return Results.BadRequest(new { message = "Source system definition does not exist." });

        var exists = await dbContext.MappingDefinitions
            .AnyAsync(x => x.MappingCode == request.MappingCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Mapping code already exists." });

        var mapping = new MappingDefinition(
            sourceSystemDefinitionId: request.SourceSystemDefinitionId,
            mappingCode: request.MappingCode,
            mappingName: request.MappingName,
            sourceObjectName: request.SourceObjectName,
            targetEntityName: request.TargetEntityName,
            mappingJson: request.MappingJson,
            isSynthetic: request.IsSynthetic,
            mappingVersion: request.MappingVersion,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.MappingDefinitions.Add(mapping);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/mapping-definitions/{mapping.Id}", new
        {
            mapping.Id,
            mapping.MappingCode,
            mapping.TargetEntityName,
            mapping.MappingVersion
        });
    }

    // ---------------------------------------------------------------------
    // 4. Create canonical material
    // ---------------------------------------------------------------------
    private static async Task<IResult> CreateMaterialAsync(
        CreateWorkflowMaterialRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var siteExists = await dbContext.Sites
            .AnyAsync(x => x.Id == request.SiteId, cancellationToken);

        if (!siteExists)
            return Results.BadRequest(new { message = "Site does not exist." });

        var exists = await dbContext.MaterialUnits
            .AnyAsync(x => x.MaterialCode == request.MaterialCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Material code already exists." });

        var material = new MaterialUnit(
            materialCode: request.MaterialCode,
            materialUnitType: request.MaterialUnitType,
            siteId: request.SiteId,
            productFamily: request.ProductFamily,
            gradeOrRecipe: request.GradeOrRecipe,
            isSynthetic: request.IsSynthetic,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        if (request.ProductionStartUtc.HasValue)
        {
            material.SetProductionWindow(
                request.ProductionStartUtc.Value,
                request.ProductionEndUtc,
                TimeSpan.FromMinutes(request.PlantUtcOffsetMinutes ?? 60),
                request.PlantTimeZoneId ?? "Europe/Berlin");
        }

        dbContext.MaterialUnits.Add(material);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/materials/{material.Id}", new
        {
            material.Id,
            material.MaterialCode,
            material.MaterialUnitType
        });
    }

    private static async Task<IResult> AddMaterialAliasAsync(
        Guid materialUnitId,
        AddWorkflowMaterialAliasRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var material = await dbContext.MaterialUnits
            .FirstOrDefaultAsync(x => x.Id == materialUnitId, cancellationToken);

        if (material is null)
            return Results.NotFound(new { message = "Material unit not found." });

        material.AddAlias(
            aliasCode: request.AliasCode,
            sourceSystem: request.SourceSystem,
            aliasType: request.AliasType);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            material.Id,
            request.AliasCode,
            request.AliasType,
            request.SourceSystem
        });
    }

    private static async Task<IResult> CreateGenealogyEdgeAsync(
        CreateWorkflowGenealogyEdgeRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.ParentMaterialUnitId == request.ChildMaterialUnitId)
            return Results.BadRequest(new { message = "Parent and child material cannot be the same." });

        var parentExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.Id == request.ParentMaterialUnitId, cancellationToken);

        var childExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.Id == request.ChildMaterialUnitId, cancellationToken);

        if (!parentExists || !childExists)
            return Results.BadRequest(new { message = "Parent or child material does not exist." });

        var edge = new GenealogyEdge(
            parentMaterialUnitId: request.ParentMaterialUnitId,
            childMaterialUnitId: request.ChildMaterialUnitId,
            relationshipType: request.RelationshipType,
            isSynthetic: request.IsSynthetic,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        if (request.EffectiveFromUtc.HasValue || request.EffectiveToUtc.HasValue)
        {
            edge.SetEffectiveWindow(
                request.EffectiveFromUtc,
                request.EffectiveToUtc);
        }

        dbContext.GenealogyEdges.Add(edge);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/genealogy-edges/{edge.Id}", new
        {
            edge.Id,
            edge.ParentMaterialUnitId,
            edge.ChildMaterialUnitId,
            edge.RelationshipType
        });
    }

    // ---------------------------------------------------------------------
    // 5. Add process steps and parameters
    // ---------------------------------------------------------------------
    private static async Task<IResult> AddProcessStepAsync(
        AddWorkflowProcessStepRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

        if (!materialExists)
            return Results.BadRequest(new { message = "Material unit does not exist." });

        if (request.EquipmentId.HasValue)
        {
            var equipmentExists = await dbContext.Equipment
                .AnyAsync(x => x.Id == request.EquipmentId.Value, cancellationToken);

            if (!equipmentExists)
                return Results.BadRequest(new { message = "Equipment does not exist." });
        }

        var step = new ProcessStepExecution(
            materialUnitId: request.MaterialUnitId,
            operationType: request.OperationType,
            startedAtUtc: request.StartedAtUtc,
            endedAtUtc: request.EndedAtUtc,
            isSynthetic: request.IsSynthetic,
            equipmentId: request.EquipmentId,
            operationCode: request.OperationCode,
            crewCode: request.CrewCode,
            executionStatus: request.ExecutionStatus,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId,
            plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
            plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

        dbContext.ProcessStepExecutions.Add(step);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/process-steps/{step.Id}", new
        {
            step.Id,
            step.MaterialUnitId,
            step.OperationType,
            step.ExecutionStatus
        });
    }

    private static async Task<IResult> AddParameterDefinitionAsync(
        AddWorkflowParameterDefinitionRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ParameterDefinitions
            .AnyAsync(x => x.ParameterCode == request.ParameterCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Parameter code already exists." });

        var definition = new ParameterDefinition(
            parameterCode: request.ParameterCode,
            parameterName: request.ParameterName,
            valueType: request.ValueType,
            unitOfMeasure: request.UnitOfMeasure,
            parameterCategory: request.ParameterCategory,
            industryTemplate: request.IndustryTemplate,
            isSynthetic: request.IsSynthetic,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        if (request.ExpectedMinValue.HasValue || request.ExpectedMaxValue.HasValue)
        {
            definition.SetExpectedRange(
                request.ExpectedMinValue,
                request.ExpectedMaxValue);
        }

        dbContext.ParameterDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/parameter-definitions/{definition.Id}", new
        {
            definition.Id,
            definition.ParameterCode,
            definition.ParameterName
        });
    }

    private static async Task<IResult> AddParameterObservationAsync(
        AddWorkflowParameterObservationRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

        if (!materialExists)
            return Results.BadRequest(new { message = "Material unit does not exist." });

        var definitionExists = await dbContext.ParameterDefinitions
            .AnyAsync(x => x.Id == request.ParameterDefinitionId, cancellationToken);

        if (!definitionExists)
            return Results.BadRequest(new { message = "Parameter definition does not exist." });

        if (request.ProcessStepExecutionId.HasValue)
        {
            var stepExists = await dbContext.ProcessStepExecutions
                .AnyAsync(x => x.Id == request.ProcessStepExecutionId.Value, cancellationToken);

            if (!stepExists)
                return Results.BadRequest(new { message = "Process step does not exist." });
        }

        if (request.EquipmentId.HasValue)
        {
            var equipmentExists = await dbContext.Equipment
                .AnyAsync(x => x.Id == request.EquipmentId.Value, cancellationToken);

            if (!equipmentExists)
                return Results.BadRequest(new { message = "Equipment does not exist." });
        }

        var observation = new ParameterObservation(
            materialUnitId: request.MaterialUnitId,
            parameterDefinitionId: request.ParameterDefinitionId,
            observedAtUtc: request.ObservedAtUtc,
            isSynthetic: request.IsSynthetic,
            numericValue: request.NumericValue,
            textValue: request.TextValue,
            booleanValue: request.BooleanValue,
            unitOfMeasure: request.UnitOfMeasure,
            processStepExecutionId: request.ProcessStepExecutionId,
            equipmentId: request.EquipmentId,
            qualityFlag: request.QualityFlag,
            rawValue: request.RawValue,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId,
            plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
            plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

        dbContext.ParameterObservations.Add(observation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/parameter-observations/{observation.Id}", new
        {
            observation.Id,
            observation.MaterialUnitId,
            observation.ParameterDefinitionId,
            observation.ObservedAtUtc
        });
    }

    // ---------------------------------------------------------------------
    // 6. Add process events / downtime / quality events
    // ---------------------------------------------------------------------
    private static async Task<IResult> AddProcessEventAsync(
        AddWorkflowProcessEventRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationResult = await ValidateOptionalProcessReferencesAsync(
            dbContext,
            request.MaterialUnitId,
            request.ProcessStepExecutionId,
            request.EquipmentId,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        if (!request.MaterialUnitId.HasValue &&
            !request.ProcessStepExecutionId.HasValue &&
            !request.EquipmentId.HasValue)
        {
            return Results.BadRequest(new
            {
                message = "Process event must be linked to at least one material, process step or equipment record."
            });
        }

        var processEvent = new ProcessEvent(
            eventType: request.EventType,
            eventAtUtc: request.EventAtUtc,
            isSynthetic: request.IsSynthetic,
            materialUnitId: request.MaterialUnitId,
            processStepExecutionId: request.ProcessStepExecutionId,
            equipmentId: request.EquipmentId,
            eventValue: request.EventValue,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId,
            plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
            plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

        dbContext.ProcessEvents.Add(processEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/process-events/{processEvent.Id}", new
        {
            processEvent.Id,
            processEvent.EventType,
            processEvent.EventAtUtc
        });
    }

    private static async Task<IResult> AddDowntimeEventAsync(
        AddWorkflowDowntimeEventRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationResult = await ValidateOptionalProcessReferencesAsync(
            dbContext,
            request.MaterialUnitId,
            request.ProcessStepExecutionId,
            request.EquipmentId,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        if (!request.MaterialUnitId.HasValue &&
            !request.ProcessStepExecutionId.HasValue &&
            !request.EquipmentId.HasValue)
        {
            return Results.BadRequest(new
            {
                message = "Downtime event must be linked to at least one material, process step or equipment record."
            });
        }

        var downtimeEvent = new DowntimeEvent(
            startedAtUtc: request.StartedAtUtc,
            downtimeType: request.DowntimeType,
            isSynthetic: request.IsSynthetic,
            endedAtUtc: request.EndedAtUtc,
            materialUnitId: request.MaterialUnitId,
            processStepExecutionId: request.ProcessStepExecutionId,
            equipmentId: request.EquipmentId,
            reasonCode: request.ReasonCode,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId,
            plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
            plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

        dbContext.DowntimeEvents.Add(downtimeEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/downtime-events/{downtimeEvent.Id}", new
        {
            downtimeEvent.Id,
            downtimeEvent.DowntimeType,
            downtimeEvent.StartedAtUtc,
            downtimeEvent.EndedAtUtc
        });
    }

    private static async Task<IResult> AddDefectCatalogAsync(
        AddWorkflowDefectCatalogRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.DefectCatalogs
            .AnyAsync(x => x.DefectCode == request.DefectCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Defect code already exists." });

        var defect = new DefectCatalog(
            defectCode: request.DefectCode,
            defectName: request.DefectName,
            defectCategory: request.DefectCategory,
            industryTemplate: request.IndustryTemplate,
            isSynthetic: request.IsSynthetic,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.DefectCatalogs.Add(defect);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/defects/{defect.Id}", new
        {
            defect.Id,
            defect.DefectCode,
            defect.DefectName
        });
    }

    private static async Task<IResult> AddQualityEventAsync(
        AddWorkflowQualityEventRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

        if (!materialExists)
            return Results.BadRequest(new { message = "Material unit does not exist." });

        if (request.DefectCatalogId.HasValue)
        {
            var defectExists = await dbContext.DefectCatalogs
                .AnyAsync(x => x.Id == request.DefectCatalogId.Value, cancellationToken);

            if (!defectExists)
                return Results.BadRequest(new { message = "Defect catalog does not exist." });
        }

        var qualityEvent = new QualityEvent(
            materialUnitId: request.MaterialUnitId,
            eventType: request.EventType,
            eventAtUtc: request.EventAtUtc,
            isSynthetic: request.IsSynthetic,
            defectCatalogId: request.DefectCatalogId,
            severity: request.Severity,
            decision: request.Decision,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId,
            plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
            plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

        dbContext.QualityEvents.Add(qualityEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/quality-events/{qualityEvent.Id}", new
        {
            qualityEvent.Id,
            qualityEvent.MaterialUnitId,
            qualityEvent.EventType,
            qualityEvent.Decision
        });
    }

    // ---------------------------------------------------------------------
    // 7. Raise data-quality issues
    // ---------------------------------------------------------------------
    private static async Task<IResult> RaiseDataQualityIssueAsync(
        RaiseWorkflowDataQualityIssueRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.MaterialUnitId.HasValue)
        {
            var materialExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.MaterialUnitId.Value, cancellationToken);

            if (!materialExists)
                return Results.BadRequest(new { message = "Material unit does not exist." });
        }

        var issue = new DataQualityIssue(
            issueType: request.IssueType,
            description: request.Description,
            isSynthetic: request.IsSynthetic,
            materialUnitId: request.MaterialUnitId,
            severity: request.Severity ?? "Warning",
            affectedEntityName: request.AffectedEntityName,
            affectedEntityId: request.AffectedEntityId,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.DataQualityIssues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/data-quality-issues/{issue.Id}", new
        {
            issue.Id,
            issue.IssueType,
            issue.Severity,
            issue.MaterialUnitId
        });
    }

    // ---------------------------------------------------------------------
    // 8. Store risk scores
    // ---------------------------------------------------------------------
    private static async Task<IResult> StoreRiskScoreAsync(
        StoreWorkflowRiskScoreRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

        if (!materialExists)
            return Results.BadRequest(new { message = "Material unit does not exist." });

        var riskScore = new RiskScore(
            materialUnitId: request.MaterialUnitId,
            riskType: request.RiskType,
            score: request.Score,
            isSynthetic: request.IsSynthetic,
            riskClass: request.RiskClass,
            mainContributorsJson: request.MainContributorsJson,
            modelVersion: request.ModelVersion,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId,
            plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
            plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

        dbContext.RiskScores.Add(riskScore);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/workflow/risk-scores/{riskScore.Id}", new
        {
            riskScore.Id,
            riskScore.MaterialUnitId,
            riskScore.RiskType,
            riskScore.Score,
            riskScore.RiskClass
        });
    }

    // ---------------------------------------------------------------------
    // 9. Investigate one material from genealogy to risk
    // ---------------------------------------------------------------------
    private static async Task<IResult> InvestigateMaterialAsync(
        Guid materialUnitId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var material = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == materialUnitId)
            .Select(x => new
            {
                x.Id,
                x.MaterialCode,
                x.MaterialUnitType,
                x.ProductFamily,
                x.GradeOrRecipe,
                x.SiteId,
                x.ProductionStartUtc,
                x.ProductionEndUtc,
                x.ProductionStartLocal,
                x.ProductionEndLocal,
                x.PlantTimeZoneId,
                x.PlantUtcOffsetMinutes,
                x.SourceSystem,
                x.SourceRecordId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return Results.NotFound(new { message = "Material unit not found." });

        var aliases = await dbContext.MaterialAliases
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .Select(x => new
            {
                x.Id,
                x.AliasCode,
                x.AliasType,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var genealogyEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => x.ParentMaterialUnitId == materialUnitId || x.ChildMaterialUnitId == materialUnitId)
            .Select(x => new
            {
                x.Id,
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId,
                x.RelationshipType,
                x.EffectiveFromUtc,
                x.EffectiveToUtc,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var processSteps = await dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.EquipmentId,
                x.OperationType,
                x.OperationCode,
                x.CrewCode,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.StartedAtLocal,
                x.EndedAtLocal,
                x.ExecutionStatus,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var stepIds = processSteps.Select(x => x.Id).ToList();

        var parameterObservations = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x =>
                x.MaterialUnitId == materialUnitId ||
                (x.ProcessStepExecutionId.HasValue && stepIds.Contains(x.ProcessStepExecutionId.Value)))
            .OrderBy(x => x.ObservedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.ProcessStepExecutionId,
                x.ParameterDefinitionId,
                x.EquipmentId,
                x.ObservedAtUtc,
                x.ObservedAtLocal,
                x.NumericValue,
                x.TextValue,
                x.BooleanValue,
                x.UnitOfMeasure,
                x.QualityFlag,
                x.RawValue,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var processEvents = await dbContext.ProcessEvents
            .AsNoTracking()
            .Where(x =>
                x.MaterialUnitId == materialUnitId ||
                (x.ProcessStepExecutionId.HasValue && stepIds.Contains(x.ProcessStepExecutionId.Value)))
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.ProcessStepExecutionId,
                x.EquipmentId,
                x.EventType,
                x.EventAtUtc,
                x.EventAtLocal,
                x.EventValue,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var downtimeEvents = await dbContext.DowntimeEvents
            .AsNoTracking()
            .Where(x =>
                x.MaterialUnitId == materialUnitId ||
                (x.ProcessStepExecutionId.HasValue && stepIds.Contains(x.ProcessStepExecutionId.Value)))
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.ProcessStepExecutionId,
                x.EquipmentId,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.StartedAtLocal,
                x.EndedAtLocal,
                x.DowntimeType,
                x.ReasonCode,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var qualityEvents = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.DefectCatalogId,
                x.EventType,
                x.EventAtUtc,
                x.EventAtLocal,
                x.Severity,
                x.Decision,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var riskScores = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderByDescending(x => x.ScoredAtUtc)
            .Select(x => new
            {
                x.Id,
                x.RiskType,
                x.Score,
                x.RiskClass,
                x.MainContributorsJson,
                x.ModelVersion,
                x.ScoredAtUtc,
                x.ScoredAtLocal,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        var dataQualityIssues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.IssueType,
                x.Severity,
                x.Description,
                x.AffectedEntityName,
                x.AffectedEntityId,
                x.SourceSystem,
                x.SourceRecordId,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            material,
            aliases,
            genealogyEdges,
            processSteps,
            parameterObservations,
            processEvents,
            downtimeEvents,
            qualityEvents,
            riskScores,
            dataQualityIssues
        });
    }

    private static async Task<IResult?> ValidateOptionalProcessReferencesAsync(
        PlantProcessDbContext dbContext,
        Guid? materialUnitId,
        Guid? processStepExecutionId,
        Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        if (materialUnitId.HasValue)
        {
            var materialExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == materialUnitId.Value, cancellationToken);

            if (!materialExists)
                return Results.BadRequest(new { message = "Material unit does not exist." });
        }

        if (processStepExecutionId.HasValue)
        {
            var stepExists = await dbContext.ProcessStepExecutions
                .AnyAsync(x => x.Id == processStepExecutionId.Value, cancellationToken);

            if (!stepExists)
                return Results.BadRequest(new { message = "Process step does not exist." });
        }

        if (equipmentId.HasValue)
        {
            var equipmentExists = await dbContext.Equipment
                .AnyAsync(x => x.Id == equipmentId.Value, cancellationToken);

            if (!equipmentExists)
                return Results.BadRequest(new { message = "Equipment does not exist." });
        }

        return null;
    }

    public sealed record RegisterSourceSystemRequest(
        string SourceSystemCode,
        string SourceSystemName,
        string SourceSystemType,
        string? Description,
        bool IsReadOnlySource,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateImportBatchRequest(
        Guid SourceSystemDefinitionId,
        string ImportBatchCode,
        string ImportType,
        string? SourceObjectName,
        string? FileName,
        string? Checksum,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateMappingDefinitionRequest(
        Guid SourceSystemDefinitionId,
        string MappingCode,
        string MappingName,
        string SourceObjectName,
        string TargetEntityName,
        string MappingJson,
        string MappingVersion,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateWorkflowMaterialRequest(
        string MaterialCode,
        string MaterialUnitType,
        Guid SiteId,
        string? ProductFamily,
        string? GradeOrRecipe,
        DateTime? ProductionStartUtc,
        DateTime? ProductionEndUtc,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AddWorkflowMaterialAliasRequest(
        string AliasCode,
        string AliasType,
        string SourceSystem);

    public sealed record CreateWorkflowGenealogyEdgeRequest(
        Guid ParentMaterialUnitId,
        Guid ChildMaterialUnitId,
        string RelationshipType,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AddWorkflowProcessStepRequest(
        Guid MaterialUnitId,
        Guid? EquipmentId,
        string OperationType,
        string? OperationCode,
        string? CrewCode,
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        string? ExecutionStatus,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

    public sealed record AddWorkflowParameterDefinitionRequest(
        string ParameterCode,
        string ParameterName,
        string ValueType,
        string? UnitOfMeasure,
        string? ParameterCategory,
        string? IndustryTemplate,
        decimal? ExpectedMinValue,
        decimal? ExpectedMaxValue,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AddWorkflowParameterObservationRequest(
        Guid MaterialUnitId,
        Guid? ProcessStepExecutionId,
        Guid ParameterDefinitionId,
        Guid? EquipmentId,
        DateTime ObservedAtUtc,
        decimal? NumericValue,
        string? TextValue,
        bool? BooleanValue,
        string? UnitOfMeasure,
        string? QualityFlag,
        string? RawValue,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

    public sealed record AddWorkflowProcessEventRequest(
        Guid? MaterialUnitId,
        Guid? ProcessStepExecutionId,
        Guid? EquipmentId,
        string EventType,
        DateTime EventAtUtc,
        string? EventValue,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

    public sealed record AddWorkflowDowntimeEventRequest(
        Guid? MaterialUnitId,
        Guid? ProcessStepExecutionId,
        Guid? EquipmentId,
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        string DowntimeType,
        string? ReasonCode,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

    public sealed record AddWorkflowDefectCatalogRequest(
        string DefectCode,
        string DefectName,
        string? DefectCategory,
        string? IndustryTemplate,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AddWorkflowQualityEventRequest(
        Guid MaterialUnitId,
        Guid? DefectCatalogId,
        string EventType,
        DateTime EventAtUtc,
        string? Severity,
        string? Decision,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

    public sealed record RaiseWorkflowDataQualityIssueRequest(
        Guid? MaterialUnitId,
        string IssueType,
        string? Severity,
        string Description,
        string? AffectedEntityName,
        Guid? AffectedEntityId,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record StoreWorkflowRiskScoreRequest(
        Guid MaterialUnitId,
        string RiskType,
        decimal Score,
        string? RiskClass,
        string? MainContributorsJson,
        string? ModelVersion,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);
}