using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;

namespace PlantProcess.Application.Integration.Interfaces.Mapping;

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




