using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

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