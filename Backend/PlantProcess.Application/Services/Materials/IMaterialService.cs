using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Materials;

namespace PlantProcess.Application.Services.Materials;

public interface IMaterialService
{
    Task<ApplicationResult<Guid>> CreateAsync(
        CreateMaterialCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> AddAliasAsync(
        AddMaterialAliasCommand command,
        CancellationToken cancellationToken);
}

internal sealed class NotImplementedMaterialService : IMaterialService
{
    public Task<ApplicationResult<Guid>> CreateAsync(
        CreateMaterialCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult<Guid>.Failure(
                ApplicationError.NotImplemented(nameof(IMaterialService))));
    }

    public Task<ApplicationResult<Guid>> AddAliasAsync(
        AddMaterialAliasCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ApplicationResult<Guid>.Failure(
                ApplicationError.NotImplemented(nameof(IMaterialService))));
    }
}


