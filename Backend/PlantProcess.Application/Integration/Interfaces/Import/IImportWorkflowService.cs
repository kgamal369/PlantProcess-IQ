using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Interfaces.Import;

public interface IImportWorkflowService
{
    Task<ApplicationResult<ImportWorkflowResult>> RunAsync(
        RunImportWorkflowCommand command,
        CancellationToken cancellationToken);
}




