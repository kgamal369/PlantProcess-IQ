using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Contracts.Integration.Commands;
using PlantProcess.Application.Contracts.Materials;
using PlantProcess.Application.Contracts.Process;
using PlantProcess.Application.Contracts.Quality;
using PlantProcess.Application.Services.Analytics;
using PlantProcess.Application.Services.Analytics.Interfaces;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Integration;
using PlantProcess.Application.Services.Integration.Interfaces;
using PlantProcess.Application.Services.Integration.Services;
using PlantProcess.Application.Services.Materials;
using PlantProcess.Application.Services.Process;
using PlantProcess.Application.Services.Quality;
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
            architectureRule = "Workflow endpoints are thin API facades. Business/process logic belongs to PlantProcess.Application services.",
            workflow = new[]
            {
                "1. Register source system",
                "2. Create import batch",
                "3. Create mapping definition",
                "4. Create canonical material and material aliases",
                "5. Create genealogy edges",
                "6. Add process steps and parameter observations",
                "7. Add process events, downtime events and quality events",
                "8. Raise data-quality issues",
                "9. Store risk scores",
                "10. Investigate one material from genealogy to risk"
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
    // 1. Source system / import / mapping workflow
    // ---------------------------------------------------------------------

    private static async Task<IResult> RegisterSourceSystemAsync(
        RegisterSourceSystemRequest request,
        ISourceSystemService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new RegisterSourceSystemCommand(
            SourceSystemCode: request.SourceSystemCode,
            SourceSystemName: request.SourceSystemName,
            SourceSystemType: request.SourceSystemType,
            IsReadOnlySource: request.IsReadOnlySource,
            Description: request.Description,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.RegisterAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/source-systems/{id}", new
            {
                id,
                request.SourceSystemCode,
                request.SourceSystemName,
                request.SourceSystemType,
                request.IsReadOnlySource
            }));
    }

    private static async Task<IResult> CreateImportBatchAsync(
        CreateImportBatchRequest request,
        IImportBatchService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateImportBatchCommand(
            SourceSystemDefinitionId: request.SourceSystemDefinitionId,
            ImportBatchCode: request.ImportBatchCode,
            ImportType: request.ImportType,
            SourceObjectName: request.SourceObjectName,
            FileName: request.FileName,
            Checksum: request.Checksum,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.CreateAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/import-batches/{id}", new
            {
                id,
                request.ImportBatchCode,
                request.ImportType,
                request.SourceSystemDefinitionId
            }));
    }

    private static async Task<IResult> CreateMappingDefinitionAsync(
        CreateMappingDefinitionRequest request,
        IMappingDefinitionService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateMappingDefinitionCommand(
            SourceSystemDefinitionId: request.SourceSystemDefinitionId,
            MappingCode: request.MappingCode,
            MappingName: request.MappingName,
            SourceObjectName: request.SourceObjectName,
            TargetEntityName: request.TargetEntityName,
            MappingJson: request.MappingJson,
            MappingVersion: request.MappingVersion,
            Description: request.Description,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.CreateAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/mapping-definitions/{id}", new
            {
                id,
                request.MappingCode,
                request.MappingName,
                request.SourceObjectName,
                request.TargetEntityName,
                mappingVersion = request.MappingVersion ?? "v1"
            }));
    }

    // ---------------------------------------------------------------------
    // 2. Material and genealogy workflow
    // ---------------------------------------------------------------------

    private static async Task<IResult> CreateMaterialAsync(
        CreateWorkflowMaterialRequest request,
        IMaterialService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateMaterialCommand(
            MaterialCode: request.MaterialCode,
            MaterialUnitType: request.MaterialUnitType,
            SiteId: request.SiteId,
            ProductFamily: request.ProductFamily,
            GradeOrRecipe: request.GradeOrRecipe,
            ProductionStartUtc: request.ProductionStartUtc,
            ProductionEndUtc: request.ProductionEndUtc,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.CreateAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/materials/{id}", new
            {
                id,
                request.MaterialCode,
                request.MaterialUnitType,
                request.SiteId,
                request.ProductFamily,
                request.GradeOrRecipe
            }));
    }

    private static async Task<IResult> AddMaterialAliasAsync(
        Guid materialUnitId,
        AddWorkflowMaterialAliasRequest request,
        IMaterialService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddMaterialAliasCommand(
            MaterialUnitId: materialUnitId,
            AliasCode: request.AliasCode,
            SourceSystem: request.SourceSystem,
            AliasType: request.AliasType,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddAliasAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/materials/{materialUnitId}/aliases/{id}", new
            {
                id,
                materialUnitId,
                request.AliasCode,
                request.AliasType,
                request.SourceSystem
            }));
    }

    private static async Task<IResult> CreateGenealogyEdgeAsync(
        CreateWorkflowGenealogyEdgeRequest request,
        IGenealogyService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateGenealogyEdgeCommand(
            ParentMaterialUnitId: request.ParentMaterialUnitId,
            ChildMaterialUnitId: request.ChildMaterialUnitId,
            RelationshipType: request.RelationshipType,
            EffectiveFromUtc: request.EffectiveFromUtc,
            EffectiveToUtc: request.EffectiveToUtc,
            Quantity: request.Quantity,
            UnitOfMeasure: request.UnitOfMeasure,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.CreateEdgeAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/genealogy-edges/{id}", new
            {
                id,
                request.ParentMaterialUnitId,
                request.ChildMaterialUnitId,
                request.RelationshipType,
                request.EffectiveFromUtc,
                request.EffectiveToUtc
            }));
    }

    // ---------------------------------------------------------------------
    // 3. Process workflow
    // ---------------------------------------------------------------------

    private static async Task<IResult> AddProcessStepAsync(
        AddWorkflowProcessStepRequest request,
        IProcessDataService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddProcessStepCommand(
            MaterialUnitId: request.MaterialUnitId,
            EquipmentId: request.EquipmentId,
            OperationDefinitionId: request.OperationDefinitionId,
            OperationType: request.OperationType,
            OperationCode: request.OperationCode,
            CrewCode: request.CrewCode,
            StartedAtUtc: request.StartedAtUtc,
            EndedAtUtc: request.EndedAtUtc,
            ExecutionStatus: request.ExecutionStatus,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddProcessStepAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/process-steps/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.EquipmentId,
                request.OperationDefinitionId,
                request.OperationType,
                request.OperationCode,
                request.ExecutionStatus
            }));
    }

    private static async Task<IResult> AddParameterDefinitionAsync(
        AddWorkflowParameterDefinitionRequest request,
        IProcessDataService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddParameterDefinitionCommand(
            ParameterCode: request.ParameterCode,
            ParameterName: request.ParameterName,
            ValueType: request.ValueType,
            UnitOfMeasure: request.UnitOfMeasure,
            ParameterCategory: request.ParameterCategory,
            IndustryTemplate: request.IndustryTemplate,
            ExpectedMinValue: request.ExpectedMinValue,
            ExpectedMaxValue: request.ExpectedMaxValue,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddParameterDefinitionAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/parameter-definitions/{id}", new
            {
                id,
                request.ParameterCode,
                request.ParameterName,
                request.ValueType,
                request.UnitOfMeasure,
                request.ParameterCategory,
                request.IndustryTemplate
            }));
    }

    private static async Task<IResult> AddParameterObservationAsync(
        AddWorkflowParameterObservationRequest request,
        IProcessDataService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddParameterObservationCommand(
            MaterialUnitId: request.MaterialUnitId,
            ProcessStepExecutionId: request.ProcessStepExecutionId,
            ParameterDefinitionId: request.ParameterDefinitionId,
            EquipmentId: request.EquipmentId,
            ObservedAtUtc: request.ObservedAtUtc,
            NumericValue: request.NumericValue,
            TextValue: request.TextValue,
            BooleanValue: request.BooleanValue,
            UnitOfMeasure: request.UnitOfMeasure,
            QualityFlag: request.QualityFlag,
            RawValue: request.RawValue,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddParameterObservationAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/parameter-observations/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.ProcessStepExecutionId,
                request.ParameterDefinitionId,
                request.EquipmentId,
                request.ObservedAtUtc,
                request.NumericValue,
                request.TextValue,
                request.BooleanValue
            }));
    }

    private static async Task<IResult> AddProcessEventAsync(
        AddWorkflowProcessEventRequest request,
        IProcessDataService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddProcessEventCommand(
            MaterialUnitId: request.MaterialUnitId,
            ProcessStepExecutionId: request.ProcessStepExecutionId,
            EquipmentId: request.EquipmentId,
            EventType: request.EventType,
            EventAtUtc: request.EventAtUtc,
            EventValue: request.EventValue,
            Description: request.Description,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddProcessEventAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/process-events/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.ProcessStepExecutionId,
                request.EquipmentId,
                request.EventType,
                request.EventAtUtc
            }));
    }

    private static async Task<IResult> AddDowntimeEventAsync(
        AddWorkflowDowntimeEventRequest request,
        IProcessDataService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddDowntimeEventCommand(
            MaterialUnitId: request.MaterialUnitId,
            ProcessStepExecutionId: request.ProcessStepExecutionId,
            EquipmentId: request.EquipmentId,
            StartedAtUtc: request.StartedAtUtc,
            EndedAtUtc: request.EndedAtUtc,
            DowntimeType: request.DowntimeType,
            ReasonCode: request.ReasonCode,
            Description: request.Description,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddDowntimeEventAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/downtime-events/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.ProcessStepExecutionId,
                request.EquipmentId,
                request.StartedAtUtc,
                request.EndedAtUtc,
                request.DowntimeType,
                request.ReasonCode
            }));
    }

    // ---------------------------------------------------------------------
    // 4. Quality workflow
    // ---------------------------------------------------------------------

    private static async Task<IResult> AddDefectCatalogAsync(
        AddWorkflowDefectCatalogRequest request,
        IQualityService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddDefectCatalogCommand(
            DefectCode: request.DefectCode,
            DefectName: request.DefectName,
            DefectCategory: request.DefectCategory,
            IndustryTemplate: request.IndustryTemplate,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddDefectCatalogAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/defects/{id}", new
            {
                id,
                request.DefectCode,
                request.DefectName,
                request.DefectCategory,
                request.IndustryTemplate
            }));
    }

    private static async Task<IResult> AddQualityEventAsync(
        AddWorkflowQualityEventRequest request,
        IQualityService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new AddQualityEventCommand(
            MaterialUnitId: request.MaterialUnitId,
            DefectCatalogId: request.DefectCatalogId,
            EventType: request.EventType,
            EventAtUtc: request.EventAtUtc,
            Severity: request.Severity,
            Decision: request.Decision,
            Description: request.Description,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.AddQualityEventAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/quality-events/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.DefectCatalogId,
                request.EventType,
                request.EventAtUtc,
                request.Severity,
                request.Decision
            }));
    }

    // ---------------------------------------------------------------------
    // 5. Data quality and risk workflow
    // ---------------------------------------------------------------------

    private static async Task<IResult> RaiseDataQualityIssueAsync(
        RaiseWorkflowDataQualityIssueRequest request,
        IDataQualityService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new RaiseDataQualityIssueCommand(
            MaterialUnitId: request.MaterialUnitId,
            IssueType: request.IssueType,
            Severity: request.Severity,
            Description: request.Description,
            AffectedEntityName: request.AffectedEntityName,
            AffectedEntityId: request.AffectedEntityId,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.RaiseIssueAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/data-quality-issues/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.IssueType,
                severity = request.Severity ?? "Warning",
                request.AffectedEntityName,
                request.AffectedEntityId
            }));
    }

    private static async Task<IResult> StoreRiskScoreAsync(
        StoreWorkflowRiskScoreRequest request,
        IRiskScoreService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new StoreRiskScoreCommand(
            MaterialUnitId: request.MaterialUnitId,
            RiskType: request.RiskType,
            Score: request.Score,
            RiskClass: request.RiskClass,
            MainContributorsJson: request.MainContributorsJson,
            ModelVersion: request.ModelVersion,
            PlantTimeZoneId: request.PlantTimeZoneId,
            PlantUtcOffsetMinutes: request.PlantUtcOffsetMinutes,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.StoreAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/risk-scores/{id}", new
            {
                id,
                request.MaterialUnitId,
                request.RiskType,
                request.Score,
                riskClass = request.RiskClass
            }));
    }

    // ---------------------------------------------------------------------
    // 6. Investigation read model
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
                x.SourceRecordId,
                x.IsSynthetic
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return Results.NotFound(new { message = "Material unit not found." });

        var aliases = await dbContext.MaterialAliases
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.SourceSystem)
            .ThenBy(x => x.AliasCode)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.AliasCode,
                x.AliasType,
                x.SourceSystem,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var parentEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => x.ChildMaterialUnitId == materialUnitId)
            .OrderBy(x => x.EffectiveFromUtc)
            .Select(x => new
            {
                x.Id,
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId,
                x.RelationshipType,
                x.EffectiveFromUtc,
                x.EffectiveToUtc,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var childEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => x.ParentMaterialUnitId == materialUnitId)
            .OrderBy(x => x.EffectiveFromUtc)
            .Select(x => new
            {
                x.Id,
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId,
                x.RelationshipType,
                x.EffectiveFromUtc,
                x.EffectiveToUtc,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var processSteps = await dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.EquipmentId,
                x.OperationDefinitionId,
                x.OperationType,
                x.OperationCode,
                x.CrewCode,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.StartedAtLocal,
                x.EndedAtLocal,
                x.PlantTimeZoneId,
                x.PlantUtcOffsetMinutes,
                x.ExecutionStatus,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var parameterObservations = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.ObservedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
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
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var processEvents = await dbContext.ProcessEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.ProcessStepExecutionId,
                x.EquipmentId,
                x.EventType,
                x.EventAtUtc,
                x.EventAtLocal,
                x.EventValue,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var downtimeEvents = await dbContext.DowntimeEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
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
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var qualityEvents = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.DefectCatalogId,
                x.EventType,
                x.EventAtUtc,
                x.EventAtLocal,
                x.Severity,
                x.Decision,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var dataQualityIssues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.IssueType,
                x.Severity,
                x.Description,
                x.AffectedEntityName,
                x.AffectedEntityId,
                x.SourceSystem,
                x.SourceRecordId,
                x.CreatedAtUtc,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var riskScores = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId)
            .OrderByDescending(x => x.ScoredAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.RiskType,
                x.Score,
                x.RiskClass,
                x.MainContributorsJson,
                x.ModelVersion,
                x.ScoredAtUtc,
                x.ScoredAtLocal,
                x.PlantTimeZoneId,
                x.PlantUtcOffsetMinutes,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            material,
            aliases,
            genealogy = new
            {
                parents = parentEdges,
                children = childEdges
            },
            process = new
            {
                processSteps,
                parameterObservations,
                processEvents,
                downtimeEvents
            },
            quality = new
            {
                qualityEvents
            },
            dataQualityIssues,
            riskScores
        });
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static CommandMetadata ToMetadata(
        bool isSynthetic,
        string? sourceSystem,
        string? sourceRecordId,
        HttpContext httpContext)
    {
        var correlationId = httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var value)
            ? value.ToString()
            : httpContext.TraceIdentifier;

        return new CommandMetadata(
            IsSynthetic: isSynthetic,
            SourceSystem: sourceSystem,
            SourceRecordId: sourceRecordId,
            RequestedBy: httpContext.User?.Identity?.Name,
            CorrelationId: correlationId);
    }

    // ---------------------------------------------------------------------
    // Request DTOs
    // ---------------------------------------------------------------------

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
        string? MappingVersion,
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
        string? AliasType,
        string SourceSystem,
        bool IsSynthetic,
        string? SourceRecordId);

    public sealed record CreateWorkflowGenealogyEdgeRequest(
        Guid ParentMaterialUnitId,
        Guid ChildMaterialUnitId,
        string RelationshipType,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        decimal? Quantity,
        string? UnitOfMeasure,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AddWorkflowProcessStepRequest(
        Guid MaterialUnitId,
        Guid? EquipmentId,
        Guid? OperationDefinitionId,
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