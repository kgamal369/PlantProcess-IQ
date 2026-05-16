using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration.Commands;
using PlantProcess.Application.Contracts.Integration.Dtos;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IImportWorkflowService
{
    Task<ApplicationResult<ImportWorkflowResult>> RunAsync(
        RunImportWorkflowCommand command,
        CancellationToken cancellationToken);
}
