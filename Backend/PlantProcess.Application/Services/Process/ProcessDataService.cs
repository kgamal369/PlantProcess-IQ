using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Process;
using PlantProcess.Domain.Entities.Process;

namespace PlantProcess.Application.Services.Process;

public sealed class ProcessDataService : IProcessDataService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IPlantTimeContextResolver _timeContextResolver;
    private readonly ILogger<ProcessDataService> _logger;

    public ProcessDataService(
        IPlantProcessDbContext dbContext,
        IPlantTimeContextResolver timeContextResolver,
        ILogger<ProcessDataService> logger)
    {
        _dbContext = dbContext;
        _timeContextResolver = timeContextResolver;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> AddProcessStepAsync(
        AddProcessStepCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit ID is required."));

        if (string.IsNullOrWhiteSpace(command.OperationType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Operation type is required."));

        if (command.StartedAtUtc.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("StartedAtUtc must be UTC."));

        if (command.EndedAtUtc.HasValue && command.EndedAtUtc.Value.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("EndedAtUtc must be UTC."));

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == command.MaterialUnitId)
            .Select(x => new { x.Id, x.SiteId })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        if (command.EquipmentId.HasValue)
        {
            var equipmentExists = await _dbContext.Equipment
                .AnyAsync(x => x.Id == command.EquipmentId.Value, cancellationToken);

            if (!equipmentExists)
                return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Equipment does not exist."));
        }

        if (command.OperationDefinitionId.HasValue)
        {
            var operationExists = await _dbContext.OperationDefinitions
                .AnyAsync(x => x.Id == command.OperationDefinitionId.Value, cancellationToken);

            if (!operationExists)
                return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Operation definition does not exist."));
        }

        var siteTimeZoneId = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == material.SiteId)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeContext = _timeContextResolver.Resolve(
            command.PlantTimeZoneId ?? siteTimeZoneId,
            command.StartedAtUtc);

        var processStep = new ProcessStepExecution(
            materialUnitId: command.MaterialUnitId,
            operationType: command.OperationType,
            startedAtUtc: command.StartedAtUtc,
            endedAtUtc: command.EndedAtUtc,
            isSynthetic: command.Metadata.IsSynthetic,
            equipmentId: command.EquipmentId,
            operationCode: command.OperationCode,
            operationDefinitionId: command.OperationDefinitionId,
            crewCode: command.CrewCode,
            executionStatus: command.ExecutionStatus,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.ProcessStepExecutions.Add(processStep);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added process step. ProcessStepExecutionId={ProcessStepExecutionId}, MaterialUnitId={MaterialUnitId}, OperationType={OperationType}, OperationCode={OperationCode}, OperationDefinitionId={OperationDefinitionId}, CorrelationId={CorrelationId}",
            processStep.Id,
            processStep.MaterialUnitId,
            processStep.OperationType,
            processStep.OperationCode,
            processStep.OperationDefinitionId,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(processStep.Id);
    }

    public async Task<ApplicationResult<Guid>> AddParameterDefinitionAsync(
        AddParameterDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ParameterCode))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Parameter code is required."));

        if (string.IsNullOrWhiteSpace(command.ParameterName))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Parameter name is required."));

        var normalizedCode = command.ParameterCode.Trim();
        var normalizedTemplate = command.IndustryTemplate?.Trim();

        var exists = await _dbContext.ParameterDefinitions.AnyAsync(x =>
                x.ParameterCode == normalizedCode &&
                x.IndustryTemplate == normalizedTemplate,
            cancellationToken);

        if (exists)
        {
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Parameter '{normalizedCode}' already exists for this industry template."));
        }

        var definition = new ParameterDefinition(
            parameterCode: normalizedCode,
            parameterName: command.ParameterName,
            valueType: command.ValueType,
            unitOfMeasure: command.UnitOfMeasure,
            parameterCategory: command.ParameterCategory,
            industryTemplate: command.IndustryTemplate,
            isSynthetic: command.Metadata.IsSynthetic,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        definition.SetExpectedRange(command.ExpectedMinValue, command.ExpectedMaxValue);

        _dbContext.ParameterDefinitions.Add(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added parameter definition. ParameterDefinitionId={ParameterDefinitionId}, ParameterCode={ParameterCode}, IndustryTemplate={IndustryTemplate}, CorrelationId={CorrelationId}",
            definition.Id,
            definition.ParameterCode,
            definition.IndustryTemplate,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(definition.Id);
    }

    public async Task<ApplicationResult<Guid>> AddParameterObservationAsync(
        AddParameterObservationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit ID is required."));

        if (command.ParameterDefinitionId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Parameter definition ID is required."));

        if (command.ObservedAtUtc.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("ObservedAtUtc must be UTC."));

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == command.MaterialUnitId)
            .Select(x => new { x.Id, x.SiteId })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        var parameter = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.ParameterDefinitionId, cancellationToken);

        if (parameter is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Parameter definition does not exist."));

        if (command.ProcessStepExecutionId.HasValue)
        {
            var stepExists = await _dbContext.ProcessStepExecutions
                .AnyAsync(x => x.Id == command.ProcessStepExecutionId.Value, cancellationToken);

            if (!stepExists)
                return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Process step execution does not exist."));
        }

        if (command.EquipmentId.HasValue)
        {
            var equipmentExists = await _dbContext.Equipment
                .AnyAsync(x => x.Id == command.EquipmentId.Value, cancellationToken);

            if (!equipmentExists)
                return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Equipment does not exist."));
        }

        var valueValidation = ValidateValueAgainstParameterType(parameter.ValueType, command);

        if (valueValidation is not null)
            return ApplicationResult<Guid>.Failure(valueValidation);

        var siteTimeZoneId = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == material.SiteId)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeContext = _timeContextResolver.Resolve(
            command.PlantTimeZoneId ?? siteTimeZoneId,
            command.ObservedAtUtc);

        var observation = new ParameterObservation(
            materialUnitId: command.MaterialUnitId,
            parameterDefinitionId: command.ParameterDefinitionId,
            observedAtUtc: command.ObservedAtUtc,
            isSynthetic: command.Metadata.IsSynthetic,
            numericValue: command.NumericValue,
            textValue: command.TextValue,
            booleanValue: command.BooleanValue,
            unitOfMeasure: command.UnitOfMeasure,
            processStepExecutionId: command.ProcessStepExecutionId,
            equipmentId: command.EquipmentId,
            qualityFlag: command.QualityFlag,
            rawValue: command.RawValue,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.ParameterObservations.Add(observation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added parameter observation. ParameterObservationId={ParameterObservationId}, MaterialUnitId={MaterialUnitId}, ParameterDefinitionId={ParameterDefinitionId}, ObservedAtUtc={ObservedAtUtc}, CorrelationId={CorrelationId}",
            observation.Id,
            observation.MaterialUnitId,
            observation.ParameterDefinitionId,
            observation.ObservedAtUtc,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(observation.Id);
    }

    public async Task<ApplicationResult<Guid>> AddProcessEventAsync(
        AddProcessEventCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.EventType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Event type is required."));

        if (command.EventAtUtc.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("EventAtUtc must be UTC."));

        var contextResult = await ValidateOptionalReferencesAsync(
            command.MaterialUnitId,
            command.ProcessStepExecutionId,
            command.EquipmentId,
            cancellationToken);

        if (contextResult.IsFailure)
            return ApplicationResult<Guid>.Failure(contextResult.Error!);

        var timeContext = _timeContextResolver.Resolve(
            command.PlantTimeZoneId,
            command.EventAtUtc);

        var processEvent = new ProcessEvent(
            eventType: command.EventType,
            eventAtUtc: command.EventAtUtc,
            isSynthetic: command.Metadata.IsSynthetic,
            materialUnitId: command.MaterialUnitId,
            processStepExecutionId: command.ProcessStepExecutionId,
            equipmentId: command.EquipmentId,
            eventValue: command.EventValue,
            description: command.Description,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.ProcessEvents.Add(processEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<Guid>.Success(processEvent.Id);
    }

    public async Task<ApplicationResult<Guid>> AddDowntimeEventAsync(
        AddDowntimeEventCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DowntimeType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Downtime type is required."));

        if (command.StartedAtUtc.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("StartedAtUtc must be UTC."));

        if (command.EndedAtUtc.HasValue && command.EndedAtUtc.Value.Kind != DateTimeKind.Utc)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("EndedAtUtc must be UTC."));

        var contextResult = await ValidateOptionalReferencesAsync(
            command.MaterialUnitId,
            command.ProcessStepExecutionId,
            command.EquipmentId,
            cancellationToken);

        if (contextResult.IsFailure)
            return ApplicationResult<Guid>.Failure(contextResult.Error!);

        var timeContext = _timeContextResolver.Resolve(
            command.PlantTimeZoneId,
            command.StartedAtUtc);

        var downtime = new DowntimeEvent(
            startedAtUtc: command.StartedAtUtc,
            downtimeType: command.DowntimeType,
            isSynthetic: command.Metadata.IsSynthetic,
            endedAtUtc: command.EndedAtUtc,
            materialUnitId: command.MaterialUnitId,
            processStepExecutionId: command.ProcessStepExecutionId,
            equipmentId: command.EquipmentId,
            reasonCode: command.ReasonCode,
            description: command.Description,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.DowntimeEvents.Add(downtime);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<Guid>.Success(downtime.Id);
    }

    private static ApplicationError? ValidateValueAgainstParameterType(
        string valueType,
        AddParameterObservationCommand command)
    {
        var normalizedType = string.IsNullOrWhiteSpace(valueType)
            ? "Numeric"
            : valueType.Trim();

        var hasNumeric = command.NumericValue.HasValue;
        var hasText = !string.IsNullOrWhiteSpace(command.TextValue);
        var hasBoolean = command.BooleanValue.HasValue;

        if (!hasNumeric && !hasText && !hasBoolean)
            return ApplicationError.Validation("At least one parameter value must be provided.");

        return normalizedType switch
        {
            "Numeric" when !hasNumeric =>
                ApplicationError.Validation("Numeric parameter requires NumericValue."),

            "Boolean" when !hasBoolean =>
                ApplicationError.Validation("Boolean parameter requires BooleanValue."),

            "Text" or "Categorical" when !hasText =>
                ApplicationError.Validation("Text/Categorical parameter requires TextValue."),

            _ => null
        };
    }

    private async Task<ApplicationResult> ValidateOptionalReferencesAsync(
        Guid? materialUnitId,
        Guid? processStepExecutionId,
        Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        if (materialUnitId.HasValue)
        {
            var exists = await _dbContext.MaterialUnits
                .AnyAsync(x => x.Id == materialUnitId.Value, cancellationToken);

            if (!exists)
                return ApplicationResult.Failure(ApplicationError.NotFound("Material unit does not exist."));
        }

        if (processStepExecutionId.HasValue)
        {
            var exists = await _dbContext.ProcessStepExecutions
                .AnyAsync(x => x.Id == processStepExecutionId.Value, cancellationToken);

            if (!exists)
                return ApplicationResult.Failure(ApplicationError.NotFound("Process step execution does not exist."));
        }

        if (equipmentId.HasValue)
        {
            var exists = await _dbContext.Equipment
                .AnyAsync(x => x.Id == equipmentId.Value, cancellationToken);

            if (!exists)
                return ApplicationResult.Failure(ApplicationError.NotFound("Equipment does not exist."));
        }

        return ApplicationResult.Success();
    }
}


