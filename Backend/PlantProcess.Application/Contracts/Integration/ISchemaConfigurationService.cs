using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface ISchemaConfigurationService
{
    Task<ApplicationResult<IReadOnlyList<SchemaViewDefinitionDto>>> GetSchemaViewsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> GetSchemaViewByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> CreateSchemaViewAsync(
        CreateSchemaViewDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> UpdateSchemaViewAsync(
        Guid id,
        UpdateSchemaViewDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> MarkSchemaViewValidationAsync(
        Guid id,
        bool isSuccess,
        string message,
        string? outputSchemaJson,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> ApproveSchemaViewAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> ActivateSchemaViewAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SchemaViewDefinitionDto>> DeactivateSchemaViewAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<KpiDefinitionDto>>> GetKpisAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<KpiDefinitionDto>> CreateKpiAsync(
        CreateKpiDefinitionRequest request,
        CancellationToken cancellationToken);
}