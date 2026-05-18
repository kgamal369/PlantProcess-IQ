using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.SchemaConfiguration;

public sealed class SchemaConfigurationService : ISchemaConfigurationService
{
    private readonly IPlantProcessDbContext _dbContext;

    public SchemaConfigurationService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<IReadOnlyList<SchemaViewDefinitionDto>>> GetSchemaViewsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var rows = await query
            .OrderBy(x => x.ViewKind)
            .ThenBy(x => x.SchemaViewCode)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<SchemaViewDefinitionDto>>.Success(rows);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> GetSchemaViewByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."))
            : ApplicationResult<SchemaViewDefinitionDto>.Success(row);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> CreateSchemaViewAsync(
        CreateSchemaViewDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(validationError);

        var duplicate = await _dbContext.SchemaViewDefinitions
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.SchemaViewCode == request.SchemaViewCode.Trim(),
                cancellationToken);

        if (duplicate)
        {
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(
                ApplicationError.Conflict("Schema view code already exists."));
        }

        if (request.PrimarySourceDatasetDefinitionId.HasValue)
        {
            var datasetExists = await _dbContext.SourceDatasetDefinitions
                .AnyAsync(x =>
                    x.Id == request.PrimarySourceDatasetDefinitionId.Value &&
                    !x.IsDeleted,
                    cancellationToken);

            if (!datasetExists)
            {
                return ApplicationResult<SchemaViewDefinitionDto>.Failure(
                    ApplicationError.NotFound("Primary source dataset definition not found."));
            }
        }

        var entity = new SchemaViewDefinition(
            schemaViewCode: request.SchemaViewCode,
            schemaViewName: request.SchemaViewName,
            viewKind: request.ViewKind,
            sqlText: request.SqlText,
            isSynthetic: request.IsSynthetic,
            primarySourceDatasetDefinitionId: request.PrimarySourceDatasetDefinitionId,
            sourceDatasetIdsJson: request.SourceDatasetIdsJson,
            maxPreviewRows: request.MaxPreviewRows ?? 100,
            timeoutSeconds: request.TimeoutSeconds ?? 15,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        _dbContext.SchemaViewDefinitions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSchemaViewByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> UpdateSchemaViewAsync(
        Guid id,
        UpdateSchemaViewDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SchemaViewDefinitions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."));

        if (string.IsNullOrWhiteSpace(request.SchemaViewName))
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.Validation("SchemaViewName is required."));

        if (string.IsNullOrWhiteSpace(request.ViewKind))
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.Validation("ViewKind is required."));

        if (string.IsNullOrWhiteSpace(request.SqlText))
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.Validation("SqlText is required."));

        if (request.PrimarySourceDatasetDefinitionId.HasValue)
        {
            var datasetExists = await _dbContext.SourceDatasetDefinitions
                .AnyAsync(x =>
                    x.Id == request.PrimarySourceDatasetDefinitionId.Value &&
                    !x.IsDeleted,
                    cancellationToken);

            if (!datasetExists)
                return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Primary source dataset definition not found."));
        }

        entity.Update(
            schemaViewName: request.SchemaViewName,
            viewKind: request.ViewKind,
            sqlText: request.SqlText,
            primarySourceDatasetDefinitionId: request.PrimarySourceDatasetDefinitionId,
            sourceDatasetIdsJson: request.SourceDatasetIdsJson,
            maxPreviewRows: request.MaxPreviewRows ?? entity.MaxPreviewRows,
            timeoutSeconds: request.TimeoutSeconds ?? entity.TimeoutSeconds,
            description: request.Description);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSchemaViewByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> MarkSchemaViewValidationAsync(
        Guid id,
        bool isSuccess,
        string message,
        string? outputSchemaJson,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SchemaViewDefinitions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."));

        entity.MarkValidationResult(isSuccess, message, outputSchemaJson);

        if (isSuccess)
            entity.Approve();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSchemaViewByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> ApproveSchemaViewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SchemaViewDefinitions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."));

        if (entity.LastValidationStatus != "Success")
        {
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(
                ApplicationError.BusinessRule("Schema view must be successfully previewed/validated before approval."));
        }

        entity.Approve();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSchemaViewByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> ActivateSchemaViewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SchemaViewDefinitions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."));

        entity.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSchemaViewByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<SchemaViewDefinitionDto>> DeactivateSchemaViewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SchemaViewDefinitions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<SchemaViewDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."));

        entity.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSchemaViewByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<IReadOnlyList<KpiDefinitionDto>>> GetKpisAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.KpiDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var rows = await query
            .OrderBy(x => x.KpiCategory)
            .ThenBy(x => x.KpiCode)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<KpiDefinitionDto>>.Success(rows);
    }

    public async Task<ApplicationResult<KpiDefinitionDto>> CreateKpiAsync(
        CreateKpiDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.KpiCode))
            return ApplicationResult<KpiDefinitionDto>.Failure(ApplicationError.Validation("KpiCode is required."));

        if (string.IsNullOrWhiteSpace(request.KpiName))
            return ApplicationResult<KpiDefinitionDto>.Failure(ApplicationError.Validation("KpiName is required."));

        if (string.IsNullOrWhiteSpace(request.KpiCategory))
            return ApplicationResult<KpiDefinitionDto>.Failure(ApplicationError.Validation("KpiCategory is required."));

        if (string.IsNullOrWhiteSpace(request.ValueExpression))
            return ApplicationResult<KpiDefinitionDto>.Failure(ApplicationError.Validation("ValueExpression is required."));

        var duplicate = await _dbContext.KpiDefinitions
            .AnyAsync(x => !x.IsDeleted && x.KpiCode == request.KpiCode.Trim(), cancellationToken);

        if (duplicate)
            return ApplicationResult<KpiDefinitionDto>.Failure(ApplicationError.Conflict("KPI code already exists."));

        if (request.SchemaViewDefinitionId.HasValue)
        {
            var viewExists = await _dbContext.SchemaViewDefinitions
                .AnyAsync(x =>
                    x.Id == request.SchemaViewDefinitionId.Value &&
                    !x.IsDeleted,
                    cancellationToken);

            if (!viewExists)
                return ApplicationResult<KpiDefinitionDto>.Failure(ApplicationError.NotFound("Schema view definition not found."));
        }

        var entity = new KpiDefinition(
            kpiCode: request.KpiCode,
            kpiName: request.KpiName,
            kpiCategory: request.KpiCategory,
            valueExpression: request.ValueExpression,
            isSynthetic: request.IsSynthetic,
            schemaViewDefinitionId: request.SchemaViewDefinitionId,
            unit: request.Unit,
            dimensionExpression: request.DimensionExpression,
            filterExpression: request.FilterExpression,
            aggregationType: request.AggregationType,
            kpiOptionsJson: request.KpiOptionsJson,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        _dbContext.KpiDefinitions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.KpiDefinitions
            .AsNoTracking()
            .Where(x => x.Id == entity.Id)
            .Select(x => ToDto(x))
            .FirstAsync(cancellationToken);

        return ApplicationResult<KpiDefinitionDto>.Success(created);
    }

    private static ApplicationError? ValidateCreateRequest(CreateSchemaViewDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SchemaViewCode))
            return ApplicationError.Validation("SchemaViewCode is required.");

        if (string.IsNullOrWhiteSpace(request.SchemaViewName))
            return ApplicationError.Validation("SchemaViewName is required.");

        if (string.IsNullOrWhiteSpace(request.ViewKind))
            return ApplicationError.Validation("ViewKind is required.");

        if (string.IsNullOrWhiteSpace(request.SqlText))
            return ApplicationError.Validation("SqlText is required.");

        return null;
    }

    private static SchemaViewDefinitionDto ToDto(SchemaViewDefinition entity)
    {
        return new SchemaViewDefinitionDto(
            entity.Id,
            entity.SchemaViewCode,
            entity.SchemaViewName,
            entity.ViewKind,
            entity.PrimarySourceDatasetDefinitionId,
            entity.SqlText,
            entity.SourceDatasetIdsJson,
            entity.OutputSchemaJson,
            entity.MaxPreviewRows,
            entity.TimeoutSeconds,
            entity.IsApproved,
            entity.IsActive,
            entity.LastValidatedAtUtc,
            entity.LastValidationStatus,
            entity.LastValidationMessage,
            entity.Description,
            entity.IsSynthetic,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    private static KpiDefinitionDto ToDto(KpiDefinition entity)
    {
        return new KpiDefinitionDto(
            entity.Id,
            entity.SchemaViewDefinitionId,
            entity.KpiCode,
            entity.KpiName,
            entity.KpiCategory,
            entity.ValueExpression,
            entity.Unit,
            entity.DimensionExpression,
            entity.FilterExpression,
            entity.AggregationType,
            entity.KpiOptionsJson,
            entity.IsActive,
            entity.Description,
            entity.IsSynthetic,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }
}



