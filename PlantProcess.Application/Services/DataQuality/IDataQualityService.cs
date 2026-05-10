using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;

namespace PlantProcess.Application.Services.DataQuality;

public interface IDataQualityService
{
    Task<ApplicationResult<Guid>> RaiseIssueAsync(
        RaiseDataQualityIssueCommand command,
        CancellationToken cancellationToken);
}