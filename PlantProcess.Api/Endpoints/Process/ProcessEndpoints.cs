using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Process;

public static class ProcessEndpoints
{
    public static IEndpointRouteBuilder MapProcessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/process")
            .WithTags("Process");

        // ------------------------------------------------------------
        // Process Step Executions
        // ------------------------------------------------------------
        group.MapGet("/steps", async (
            Guid? materialUnitId,
            Guid? equipmentId,
            string? operationType,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.ProcessStepExecutions.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (equipmentId.HasValue)
                query = query.Where(x => x.EquipmentId == equipmentId.Value);

            if (!string.IsNullOrWhiteSpace(operationType))
                query = query.Where(x => x.OperationType == operationType);

            var result = await query
                .OrderBy(x => x.StartedAtUtc)
                .Take(take ?? 200)
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

            return Results.Ok(result);
        });

        group.MapGet("/steps/{id:guid}", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(x => x.Id == id)
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
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/steps", async (
            CreateProcessStepExecutionRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var materialExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

            if (!materialExists)
                return Results.BadRequest(new { message = "MaterialUnit does not exist." });

            if (request.EquipmentId.HasValue)
            {
                var equipmentExists = await dbContext.Equipment
                    .AnyAsync(x => x.Id == request.EquipmentId.Value, cancellationToken);

                if (!equipmentExists)
                    return Results.BadRequest(new { message = "Equipment does not exist." });
            }

            if (request.OperationDefinitionId.HasValue)
            {
                var operationExists = await dbContext.OperationDefinitions
                    .AnyAsync(x => x.Id == request.OperationDefinitionId.Value, cancellationToken);

                if (!operationExists)
                    return Results.BadRequest(new { message = "OperationDefinition does not exist." });
            }

            var step = new ProcessStepExecution(
                materialUnitId: request.MaterialUnitId,
                operationType: request.OperationType,
                startedAtUtc: request.StartedAtUtc,
                endedAtUtc: request.EndedAtUtc,
                isSynthetic: request.IsSynthetic,
                equipmentId: request.EquipmentId,
                operationCode: request.OperationCode,
                operationDefinitionId: request.OperationDefinitionId,
                crewCode: request.CrewCode,
                executionStatus: request.ExecutionStatus,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId,
                plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
                plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

            dbContext.ProcessStepExecutions.Add(step);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/process/steps/{step.Id}", new
            {
                step.Id,
                step.MaterialUnitId,
                step.OperationType,
                step.OperationDefinitionId,
                step.ExecutionStatus
            });
        });


        group.MapPatch("/steps/{id:guid}/complete", async (
            Guid id,
            CompleteProcessStepRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var step = await dbContext.ProcessStepExecutions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (step is null)
                return Results.NotFound(new { message = "ProcessStepExecution not found." });

            try
            {
                step.Complete(request.EndedAtUtc, request.ExecutionStatus);
                await dbContext.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { step.Id, step.MaterialUnitId, step.ExecutionStatus, step.EndedAtUtc, step.EndedAtLocal });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPatch("/steps/{id:guid}/abort", async (
            Guid id,
            AbortProcessStepRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var step = await dbContext.ProcessStepExecutions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (step is null)
                return Results.NotFound(new { message = "ProcessStepExecution not found." });

            try
            {
                step.Abort(request.EndedAtUtc, request.Reason);
                await dbContext.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { step.Id, step.MaterialUnitId, step.ExecutionStatus, step.EndedAtUtc, step.EndedAtLocal, request.Reason });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapDelete("/steps/{id:guid}", async (
            Guid id,
            SoftDeleteProcessRequest? request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var step = await dbContext.ProcessStepExecutions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (step is null)
                return Results.NotFound(new { message = "ProcessStepExecution not found." });

            step.SoftDelete(request?.Reason ?? "Soft-deleted via API.");
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { step.Id, step.MaterialUnitId, step.IsDeleted, step.DeletedAtUtc });
        });

        // ------------------------------------------------------------
        // Parameter Definitions
        // ------------------------------------------------------------
        group.MapGet("/parameters/definitions", async (
            string? category,
            string? industryTemplate,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.ParameterDefinitions.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(x => x.ParameterCategory == category);

            if (!string.IsNullOrWhiteSpace(industryTemplate))
                query = query.Where(x => x.IndustryTemplate == industryTemplate);

            var result = await query
                .OrderBy(x => x.ParameterCode)
                .Select(x => new
                {
                    x.Id,
                    x.ParameterCode,
                    x.ParameterName,
                    x.ValueType,
                    x.UnitOfMeasure,
                    x.ParameterCategory,
                    x.IndustryTemplate,
                    x.ExpectedMinValue,
                    x.ExpectedMaxValue,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapPost("/parameters/definitions", async (
            CreateParameterDefinitionRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var exists = await dbContext.ParameterDefinitions
                .AnyAsync(x =>
                    x.ParameterCode == request.ParameterCode &&
                    x.IndustryTemplate == request.IndustryTemplate,
                    cancellationToken);

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

            return Results.Created($"/process/parameters/definitions/{definition.Id}", new
            {
                definition.Id,
                definition.ParameterCode,
                definition.ParameterName
            });
        });

        // ------------------------------------------------------------
        // Parameter Observations
        // ------------------------------------------------------------
        group.MapGet("/parameters/observations", async (
            Guid? materialUnitId,
            Guid? parameterDefinitionId,
            Guid? processStepExecutionId,
            Guid? equipmentId,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.ParameterObservations.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (parameterDefinitionId.HasValue)
                query = query.Where(x => x.ParameterDefinitionId == parameterDefinitionId.Value);

            if (processStepExecutionId.HasValue)
                query = query.Where(x => x.ProcessStepExecutionId == processStepExecutionId.Value);

            if (equipmentId.HasValue)
                query = query.Where(x => x.EquipmentId == equipmentId.Value);

            if (fromUtc.HasValue)
                query = query.Where(x => x.ObservedAtUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                query = query.Where(x => x.ObservedAtUtc <= toUtc.Value);

            var result = await query
                .OrderByDescending(x => x.ObservedAtUtc)
                .Take(take ?? 500)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.ProcessStepExecutionId,
                    x.ParameterDefinitionId,
                    x.EquipmentId,
                    x.ObservedAtUtc,
                    x.ObservedAtLocal,
                    x.PlantTimeZoneId,
                    x.PlantUtcOffsetMinutes,
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

            return Results.Ok(result);
        });

        group.MapPost("/parameters/observations", async (
            CreateParameterObservationRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var materialExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

            if (!materialExists)
                return Results.BadRequest(new { message = "MaterialUnit does not exist." });

            var parameterExists = await dbContext.ParameterDefinitions
                .AnyAsync(x => x.Id == request.ParameterDefinitionId, cancellationToken);

            if (!parameterExists)
                return Results.BadRequest(new { message = "ParameterDefinition does not exist." });

            if (request.ProcessStepExecutionId.HasValue)
            {
                var stepExists = await dbContext.ProcessStepExecutions
                    .AnyAsync(x => x.Id == request.ProcessStepExecutionId.Value, cancellationToken);

                if (!stepExists)
                    return Results.BadRequest(new { message = "ProcessStepExecution does not exist." });
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

            return Results.Created($"/process/parameters/observations/{observation.Id}", new
            {
                observation.Id,
                observation.MaterialUnitId,
                observation.ParameterDefinitionId,
                observation.ObservedAtUtc
            });
        });

        // ------------------------------------------------------------
        // Process Events
        // ------------------------------------------------------------
        group.MapGet("/events", async (
            Guid? materialUnitId,
            Guid? equipmentId,
            string? eventType,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.ProcessEvents.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (equipmentId.HasValue)
                query = query.Where(x => x.EquipmentId == equipmentId.Value);

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(x => x.EventType == eventType);

            var result = await query
                .OrderByDescending(x => x.EventAtUtc)
                .Take(take ?? 200)
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

            return Results.Ok(result);
        });

        group.MapPost("/events", async (
     CreateProcessEventRequest request,
     PlantProcessDbContext dbContext,
     CancellationToken cancellationToken) =>
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

            return Results.Created($"/process/events/{processEvent.Id}", new
            {
                processEvent.Id,
                processEvent.EventType,
                processEvent.EventAtUtc
            });
        });

        // ------------------------------------------------------------
        // Downtime Events
        // ------------------------------------------------------------
        group.MapGet("/downtime-events", async (
            Guid? materialUnitId,
            Guid? equipmentId,
            string? downtimeType,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.DowntimeEvents.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (equipmentId.HasValue)
                query = query.Where(x => x.EquipmentId == equipmentId.Value);

            if (!string.IsNullOrWhiteSpace(downtimeType))
                query = query.Where(x => x.DowntimeType == downtimeType);

            var result = await query
                .OrderByDescending(x => x.StartedAtUtc)
                .Take(take ?? 200)
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

            return Results.Ok(result);
        });

        group.MapPost("/downtime-events", async (
            CreateDowntimeEventRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
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

            dbContext.DowntimeEvents.Add(downtimeEvent);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/process/downtime-events/{downtimeEvent.Id}", new
            {
                downtimeEvent.Id,
                downtimeEvent.DowntimeType,
                downtimeEvent.StartedAtUtc,
                downtimeEvent.EndedAtUtc
            });
        });

        return app;
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
                return Results.BadRequest(new { message = "MaterialUnit does not exist." });
        }

        if (processStepExecutionId.HasValue)
        {
            var stepExists = await dbContext.ProcessStepExecutions
                .AnyAsync(x => x.Id == processStepExecutionId.Value, cancellationToken);

            if (!stepExists)
                return Results.BadRequest(new { message = "ProcessStepExecution does not exist." });
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

    public sealed record CreateProcessStepExecutionRequest(
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

    public sealed record CompleteProcessStepRequest(
        DateTime EndedAtUtc,
        string? ExecutionStatus);

    public sealed record AbortProcessStepRequest(
        DateTime EndedAtUtc,
        string? Reason);

    public sealed record SoftDeleteProcessRequest(string? Reason);

    public sealed record CreateParameterDefinitionRequest(
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

    public sealed record CreateParameterObservationRequest(
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

    public sealed record CreateProcessEventRequest(
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

    public sealed record CreateDowntimeEventRequest(
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
}
