using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface ICorrelationService
{
    Task<ApplicationResult<ParameterDefectCorrelationResult>> GetParameterDefectCorrelationAsync(
        ParameterDefectCorrelationQuery query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EquipmentDefectRateResult>> GetEquipmentDefectRateAsync(
        EquipmentDefectRateQuery query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<OperationDefectRateResult>> GetOperationDefectRateAsync(
        OperationDefectRateQuery query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<MaterialCorrelationContextResult>> GetMaterialCorrelationContextAsync(
        Guid materialUnitId,
        string defectType,
        CancellationToken cancellationToken);
}





