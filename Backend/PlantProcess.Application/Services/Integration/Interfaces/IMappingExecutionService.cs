using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration.Dtos;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IMappingExecutionService
{
    Task<ApplicationResult<MappingExecutionResult>> PreviewAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        CancellationToken cancellationToken);

    Task<ApplicationResult<MappingExecutionResult>> ExecuteAsync(
        Guid mappingDefinitionId,
        Guid importBatchId,
        int take,
        bool stopOnFirstError,
        CancellationToken cancellationToken);
}
