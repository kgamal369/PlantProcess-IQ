using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Materials;

namespace PlantProcess.Application.Services.Materials;

public interface IGenealogyService
{
    Task<ApplicationResult<Guid>> CreateEdgeAsync(
        CreateGenealogyEdgeCommand command,
        CancellationToken cancellationToken);
}

internal sealed class NotImplementedGenealogyService : IGenealogyService
{
    public Task<ApplicationResult<Guid>> CreateEdgeAsync(
        CreateGenealogyEdgeCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult<Guid>.Failure(
                ApplicationError.NotImplemented(nameof(IGenealogyService))));
    }
}



