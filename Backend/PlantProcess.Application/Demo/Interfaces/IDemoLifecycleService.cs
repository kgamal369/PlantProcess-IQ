using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Demo.Contracts;

namespace PlantProcess.Application.Demo.Interfaces;

public interface IDemoLifecycleService
{
    Task<ApplicationResult<DemoLifecycleDto>> GetDemoLifecycleAsync(CancellationToken cancellationToken);
}