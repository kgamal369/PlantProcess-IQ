using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface IFeatureEngineeringService
{
    Task<ApplicationResult<MaterialFeatureVectorDto>> BuildMaterialFeatureVectorAsync(
        Guid materialUnitId,
        CancellationToken cancellationToken);
}





