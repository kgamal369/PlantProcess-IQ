using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Infrastructure.Persistence;
using RouteEntity = PlantProcess.Domain.Entities.Configuration.Route;

namespace PlantProcess.Api.Endpoints.Configuration;

public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/configuration")
            .WithTags("Configuration");

        group.MapGet("/status", () => Results.Ok(new
        {
            message = "Configuration API exists.",
            purpose = "Industry templates, material unit types, operation definitions, routes and route steps.",
            rule = "Industry-specific names stay as configuration, not hard-coded architecture."
        }));

        group.MapGet("/summary", GetConfigurationSummaryAsync);

        group.MapGet("/industry-templates", GetIndustryTemplatesAsync);
        group.MapPost("/industry-templates", CreateIndustryTemplateAsync);
        group.MapPatch("/industry-templates/{id:guid}/activate", ActivateIndustryTemplateAsync);
        group.MapPatch("/industry-templates/{id:guid}/deactivate", DeactivateIndustryTemplateAsync);

        group.MapGet("/material-unit-types", GetMaterialUnitTypesAsync);
        group.MapPost("/material-unit-types", CreateMaterialUnitTypeAsync);
        group.MapPatch("/material-unit-types/{id:guid}/activate", ActivateMaterialUnitTypeAsync);
        group.MapPatch("/material-unit-types/{id:guid}/deactivate", DeactivateMaterialUnitTypeAsync);

        group.MapGet("/operation-definitions", GetOperationDefinitionsAsync);
        group.MapPost("/operation-definitions", CreateOperationDefinitionAsync);
        group.MapPatch("/operation-definitions/{id:guid}/activate", ActivateOperationDefinitionAsync);
        group.MapPatch("/operation-definitions/{id:guid}/deactivate", DeactivateOperationDefinitionAsync);

        group.MapGet("/routes", GetRoutesAsync);
        group.MapPost("/routes", CreateRouteAsync);
        group.MapPatch("/routes/{id:guid}/activate", ActivateRouteAsync);
        group.MapPatch("/routes/{id:guid}/deactivate", DeactivateRouteAsync);

        group.MapGet("/route-steps", GetRouteStepsAsync);
        group.MapPost("/route-steps", CreateRouteStepAsync);

        return app;
    }

    private static async Task<IResult> GetConfigurationSummaryAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return Results.Ok(new
        {
            industryTemplates = await dbContext.IndustryTemplates.CountAsync(cancellationToken),
            materialUnitTypes = await dbContext.MaterialUnitTypeDefinitions.CountAsync(cancellationToken),
            operationDefinitions = await dbContext.OperationDefinitions.CountAsync(cancellationToken),
            routes = await dbContext.Routes.CountAsync(cancellationToken),
            routeSteps = await dbContext.RouteSteps.CountAsync(cancellationToken)
        });
    }

    private static async Task<IResult> GetIndustryTemplatesAsync(
        bool? activeOnly,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IndustryTemplates.AsNoTracking();

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsDeleted);

        var result = await query
            .OrderBy(x => x.IndustryName)
            .ThenBy(x => x.TemplateCode)
            .Select(x => new
            {
                x.Id,
                x.TemplateCode,
                x.TemplateName,
                x.IndustryName,
                x.Version,
                x.Description,
                x.IsActive,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateIndustryTemplateAsync(
        CreateIndustryTemplateRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.IndustryTemplates
            .AnyAsync(x => x.TemplateCode == request.TemplateCode, cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Industry template code already exists." });

        var template = new IndustryTemplate(
            templateCode: request.TemplateCode,
            templateName: request.TemplateName,
            industryName: request.IndustryName,
            isSynthetic: request.IsSynthetic,
            version: request.Version ?? "v1",
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.IndustryTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/configuration/industry-templates/{template.Id}", new
        {
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.IndustryName,
            template.Version
        });
    }

    private static async Task<IResult> GetMaterialUnitTypesAsync(
        Guid? industryTemplateId,
        bool? activeOnly,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MaterialUnitTypeDefinitions.AsNoTracking();

        if (industryTemplateId.HasValue)
            query = query.Where(x => x.IndustryTemplateId == industryTemplateId.Value);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsDeleted);

        var result = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.MaterialUnitTypeCode)
            .Select(x => new
            {
                x.Id,
                x.IndustryTemplateId,
                x.MaterialUnitTypeCode,
                x.MaterialUnitTypeName,
                x.Description,
                x.SortOrder,
                x.IsActive,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateMaterialUnitTypeAsync(
        CreateMaterialUnitTypeRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var templateExists = await dbContext.IndustryTemplates
            .AnyAsync(x => x.Id == request.IndustryTemplateId, cancellationToken);

        if (!templateExists)
            return Results.BadRequest(new { message = "Industry template does not exist." });

        var exists = await dbContext.MaterialUnitTypeDefinitions.AnyAsync(x =>
            x.IndustryTemplateId == request.IndustryTemplateId &&
            x.MaterialUnitTypeCode == request.MaterialUnitTypeCode,
            cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Material unit type code already exists for this template." });

        var type = new MaterialUnitTypeDefinition(
            industryTemplateId: request.IndustryTemplateId,
            materialUnitTypeCode: request.MaterialUnitTypeCode,
            materialUnitTypeName: request.MaterialUnitTypeName,
            isSynthetic: request.IsSynthetic,
            sortOrder: request.SortOrder ?? 0,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.MaterialUnitTypeDefinitions.Add(type);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/configuration/material-unit-types/{type.Id}", new
        {
            type.Id,
            type.IndustryTemplateId,
            type.MaterialUnitTypeCode,
            type.MaterialUnitTypeName
        });
    }

    private static async Task<IResult> GetOperationDefinitionsAsync(
        Guid? industryTemplateId,
        string? category,
        bool? activeOnly,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.OperationDefinitions.AsNoTracking();

        if (industryTemplateId.HasValue)
            query = query.Where(x => x.IndustryTemplateId == industryTemplateId.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.OperationCategory == category);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsDeleted);

        var result = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.OperationCode)
            .Select(x => new
            {
                x.Id,
                x.IndustryTemplateId,
                x.OperationCode,
                x.OperationName,
                x.OperationCategory,
                x.Description,
                x.SortOrder,
                x.IsActive,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateOperationDefinitionAsync(
        CreateOperationDefinitionRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var templateExists = await dbContext.IndustryTemplates
            .AnyAsync(x => x.Id == request.IndustryTemplateId, cancellationToken);

        if (!templateExists)
            return Results.BadRequest(new { message = "Industry template does not exist." });

        var exists = await dbContext.OperationDefinitions.AnyAsync(x =>
            x.IndustryTemplateId == request.IndustryTemplateId &&
            x.OperationCode == request.OperationCode,
            cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Operation code already exists for this template." });

        var operation = new OperationDefinition(
            industryTemplateId: request.IndustryTemplateId,
            operationCode: request.OperationCode,
            operationName: request.OperationName,
            isSynthetic: request.IsSynthetic,
            operationCategory: request.OperationCategory,
            sortOrder: request.SortOrder ?? 0,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.OperationDefinitions.Add(operation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/configuration/operation-definitions/{operation.Id}", new
        {
            operation.Id,
            operation.IndustryTemplateId,
            operation.OperationCode,
            operation.OperationName
        });
    }

    private static async Task<IResult> GetRoutesAsync(
        Guid? industryTemplateId,
        string? productFamily,
        bool? activeOnly,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Routes.AsNoTracking();

        if (industryTemplateId.HasValue)
            query = query.Where(x => x.IndustryTemplateId == industryTemplateId.Value);

        if (!string.IsNullOrWhiteSpace(productFamily))
            query = query.Where(x => x.ProductFamily == productFamily);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsDeleted);

        var result = await query
            .OrderBy(x => x.RouteCode)
            .Select(x => new
            {
                x.Id,
                x.IndustryTemplateId,
                x.RouteCode,
                x.RouteName,
                x.ProductFamily,
                x.Description,
                x.IsActive,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateRouteAsync(
        CreateRouteRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var templateExists = await dbContext.IndustryTemplates
            .AnyAsync(x => x.Id == request.IndustryTemplateId, cancellationToken);

        if (!templateExists)
            return Results.BadRequest(new { message = "Industry template does not exist." });

        var exists = await dbContext.Routes.AnyAsync(x =>
            x.IndustryTemplateId == request.IndustryTemplateId &&
            x.RouteCode == request.RouteCode,
            cancellationToken);

        if (exists)
            return Results.Conflict(new { message = "Route code already exists for this template." });

        var route = new RouteEntity(
            industryTemplateId: request.IndustryTemplateId,
            routeCode: request.RouteCode,
            routeName: request.RouteName,
            isSynthetic: request.IsSynthetic,
            productFamily: request.ProductFamily,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/configuration/routes/{route.Id}", new
        {
            route.Id,
            route.IndustryTemplateId,
            route.RouteCode,
            route.RouteName
        });
    }

    private static async Task<IResult> GetRouteStepsAsync(
        Guid? routeId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RouteSteps.AsNoTracking();

        if (routeId.HasValue)
            query = query.Where(x => x.RouteId == routeId.Value);

        var result = await query
            .OrderBy(x => x.SequenceNo)
            .Select(x => new
            {
                x.Id,
                x.RouteId,
                x.OperationDefinitionId,
                x.SequenceNo,
                x.ExpectedMaterialUnitType,
                x.IsRequired,
                x.Description,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateRouteStepAsync(
        CreateRouteStepRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var routeExists = await dbContext.Routes
            .AnyAsync(x => x.Id == request.RouteId, cancellationToken);

        if (!routeExists)
            return Results.BadRequest(new { message = "Route does not exist." });

        var operationExists = await dbContext.OperationDefinitions
            .AnyAsync(x => x.Id == request.OperationDefinitionId, cancellationToken);

        if (!operationExists)
            return Results.BadRequest(new { message = "Operation definition does not exist." });

        var sequenceExists = await dbContext.RouteSteps.AnyAsync(x =>
            x.RouteId == request.RouteId &&
            x.SequenceNo == request.SequenceNo,
            cancellationToken);

        if (sequenceExists)
            return Results.Conflict(new { message = "Route step sequence already exists for this route." });

        var step = new RouteStep(
            routeId: request.RouteId,
            operationDefinitionId: request.OperationDefinitionId,
            sequenceNo: request.SequenceNo,
            isSynthetic: request.IsSynthetic,
            expectedMaterialUnitType: request.ExpectedMaterialUnitType,
            isRequired: request.IsRequired ?? true,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.RouteSteps.Add(step);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/configuration/route-steps/{step.Id}", new
        {
            step.Id,
            step.RouteId,
            step.OperationDefinitionId,
            step.SequenceNo
        });
    }


    private static async Task<IResult> ActivateIndustryTemplateAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.IndustryTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Industry template not found." });
        entity.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.TemplateCode, entity.IsActive });
    }

    private static async Task<IResult> DeactivateIndustryTemplateAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.IndustryTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Industry template not found." });
        entity.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.TemplateCode, entity.IsActive });
    }

    private static async Task<IResult> ActivateMaterialUnitTypeAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.MaterialUnitTypeDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Material unit type not found." });
        entity.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.MaterialUnitTypeCode, entity.IsActive });
    }

    private static async Task<IResult> DeactivateMaterialUnitTypeAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.MaterialUnitTypeDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Material unit type not found." });
        entity.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.MaterialUnitTypeCode, entity.IsActive });
    }

    private static async Task<IResult> ActivateOperationDefinitionAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.OperationDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Operation definition not found." });
        entity.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.OperationCode, entity.IsActive });
    }

    private static async Task<IResult> DeactivateOperationDefinitionAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.OperationDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Operation definition not found." });
        entity.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.OperationCode, entity.IsActive });
    }

    private static async Task<IResult> ActivateRouteAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Routes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Route not found." });
        entity.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.RouteCode, entity.IsActive });
    }

    private static async Task<IResult> DeactivateRouteAsync(Guid id, PlantProcessDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Routes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound(new { message = "Route not found." });
        entity.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.RouteCode, entity.IsActive });
    }

    public sealed record CreateIndustryTemplateRequest(
        string TemplateCode,
        string TemplateName,
        string IndustryName,
        string? Version,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateMaterialUnitTypeRequest(
        Guid IndustryTemplateId,
        string MaterialUnitTypeCode,
        string MaterialUnitTypeName,
        int? SortOrder,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateOperationDefinitionRequest(
        Guid IndustryTemplateId,
        string OperationCode,
        string OperationName,
        string? OperationCategory,
        int? SortOrder,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateRouteRequest(
        Guid IndustryTemplateId,
        string RouteCode,
        string RouteName,
        string? ProductFamily,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateRouteStepRequest(
        Guid RouteId,
        Guid OperationDefinitionId,
        int SequenceNo,
        string? ExpectedMaterialUnitType,
        bool? IsRequired,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);
}

