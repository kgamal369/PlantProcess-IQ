using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Services.Analytics;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class CorrelationEndpoints
{
    public static IEndpointRouteBuilder MapCorrelationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/correlations")
            .WithTags("Correlation Analytics");

        group.MapGet("/parameter-defect", GetParameterDefectCorrelationAsync);
        group.MapGet("/equipment-defect-rate", GetEquipmentDefectRateAsync);
        group.MapGet("/operation-defect-rate", GetOperationDefectRateAsync);
        group.MapGet("/materials/{materialUnitId:guid}/context", GetMaterialCorrelationContextAsync);

        return app;
    }

    private static async Task<IResult> GetParameterDefectCorrelationAsync(
        string parameterCode,
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? bins,
        int? minimumObservationsPerBin,
        bool? persistResult,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetParameterDefectCorrelationAsync(
            new ParameterDefectCorrelationQuery(
                parameterCode,
                defectType,
                siteId,
                fromUtc,
                toUtc,
                bins ?? 8,
                minimumObservationsPerBin ?? 5,
                persistResult ?? false),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetEquipmentDefectRateAsync(
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? minimumMaterialsPerEquipment,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetEquipmentDefectRateAsync(
            new EquipmentDefectRateQuery(
                defectType,
                siteId,
                fromUtc,
                toUtc,
                minimumMaterialsPerEquipment ?? 5),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetOperationDefectRateAsync(
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? minimumMaterialsPerOperation,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOperationDefectRateAsync(
            new OperationDefectRateQuery(
                defectType,
                siteId,
                fromUtc,
                toUtc,
                minimumMaterialsPerOperation ?? 5),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetMaterialCorrelationContextAsync(
        Guid materialUnitId,
        string defectType,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMaterialCorrelationContextAsync(materialUnitId, defectType, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}
