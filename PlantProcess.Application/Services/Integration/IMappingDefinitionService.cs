using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface IMappingDefinitionService
{
    Task<ApplicationResult<Guid>> CreateAsync(
        CreateMappingDefinitionCommand command,
        CancellationToken cancellationToken);
}

internal sealed class NotImplementedMappingDefinitionService : IMappingDefinitionService
{
    public Task<ApplicationResult<Guid>> CreateAsync(
        CreateMappingDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult<Guid>.Failure(
                ApplicationError.NotImplemented(nameof(IMappingDefinitionService))));
    }
}