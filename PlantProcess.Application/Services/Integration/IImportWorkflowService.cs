using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface IImportWorkflowService
{
    Task<ApplicationResult<ImportWorkflowResult>> RunAsync(
        RunImportWorkflowCommand command,
        CancellationToken cancellationToken);
}
