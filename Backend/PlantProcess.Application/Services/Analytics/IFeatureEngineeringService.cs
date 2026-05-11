using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IFeatureEngineeringService
{
    Task<ApplicationResult<MaterialFeatureVectorDto>> BuildMaterialFeatureVectorAsync(
        Guid materialUnitId,
        CancellationToken cancellationToken);
}
