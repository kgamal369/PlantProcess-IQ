using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Process;

namespace PlantProcess.Application.Services.Process;

public interface IProcessDataService
{
    Task<ApplicationResult<Guid>> AddProcessStepAsync(
        AddProcessStepCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> AddParameterDefinitionAsync(
        AddParameterDefinitionCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> AddParameterObservationAsync(
        AddParameterObservationCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> AddProcessEventAsync(
        AddProcessEventCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> AddDowntimeEventAsync(
        AddDowntimeEventCommand command,
        CancellationToken cancellationToken);
}



