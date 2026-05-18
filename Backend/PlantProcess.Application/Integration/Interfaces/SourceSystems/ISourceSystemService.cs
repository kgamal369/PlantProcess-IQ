using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.SourceSystems;

namespace PlantProcess.Application.Integration.Interfaces.SourceSystems;

public interface ISourceSystemService
{
    Task<ApplicationResult<Guid>> RegisterAsync(
        RegisterSourceSystemCommand command,
        CancellationToken cancellationToken);
}

internal sealed class NotImplementedSourceSystemService : ISourceSystemService
{
    public Task<ApplicationResult<Guid>> RegisterAsync(
        RegisterSourceSystemCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult<Guid>.Failure(
                ApplicationError.NotImplemented(nameof(ISourceSystemService))));
    }
}


