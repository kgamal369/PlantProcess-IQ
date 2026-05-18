using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Commands;

namespace PlantProcess.Application.Integration.Interfaces.Import;

public interface IImportBatchService
{
    Task<ApplicationResult<Guid>> CreateAsync(
        CreateImportBatchCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult> MarkRunningAsync(
        Guid importBatchId,
        CancellationToken cancellationToken);

    Task<ApplicationResult> MarkCompletedAsync(
        Guid importBatchId,
        int rowCount,
        CancellationToken cancellationToken);

    Task<ApplicationResult> MarkFailedAsync(
        Guid importBatchId,
        string errorMessage,
        CancellationToken cancellationToken);
}

internal sealed class NotImplementedImportBatchService : IImportBatchService
{
    public Task<ApplicationResult<Guid>> CreateAsync(
        CreateImportBatchCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult<Guid>.Failure(
                ApplicationError.NotImplemented(nameof(IImportBatchService))));
    }

    public Task<ApplicationResult> MarkRunningAsync(
        Guid importBatchId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult.Failure(
                ApplicationError.NotImplemented(nameof(IImportBatchService))));
    }

    public Task<ApplicationResult> MarkCompletedAsync(
        Guid importBatchId,
        int rowCount,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult.Failure(
                ApplicationError.NotImplemented(nameof(IImportBatchService))));
    }

    public Task<ApplicationResult> MarkFailedAsync(
        Guid importBatchId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult.Failure(
                ApplicationError.NotImplemented(nameof(IImportBatchService))));
    }

}



