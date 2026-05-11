using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Services.Integration;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Integration;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/integration")
            .WithTags("Integration");

        group.MapGet("/summary", GetIntegrationSummaryAsync);

        group.MapGet("/source-systems", GetSourceSystemsAsync);
        group.MapGet("/source-systems/{id:guid}", GetSourceSystemByIdAsync);
        group.MapPost("/source-systems", CreateSourceSystemAsync);

        group.MapGet("/import-batches", GetImportBatchesAsync);
        group.MapGet("/import-batches/{id:guid}", GetImportBatchByIdAsync);
        group.MapPost("/import-batches", CreateImportBatchAsync);
        group.MapPost("/import-batches/{id:guid}/mark-running", MarkImportBatchRunningAsync);
        group.MapPost("/import-batches/{id:guid}/mark-completed", MarkImportBatchCompletedAsync);
        group.MapPost("/import-batches/{id:guid}/mark-failed", MarkImportBatchFailedAsync);

        group.MapGet("/mapping-definitions", GetMappingDefinitionsAsync);
        group.MapGet("/mapping-definitions/{id:guid}", GetMappingDefinitionByIdAsync);
        group.MapPost("/mapping-definitions", CreateMappingDefinitionAsync);
        group.MapPatch("/mapping-definitions/{id:guid}/mapping-json", UpdateMappingDefinitionJsonAsync);

        return app;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Summary
    // ────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetIntegrationSummaryAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return Results.Ok(new
        {
            sourceSystems = await dbContext.SourceSystemDefinitions.CountAsync(cancellationToken),
            importBatches = await dbContext.ImportBatches.CountAsync(cancellationToken),
            mappingDefinitions = await dbContext.MappingDefinitions.CountAsync(cancellationToken)
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // Source Systems
    // ────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSourceSystemsAsync(
        string? type,
        bool? readOnlyOnly,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SourceSystemDefinitions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(x => x.SourceSystemType == type);

        if (readOnlyOnly == true)
            query = query.Where(x => x.IsReadOnlySource);

        var systems = await query
            .OrderBy(x => x.SourceSystemCode)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemCode,
                x.SourceSystemName,
                x.SourceSystemType,
                x.Description,
                x.IsReadOnlySource,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(systems);
    }

    private static async Task<IResult> GetSourceSystemByIdAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var system = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemCode,
                x.SourceSystemName,
                x.SourceSystemType,
                x.Description,
                x.IsReadOnlySource,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return system is null ? Results.NotFound() : Results.Ok(system);
    }

    private static async Task<IResult> CreateSourceSystemAsync(
        CreateSourceSystemRequest request,
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

        return Results.Created($"/integration/source-systems/{sourceSystem.Id}", new
        {
            sourceSystem.Id,
            sourceSystem.SourceSystemCode,
            sourceSystem.SourceSystemName,
            sourceSystem.SourceSystemType
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // Import Batches
    // ────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetImportBatchesAsync(
        Guid? sourceSystemDefinitionId,
        string? status,
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ImportBatches.AsNoTracking();

        if (sourceSystemDefinitionId.HasValue)
            query = query.Where(x => x.SourceSystemDefinitionId == sourceSystemDefinitionId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        var batches = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(take ?? 200)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemDefinitionId,
                x.ImportBatchCode,
                x.ImportType,
                x.Status,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.SourceObjectName,
                x.FileName,
                x.Checksum,
                x.RowCount,
                x.ErrorMessage,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(batches);
    }

    private static async Task<IResult> GetImportBatchByIdAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemDefinitionId,
                x.ImportBatchCode,
                x.ImportType,
                x.Status,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.SourceObjectName,
                x.FileName,
                x.Checksum,
                x.RowCount,
                x.ErrorMessage,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return batch is null ? Results.NotFound() : Results.Ok(batch);
    }

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

        return Results.Created($"/integration/import-batches/{importBatch.Id}", new
        {
            importBatch.Id,
            importBatch.ImportBatchCode,
            importBatch.ImportType,
            importBatch.Status
        });
    }

    private static async Task<IResult> MarkImportBatchRunningAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
            return Results.NotFound(new { message = "Import batch not found." });

        batch.MarkRunning();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { batch.Id, batch.ImportBatchCode, batch.Status });
    }

    private static async Task<IResult> MarkImportBatchCompletedAsync(
        Guid id,
        CompleteImportBatchRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.RowCount < 0)
            return Results.BadRequest(new { message = "Row count cannot be negative." });

        var batch = await dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
            return Results.NotFound(new { message = "Import batch not found." });

        batch.MarkCompleted(request.RowCount);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            batch.Id,
            batch.ImportBatchCode,
            batch.Status,
            batch.RowCount,
            batch.CompletedAtUtc
        });
    }

    private static async Task<IResult> MarkImportBatchFailedAsync(
        Guid id,
        FailImportBatchRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
            return Results.NotFound(new { message = "Import batch not found." });

        batch.MarkFailed(request.ErrorMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            batch.Id,
            batch.ImportBatchCode,
            batch.Status,
            batch.ErrorMessage,
            batch.CompletedAtUtc
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // Mapping Definitions — routed through IMappingDefinitionService (T-06)
    // ────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetMappingDefinitionsAsync(
        Guid? sourceSystemDefinitionId,
        string? targetEntityName,
        bool? activeOnly,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MappingDefinitions.AsNoTracking();

        if (sourceSystemDefinitionId.HasValue)
            query = query.Where(x => x.SourceSystemDefinitionId == sourceSystemDefinitionId.Value);

        if (!string.IsNullOrWhiteSpace(targetEntityName))
            query = query.Where(x => x.TargetEntityName == targetEntityName);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsDeleted);

        var mappings = await query
            .OrderBy(x => x.MappingCode)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemDefinitionId,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.MappingJson,
                x.MappingVersion,
                x.IsActive,
                x.Description,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(mappings);
    }

    private static async Task<IResult> GetMappingDefinitionByIdAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mapping = await dbContext.MappingDefinitions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemDefinitionId,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.MappingJson,
                x.MappingVersion,
                x.IsActive,
                x.Description,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return mapping is null ? Results.NotFound() : Results.Ok(mapping);
    }

    /// <summary>
    /// POST /integration/mapping-definitions
    /// Routes through IMappingDefinitionService — no direct DbContext writes.
    /// </summary>
    private static async Task<IResult> CreateMappingDefinitionAsync(
        CreateMappingDefinitionRequest request,
        IMappingDefinitionService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N");

        var command = new CreateMappingDefinitionCommand(
            SourceSystemDefinitionId: request.SourceSystemDefinitionId,
            MappingCode: request.MappingCode,
            MappingName: request.MappingName,
            SourceObjectName: request.SourceObjectName,
            TargetEntityName: request.TargetEntityName,
            MappingJson: request.MappingJson,
            MappingVersion: request.MappingVersion,
            Description: request.Description,
            Metadata: new CommandMetadata(
                IsSynthetic: request.IsSynthetic,
                SourceSystem: request.SourceSystem,
                SourceRecordId: request.SourceRecordId,
                CorrelationId: correlationId));

        var result = await service.CreateAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/integration/mapping-definitions/{id}", new
            {
                id,
                request.MappingCode,
                request.MappingName,
                request.TargetEntityName,
                mappingVersion = request.MappingVersion ?? "v1"
            }));
    }

    /// <summary>
    /// PATCH /integration/mapping-definitions/{id}/mapping-json
    /// Updates the field-map JSON on an existing mapping definition.
    /// </summary>
    private static async Task<IResult> UpdateMappingDefinitionJsonAsync(
        Guid id,
        UpdateMappingJsonRequest request,
        IMappingDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateMappingJsonAsync(
            id,
            request.MappingJson,
            request.MappingVersion ?? "v1",
            cancellationToken);

        return result.ToHttpResult(() => Results.Ok(new
        {
            mappingDefinitionId = id,
            request.MappingVersion,
            updatedAt = DateTime.UtcNow
        }));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Request records
    // ────────────────────────────────────────────────────────────────────────

    public sealed record CreateSourceSystemRequest(
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

    public sealed record CompleteImportBatchRequest(int RowCount);

    public sealed record FailImportBatchRequest(string ErrorMessage);

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

    public sealed record UpdateMappingJsonRequest(
        string MappingJson,
        string? MappingVersion);
}